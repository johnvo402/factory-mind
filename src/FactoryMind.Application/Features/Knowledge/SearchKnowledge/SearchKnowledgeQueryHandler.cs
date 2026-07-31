using FactoryMind.Application.Common.Identity;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Knowledge.SearchKnowledge;

public sealed class SearchKnowledgeQueryHandler(
    KnowledgeRetriever knowledgeRetriever,
    ICurrentUser currentUser) : IRequestHandler<SearchKnowledgeQuery, Result<IReadOnlyList<KnowledgeSearchResult>>> {
    public async ValueTask<Result<IReadOnlyList<KnowledgeSearchResult>>> Handle(
        SearchKnowledgeQuery query,
        CancellationToken cancellationToken) {
        var results = await knowledgeRetriever.SearchAsync(
            currentUser.CompanyId,
            query.Query,
            query.Limit,
            cancellationToken);
        return Result<IReadOnlyList<KnowledgeSearchResult>>.Success(results);
    }
}
