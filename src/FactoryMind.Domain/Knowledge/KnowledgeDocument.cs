using FactoryMind.Domain.Identity;

namespace FactoryMind.Domain.Knowledge;

public sealed class KnowledgeDocument {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }
    public Guid UploadedByUserId { get; set; }
    public User? UploadedByUser { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/pdf";
    public string Path { get; set; } = string.Empty;
    public long Size { get; set; }
    public string Status { get; set; } = DocumentStatuses.Uploaded;
    public int PageCount { get; set; }
    public int ChunkCount { get; set; }
    public string? ProcessingError { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<DocumentChunk> Chunks { get; set; } = [];
}

public static class DocumentStatuses {
    public const string Uploaded = "uploaded";
    public const string Processing = "processing";
    public const string Ready = "ready";
    public const string Failed = "failed";
}
