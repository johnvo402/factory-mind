using FactoryMind.Application.Common.Identity;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Machines.DeleteMachine;

public sealed class DeleteMachineCommandHandler(
    IMachineRepository repository,
    ICurrentUser currentUser) : IRequestHandler<DeleteMachineCommand, Result> {
    public async ValueTask<Result> Handle(
        DeleteMachineCommand command,
        CancellationToken cancellationToken) {
        var machine = await repository.GetByIdAsync(
            command.MachineId,
            currentUser.CompanyId,
            cancellationToken);
        if (machine is null) {
            return Result.Failure(MachineErrors.NotFound);
        }

        if (await repository.HasActiveOperationAsync(machine.Id, currentUser.CompanyId, cancellationToken)) {
            return Result.Failure(MachineErrors.ActiveExecution);
        }
        if (await repository.HasOperationReferenceAsync(machine.Id, currentUser.CompanyId, cancellationToken)) {
            return Result.Failure(MachineErrors.HistoryProtected);
        }

        return await repository.TryDeleteAsync(machine, cancellationToken)
            ? Result.Success()
            : Result.Failure(MachineErrors.HistoryProtected);
    }
}
