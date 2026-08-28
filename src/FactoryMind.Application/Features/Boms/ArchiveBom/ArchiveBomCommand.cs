using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Boms.ArchiveBom;

public sealed record ArchiveBomCommand(Guid ProductId, Guid BomId)
    : IRequest<Result<BomResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
