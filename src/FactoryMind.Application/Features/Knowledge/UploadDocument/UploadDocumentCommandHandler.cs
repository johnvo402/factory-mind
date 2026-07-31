using System.Text;
using FactoryMind.Application.Common.Identity;
using FactoryMind.Domain.Knowledge;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Knowledge.UploadDocument;

public sealed class UploadDocumentCommandHandler(
    IDocumentRepository repository,
    IFileStorage fileStorage,
    IDocumentProcessingQueue processingQueue,
    ICurrentUser currentUser) : IRequestHandler<UploadDocumentCommand, Result<DocumentResponse>> {
    public async ValueTask<Result<DocumentResponse>> Handle(
        UploadDocumentCommand command,
        CancellationToken cancellationToken) {
        if (!await HasPdfSignatureAsync(command.Content, cancellationToken)) {
            return Result<DocumentResponse>.Failure(DocumentErrors.InvalidPdf);
        }

        var fileName = Path.GetFileName(command.FileName);
        var document = new KnowledgeDocument {
            CompanyId = currentUser.CompanyId,
            UploadedByUserId = currentUser.UserId,
            Title = CreateTitle(command.Title, fileName),
            FileName = fileName,
            ContentType = command.ContentType,
            Size = command.Length,
            Status = DocumentStatuses.Uploaded
        };
        document.Path = $"companies/{document.CompanyId:N}/documents/{document.Id:N}/{fileName}";

        try {
            await fileStorage.UploadAsync(
                document.Path,
                command.Content,
                command.Length,
                command.ContentType,
                cancellationToken);
        } catch (FileStorageException) {
            return Result<DocumentResponse>.Failure(DocumentErrors.StorageUnavailable);
        }

        repository.Add(document);
        await repository.SaveChangesAsync(cancellationToken);

        try {
            processingQueue.Enqueue(document.Id, document.CompanyId);
        } catch (DocumentProcessingQueueException) {
            document.Status = DocumentStatuses.Failed;
            document.ProcessingError = "Document processing could not be queued.";
            await repository.SaveChangesAsync(cancellationToken);
        }

        return Result<DocumentResponse>.Success(DocumentResponse.From(document));
    }

    private static async Task<bool> HasPdfSignatureAsync(Stream content, CancellationToken cancellationToken) {
        if (!content.CanSeek) {
            return false;
        }

        var originalPosition = content.Position;
        var signature = new byte[5];
        var bytesRead = await content.ReadAsync(signature, cancellationToken);
        content.Position = originalPosition;
        return bytesRead == signature.Length && Encoding.ASCII.GetString(signature) == "%PDF-";
    }

    private static string CreateTitle(string? title, string fileName) {
        var value = string.IsNullOrWhiteSpace(title)
            ? Path.GetFileNameWithoutExtension(fileName)
            : title.Trim();
        return value.Length <= DocumentUploadConstraints.MaximumTitleLength
            ? value
            : value[..DocumentUploadConstraints.MaximumTitleLength];
    }

}
