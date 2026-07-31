using FactoryMind.Shared.AI;

namespace FactoryMind.Application.Features.Knowledge;

public sealed class KnowledgeRetriever(
    IEmbeddingClient embeddingClient,
    IKnowledgeSearchRepository searchRepository) {
    public async Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(
        Guid companyId,
        string query,
        int limit,
        CancellationToken cancellationToken) {
        var embedding = await embeddingClient.CreateAsync(
            [query.Trim()],
            EmbeddingPurpose.Query,
            cancellationToken);
        if (embedding.Vectors.Count != 1
            || embedding.Vectors[0].Length != DocumentEmbeddingConstraints.Dimensions) {
            throw new AiProviderException("AI service returned an invalid embedding response.");
        }

        return await searchRepository.SearchAsync(
            companyId,
            embedding.Model,
            embedding.Vectors[0],
            limit,
            cancellationToken);
    }
}
