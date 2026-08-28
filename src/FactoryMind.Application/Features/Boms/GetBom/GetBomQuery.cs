using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Boms.GetBom;

public sealed record GetBomQuery(Guid ProductId, Guid BomId)
    : IRequest<Result<BomResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
