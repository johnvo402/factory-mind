using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Materials.CreateMaterial;

public sealed record CreateMaterialCommand(string Code, string Name, string Unit)
    : IRequest<Result<MaterialResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
