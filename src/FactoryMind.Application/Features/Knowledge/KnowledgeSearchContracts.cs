namespace FactoryMind.Application.Features.Knowledge;

public static class KnowledgeSearchConstraints {
    public const int DefaultLimit = 5;
    public const int MaximumLimit = 20;
    public const int MaximumQueryLength = 2000;
    public const int VectorCandidateLimit = 20;
    public const int LexicalCandidateLimit = 20;
    public const int ContextResultLimit = 8;
    public const int ReciprocalRankConstant = 60;
}

public sealed record KnowledgeSearchResult(
    Guid DocumentId,
    string DocumentTitle,
    string FileName,
    Guid ChunkId,
    int PageNumber,
    string Content,
    double Score);

public sealed record KnowledgeSearchCandidate(
    Guid DocumentId,
    string DocumentTitle,
    string FileName,
    Guid ChunkId,
    int ChunkSequence,
    int PageNumber,
    string Content,
    int? VectorRank = null,
    int? LexicalRank = null,
    double? VectorScore = null,
    double? LexicalScore = null);

public interface IKnowledgeSearchRepository {
    Task<IReadOnlyList<KnowledgeSearchCandidate>> RetrieveCandidatesAsync(
        Guid companyId,
        string embeddingModel,
        string normalizedQuery,
        float[] queryEmbedding,
        int vectorLimit,
        int lexicalLimit,
        CancellationToken cancellationToken);
}
