using Pgvector;

namespace FactoryMind.Infrastructure.Persistence.Knowledge;

public sealed class DocumentEmbeddingRecord {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentChunkId { get; set; }
    public Guid CompanyId { get; set; }
    public string Model { get; set; } = string.Empty;
    public int Dimensions { get; set; }
    public Vector Embedding { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
