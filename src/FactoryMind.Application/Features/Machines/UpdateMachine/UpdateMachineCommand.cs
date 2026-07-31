using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Machines.UpdateMachine;

public sealed record UpdateMachineCommand(Guid MachineId, string Code, string Name, string Status)
    : IRequest<Result<MachineResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
