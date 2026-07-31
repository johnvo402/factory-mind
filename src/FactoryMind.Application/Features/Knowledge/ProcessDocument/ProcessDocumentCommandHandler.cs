using FactoryMind.Domain.Knowledge;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Knowledge.ProcessDocument;

public sealed class ProcessDocumentCommandHandler(
    IDocumentRepository repository,
    IFileStorage fileStorage,
    IDocumentTextExtractor textExtractor,
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

            await repository.CompleteProcessingAsync(
                document,
                entities,
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
}
