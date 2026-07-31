using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Knowledge.SearchKnowledge;

public sealed record SearchKnowledgeQuery(
    string Query,
    int Limit) : IRequest<Result<IReadOnlyList<KnowledgeSearchResult>>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Authenticated;
}
