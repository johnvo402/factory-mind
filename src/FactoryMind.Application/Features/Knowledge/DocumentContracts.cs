using FactoryMind.Domain.Knowledge;
using FactoryMind.Shared.Contracts;

namespace FactoryMind.Application.Features.Knowledge;

public static class DocumentUploadConstraints {
    public const long MaximumFileSize = 100 * 1024 * 1024;
    public const long MaximumRequestSize = MaximumFileSize + (1024 * 1024);
    public const int MaximumFileNameLength = 255;
    public const int MaximumTitleLength = 200;
    public const string PdfContentType = "application/pdf";
}

public sealed record DocumentResponse(
    Guid Id,
    string Title,
    string FileName,
    string ContentType,
    long Size,
    string Status,
    int PageCount,
    int ChunkCount,
    string? ProcessingError,
    DateTime CreatedAt,
    DateTime? ProcessedAt) {
    public static DocumentResponse From(KnowledgeDocument document) => new(
        document.Id,
        document.Title,
        document.FileName,
        document.ContentType,
        document.Size,
        document.Status,
        document.PageCount,
        document.ChunkCount,
        document.ProcessingError,
        document.CreatedAt,
        document.ProcessedAt);
}

public sealed record DocumentPageText(int PageNumber, string Content);

public sealed record DocumentChunkDraft(int Sequence, int PageNumber, string Content);

public interface IDocumentRepository {
    Task<IReadOnlyList<KnowledgeDocument>> GetByCompanyAsync(
        Guid companyId,
        CancellationToken cancellationToken);

    Task<KnowledgeDocument?> GetByIdAsync(
        Guid documentId,
        Guid companyId,
        CancellationToken cancellationToken);

    Task<KnowledgeDocument?> GetForProcessingAsync(
        Guid documentId,
        Guid companyId,
        CancellationToken cancellationToken);

    Task MarkProcessingAsync(
        Guid documentId,
        Guid companyId,
        CancellationToken cancellationToken);

    Task CompleteProcessingAsync(
        KnowledgeDocument document,
        IReadOnlyList<DocumentChunk> chunks,
        int pageCount,
        DateTime processedAt,
        CancellationToken cancellationToken);

    Task MarkProcessingFailedAsync(
        Guid documentId,
        Guid companyId,
        string processingError,
        DateTime processedAt,
        CancellationToken cancellationToken);

    void Add(KnowledgeDocument document);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IFileStorage {
    Task UploadAsync(
        string objectKey,
        Stream content,
        long length,
        string contentType,
        CancellationToken cancellationToken);

    Task<Stream> DownloadAsync(string objectKey, CancellationToken cancellationToken);
}

public interface IDocumentTextExtractor {
    Task<IReadOnlyList<DocumentPageText>> ExtractAsync(
        Stream content,
        CancellationToken cancellationToken);
}

public interface IDocumentProcessingQueue {
    void Enqueue(Guid documentId, Guid companyId);
}

public sealed class FileStorageException : Exception {
    public FileStorageException(string message, Exception? innerException = null)
        : base(message, innerException) {
    }
}

public sealed class DocumentParsingException : Exception {
    public DocumentParsingException(string message, Exception? innerException = null)
        : base(message, innerException) {
    }
}

public sealed class DocumentProcessingQueueException : Exception {
    public DocumentProcessingQueueException(string message, Exception? innerException = null)
        : base(message, innerException) {
    }
}

public static class DocumentErrors {
    public static readonly Error InvalidPdf = new(
        "knowledge.invalid_pdf",
        "The uploaded file is not a valid PDF.",
        400);

    public static readonly Error StorageUnavailable = new(
        "knowledge.storage_unavailable",
        "File storage is temporarily unavailable.",
        503);

    public static readonly Error NotFound = new(
        "knowledge.document_not_found",
        "Document was not found.",
        404);

    public static readonly Error ProcessingAlreadyRunning = new(
        "knowledge.processing_already_running",
        "Document processing is already running.",
        409);

    public static readonly Error QueueUnavailable = new(
        "knowledge.queue_unavailable",
        "Document processing could not be queued.",
        503);
}
