using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.BusinessData;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Machines.CreateMachine;

public sealed class CreateMachineCommandHandler(
    IMachineRepository repository,
    ICurrentUser currentUser) : IRequestHandler<CreateMachineCommand, Result<MachineResponse>> {
    public async ValueTask<Result<MachineResponse>> Handle(
        CreateMachineCommand command,
        CancellationToken cancellationToken) {
        var code = BusinessDataNormalization.Code(command.Code);
        if (await repository.CodeExistsAsync(currentUser.CompanyId, code, null, cancellationToken)) {
            return Result<MachineResponse>.Failure(MachineErrors.CodeAlreadyExists);
        }

        var now = DateTime.UtcNow;
        var machine = new Machine {
            CompanyId = currentUser.CompanyId,
            Code = code,
            Name = BusinessDataNormalization.Name(command.Name),
            Status = command.Status.Trim().ToLowerInvariant(),
            CreatedAt = now,
            UpdatedAt = now
        };

        repository.Add(machine);
        await repository.SaveChangesAsync(cancellationToken);
        return Result<MachineResponse>.Success(MachineResponse.From(machine));
    }
}
