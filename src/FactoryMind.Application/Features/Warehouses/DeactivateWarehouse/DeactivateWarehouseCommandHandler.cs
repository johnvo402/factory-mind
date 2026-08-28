using FactoryMind.Application.Common.Identity;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Warehouses.DeactivateWarehouse;

public sealed class DeactivateWarehouseCommandHandler(
    IWarehouseRepository repository,
    ICurrentUser currentUser) : IRequestHandler<DeactivateWarehouseCommand, Result> {
    public async ValueTask<Result> Handle(
        DeactivateWarehouseCommand command,
        CancellationToken cancellationToken) {
        var warehouse = await repository.GetByIdAsync(
            command.WarehouseId, currentUser.CompanyId, cancellationToken);
        if (warehouse is null) {
            return Result.Failure(WarehouseErrors.NotFound);
        }
        warehouse.IsActive = false;
        warehouse.UpdatedAt = DateTime.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
