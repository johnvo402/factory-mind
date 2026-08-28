using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Boms.CalculateMaterialRequirements;

public sealed record GetProductMaterialRequirementsQuery(Guid ProductId, decimal Quantity)
    : IRequest<Result<MaterialRequirementsResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
