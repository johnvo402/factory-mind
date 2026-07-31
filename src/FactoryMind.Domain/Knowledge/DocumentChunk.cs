namespace FactoryMind.Domain.Knowledge;

public sealed class DocumentChunk {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public KnowledgeDocument? Document { get; set; }
    public Guid CompanyId { get; set; }
    public int Sequence { get; set; }
    public int PageNumber { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
