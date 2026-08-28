using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.BusinessData;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Warehouses.UpdateWarehouse;

public sealed class UpdateWarehouseCommandHandler(
    IWarehouseRepository repository,
    ICurrentUser currentUser) : IRequestHandler<UpdateWarehouseCommand, Result<WarehouseResponse>> {
    public async ValueTask<Result<WarehouseResponse>> Handle(
        UpdateWarehouseCommand command,
        CancellationToken cancellationToken) {
        var warehouse = await repository.GetByIdAsync(
            command.WarehouseId, currentUser.CompanyId, cancellationToken);
        if (warehouse is null) {
            return Result<WarehouseResponse>.Failure(WarehouseErrors.NotFound);
        }
        var code = BusinessDataNormalization.Code(command.Code);
        if (await repository.CodeExistsAsync(
            currentUser.CompanyId, code, warehouse.Id, cancellationToken)) {
            return Result<WarehouseResponse>.Failure(WarehouseErrors.CodeAlreadyExists);
        }
        warehouse.Code = code;
        warehouse.Name = BusinessDataNormalization.Name(command.Name);
        warehouse.Description = string.IsNullOrWhiteSpace(command.Description)
            ? null
            : command.Description.Trim();
        warehouse.IsActive = command.IsActive;
        warehouse.UpdatedAt = DateTime.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);
        return Result<WarehouseResponse>.Success(WarehouseResponse.From(warehouse));
    }
}
