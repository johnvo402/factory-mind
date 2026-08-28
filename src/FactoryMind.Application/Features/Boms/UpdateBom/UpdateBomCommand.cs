using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Boms.UpdateBom;

public sealed record UpdateBomCommand(
    Guid ProductId,
    Guid BomId,
    decimal OutputQuantity,
    IReadOnlyList<BomItemDefinition> Items) : IRequest<Result<BomResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
