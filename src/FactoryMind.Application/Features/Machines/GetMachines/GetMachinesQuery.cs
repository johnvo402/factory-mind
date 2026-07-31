using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Machines.GetMachines;

public sealed record GetMachinesQuery(string? Search)
    : IRequest<Result<IReadOnlyList<MachineResponse>>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
