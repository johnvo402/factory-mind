using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.BusinessData;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Machines.UpdateMachine;

public sealed class UpdateMachineCommandHandler(
    IMachineRepository repository,
    ICurrentUser currentUser) : IRequestHandler<UpdateMachineCommand, Result<MachineResponse>> {
    public async ValueTask<Result<MachineResponse>> Handle(
        UpdateMachineCommand command,
        CancellationToken cancellationToken) {
        var status = command.Status.Trim().ToLowerInvariant();
        if (!MachineStatuses.Administrative.Contains(status)) {
            return Result<MachineResponse>.Failure(MachineErrors.RunningIsSystemManaged);
        }
        var code = BusinessDataNormalization.Code(command.Code);
        var result = await repository.TryUpdateAsync(
            command.MachineId,
            currentUser.CompanyId,
            code,
            BusinessDataNormalization.Name(command.Name),
            status,
            command.WorkCenterId,
            DateTime.UtcNow,
            cancellationToken);
        return result.Status switch {
            MachineUpdateStatus.Success => Result<MachineResponse>.Success(
                MachineResponse.From(result.Machine!)),
            MachineUpdateStatus.NotFound => Result<MachineResponse>.Failure(MachineErrors.NotFound),
            MachineUpdateStatus.CodeAlreadyExists =>
                Result<MachineResponse>.Failure(MachineErrors.CodeAlreadyExists),
            MachineUpdateStatus.WorkCenterNotFound =>
                Result<MachineResponse>.Failure(MachineErrors.WorkCenterNotFound),
            MachineUpdateStatus.WorkCenterInactive =>
                Result<MachineResponse>.Failure(MachineErrors.WorkCenterInactive),
            _ => Result<MachineResponse>.Failure(MachineErrors.ActiveExecution)
        };
    }
}
