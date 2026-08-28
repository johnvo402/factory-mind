using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Boms.GetBoms;

public sealed record GetBomsQuery(Guid ProductId)
    : IRequest<Result<IReadOnlyList<BomResponse>>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
