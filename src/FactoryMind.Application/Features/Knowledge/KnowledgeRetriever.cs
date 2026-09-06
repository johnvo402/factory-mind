using FactoryMind.Shared.AI;
using FactoryMind.Application.Common.Search;

namespace FactoryMind.Application.Features.Knowledge;

public sealed class KnowledgeRetriever(
    IEmbeddingClient embeddingClient,
    IKnowledgeSearchRepository searchRepository) {
    public async Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(
        Guid companyId,
        string query,
        int limit,
        CancellationToken cancellationToken) {
        var embeddingQuery = string.Join(' ', query.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        var embedding = await embeddingClient.CreateAsync(
            [embeddingQuery],
            EmbeddingPurpose.Query,
            cancellationToken);
        if (embedding.Vectors.Count != 1
            || embedding.Vectors[0].Length != DocumentEmbeddingConstraints.Dimensions) {
            throw new AiProviderException("AI service returned an invalid embedding response.");
        }

        var normalizedQuery = SearchTextNormalizer.NormalizeLexical(query);
        var candidates = await searchRepository.RetrieveCandidatesAsync(
            companyId,
            embedding.Model,
            normalizedQuery,
            embedding.Vectors[0],
            KnowledgeSearchConstraints.VectorCandidateLimit,
            KnowledgeSearchConstraints.LexicalCandidateLimit,
            cancellationToken);
        return HybridKnowledgeRanker.Rank(query, candidates, limit);
    }
}
