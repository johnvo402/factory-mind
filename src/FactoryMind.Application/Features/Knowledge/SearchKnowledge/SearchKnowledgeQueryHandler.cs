using FactoryMind.Application.Common.Identity;
using FactoryMind.Shared.AI;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Knowledge.SearchKnowledge;

public sealed class SearchKnowledgeQueryHandler(
    IEmbeddingClient embeddingClient,
    IKnowledgeSearchRepository searchRepository,
    ICurrentUser currentUser) : IRequestHandler<SearchKnowledgeQuery, Result<IReadOnlyList<KnowledgeSearchResult>>> {
    public async ValueTask<Result<IReadOnlyList<KnowledgeSearchResult>>> Handle(
        SearchKnowledgeQuery query,
        CancellationToken cancellationToken) {
        var embedding = await embeddingClient.CreateAsync([query.Query.Trim()], cancellationToken);
        if (embedding.Vectors.Count != 1
            || embedding.Vectors[0].Length != DocumentEmbeddingConstraints.Dimensions) {
            throw new AiProviderException("AI service returned an invalid embedding response.");
        }

        var results = await searchRepository.SearchAsync(
            currentUser.CompanyId,
            embedding.Model,
            embedding.Vectors[0],
            query.Limit,
            cancellationToken);
        return Result<IReadOnlyList<KnowledgeSearchResult>>.Success(results);
    }
}
