using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Machines.CreateMachine;

public sealed record CreateMachineCommand(string Code, string Name, string Status, Guid? WorkCenterId)
    : IRequest<Result<MachineResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
