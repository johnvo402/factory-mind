namespace FactoryMind.Application.Features.Knowledge;

public static class KnowledgeSearchConstraints {
    public const int DefaultLimit = 5;
    public const int MaximumLimit = 20;
    public const int MaximumQueryLength = 2000;
}

public sealed record KnowledgeSearchResult(
    Guid DocumentId,
    string DocumentTitle,
    string FileName,
    Guid ChunkId,
    int PageNumber,
    string Content,
    double Score);

public interface IKnowledgeSearchRepository {
    Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(
        Guid companyId,
        string embeddingModel,
        float[] queryEmbedding,
        int limit,
        CancellationToken cancellationToken);
}
