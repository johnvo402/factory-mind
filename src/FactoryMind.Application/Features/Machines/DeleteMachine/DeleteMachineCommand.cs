using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Machines.DeleteMachine;

public sealed record DeleteMachineCommand(Guid MachineId)
    : IRequest<Result>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
