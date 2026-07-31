using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.BusinessData;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Machines.UpdateMachine;

public sealed class UpdateMachineCommandHandler(
    IMachineRepository repository,
    ICurrentUser currentUser) : IRequestHandler<UpdateMachineCommand, Result<MachineResponse>> {
    public async ValueTask<Result<MachineResponse>> Handle(
        UpdateMachineCommand command,
        CancellationToken cancellationToken) {
        var machine = await repository.GetByIdAsync(
            command.MachineId,
            currentUser.CompanyId,
            cancellationToken);
        if (machine is null) {
            return Result<MachineResponse>.Failure(MachineErrors.NotFound);
        }

        var code = BusinessDataNormalization.Code(command.Code);
        if (await repository.CodeExistsAsync(
                currentUser.CompanyId,
                code,
                machine.Id,
                cancellationToken)) {
            return Result<MachineResponse>.Failure(MachineErrors.CodeAlreadyExists);
        }

        machine.Code = code;
        machine.Name = BusinessDataNormalization.Name(command.Name);
        machine.Status = command.Status.Trim().ToLowerInvariant();
        machine.UpdatedAt = DateTime.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);
        return Result<MachineResponse>.Success(MachineResponse.From(machine));
    }
}
