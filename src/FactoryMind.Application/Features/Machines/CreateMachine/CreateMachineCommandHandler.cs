using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.BusinessData;
using FactoryMind.Application.Features.WorkCenters;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Machines.CreateMachine;

public sealed class CreateMachineCommandHandler(
    IMachineRepository repository,
    IWorkCenterRepository workCenterRepository,
    ICurrentUser currentUser) : IRequestHandler<CreateMachineCommand, Result<MachineResponse>> {
    public async ValueTask<Result<MachineResponse>> Handle(
        CreateMachineCommand command,
        CancellationToken cancellationToken) {
        var status = command.Status.Trim().ToLowerInvariant();
        if (!MachineStatuses.Administrative.Contains(status)) {
            return Result<MachineResponse>.Failure(MachineErrors.RunningIsSystemManaged);
        }

        WorkCenter? workCenter = null;
        if (command.WorkCenterId.HasValue) {
            workCenter = await workCenterRepository.GetByIdAsync(
                command.WorkCenterId.Value, currentUser.CompanyId, cancellationToken);
            if (workCenter is null) {
                return Result<MachineResponse>.Failure(MachineErrors.WorkCenterNotFound);
            }
            if (!workCenter.IsActive) {
                return Result<MachineResponse>.Failure(MachineErrors.WorkCenterInactive);
            }
        }

        var code = BusinessDataNormalization.Code(command.Code);
        if (await repository.CodeExistsAsync(currentUser.CompanyId, code, null, cancellationToken)) {
            return Result<MachineResponse>.Failure(MachineErrors.CodeAlreadyExists);
        }

        var now = DateTime.UtcNow;
        var machine = new Machine {
            CompanyId = currentUser.CompanyId,
            Code = code,
            Name = BusinessDataNormalization.Name(command.Name),
            Status = status,
            WorkCenterId = workCenter?.Id,
            WorkCenter = workCenter,
            CreatedAt = now,
            UpdatedAt = now
        };

        repository.Add(machine);
        await repository.SaveChangesAsync(cancellationToken);
        return Result<MachineResponse>.Success(MachineResponse.From(machine));
    }
}
