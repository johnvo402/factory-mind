using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Materials.DeleteMaterial;

public sealed record DeleteMaterialCommand(Guid MaterialId)
    : IRequest<Result>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
