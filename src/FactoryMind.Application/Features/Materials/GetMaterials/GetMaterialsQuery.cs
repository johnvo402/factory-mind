using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Materials.GetMaterials;

public sealed record GetMaterialsQuery(string? Search)
    : IRequest<Result<IReadOnlyList<MaterialResponse>>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
