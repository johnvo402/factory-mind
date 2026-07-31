using FactoryMind.Domain.Knowledge;
using FactoryMind.Shared.AI;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Knowledge.ProcessDocument;

public sealed class ProcessDocumentCommandHandler(
    IDocumentRepository repository,
    IFileStorage fileStorage,
    IDocumentTextExtractor textExtractor,
    IEmbeddingClient embeddingClient,
    DocumentChunker chunker) : IRequestHandler<ProcessDocumentCommand, Result> {
    public async ValueTask<Result> Handle(
        ProcessDocumentCommand command,
        CancellationToken cancellationToken) {
        var document = await repository.GetForProcessingAsync(
            command.DocumentId,
            command.CompanyId,
            cancellationToken);
        if (document is null) {
            return Result.Failure(DocumentErrors.NotFound);
        }

        await repository.MarkProcessingAsync(document.Id, document.CompanyId, cancellationToken);

        try {
            await using var content = await fileStorage.DownloadAsync(document.Path, cancellationToken);
            var pages = await textExtractor.ExtractAsync(content, cancellationToken);
            var chunks = chunker.Chunk(pages);
            if (chunks.Count == 0) {
                await repository.MarkProcessingFailedAsync(
                    document.Id,
                    document.CompanyId,
                    "No readable text was found in the PDF. OCR is not supported.",
                    DateTime.UtcNow,
                    cancellationToken);
                return Result.Success();
            }

            var entities = new List<DocumentChunk>(chunks.Count);
            foreach (var chunk in chunks) {
                entities.Add(new DocumentChunk {
                    DocumentId = document.Id,
                    CompanyId = document.CompanyId,
                    Sequence = chunk.Sequence,
                    PageNumber = chunk.PageNumber,
                    Content = chunk.Content
                });
            }

            var embeddings = await CreateEmbeddingsAsync(entities, cancellationToken);

            await repository.CompleteProcessingAsync(
                document,
                entities,
                embeddings,
                pages.Count,
                DateTime.UtcNow,
                cancellationToken);
            return Result.Success();
        } catch (DocumentParsingException exception) {
            await repository.MarkProcessingFailedAsync(
                document.Id,
                document.CompanyId,
                LimitError(exception.Message),
                DateTime.UtcNow,
                cancellationToken);
            return Result.Success();
        } catch {
            await repository.MarkProcessingFailedAsync(
                document.Id,
                document.CompanyId,
                "Document processing failed and will be retried.",
                DateTime.UtcNow,
                cancellationToken);
            throw;
        }
    }

    private static string LimitError(string message) =>
        message.Length <= 500 ? message : message[..500];

    private async Task<IReadOnlyList<DocumentEmbeddingDraft>> CreateEmbeddingsAsync(
        IReadOnlyList<DocumentChunk> chunks,
        CancellationToken cancellationToken) {
        var embeddings = new List<DocumentEmbeddingDraft>(chunks.Count);

        for (var offset = 0; offset < chunks.Count; offset += DocumentEmbeddingConstraints.BatchSize) {
            var batch = chunks
                .Skip(offset)
                .Take(DocumentEmbeddingConstraints.BatchSize)
                .ToList();
            var response = await embeddingClient.CreateAsync(
                batch.Select(chunk => chunk.Content).ToList(),
                EmbeddingPurpose.Document,
                cancellationToken);
            if (response.Vectors.Count != batch.Count) {
                throw new AiProviderException("AI service returned an invalid embedding response.");
            }

            for (var index = 0; index < batch.Count; index++) {
                var values = response.Vectors[index];
                if (values.Length != DocumentEmbeddingConstraints.Dimensions) {
                    throw new AiProviderException(
                        $"AI service must return {DocumentEmbeddingConstraints.Dimensions}-dimensional embeddings.");
                }

                embeddings.Add(new DocumentEmbeddingDraft(
                    batch[index].Id,
                    batch[index].CompanyId,
                    response.Model,
                    values));
            }
        }

        return embeddings;
    }
}
