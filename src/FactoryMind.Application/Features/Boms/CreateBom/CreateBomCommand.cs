using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Boms.CreateBom;

public sealed record CreateBomCommand(
    Guid ProductId,
    decimal OutputQuantity,
    IReadOnlyList<BomItemDefinition> Items) : IRequest<Result<BomResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
