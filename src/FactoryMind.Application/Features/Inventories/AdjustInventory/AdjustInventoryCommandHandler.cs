using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.Materials;
using FactoryMind.Application.Features.Warehouses;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Inventories.AdjustInventory;

public sealed class AdjustInventoryCommandHandler(
    IInventoryRepository inventoryRepository,
    IWarehouseRepository warehouseRepository,
    IMaterialRepository materialRepository,
    ICurrentUser currentUser)
    : IRequestHandler<AdjustInventoryCommand, Result<InventoryTransactionResponse>> {
    public async ValueTask<Result<InventoryTransactionResponse>> Handle(
        AdjustInventoryCommand command,
        CancellationToken cancellationToken) {
        var warehouse = await warehouseRepository.GetByIdAsync(
            command.WarehouseId, currentUser.CompanyId, cancellationToken);
        if (warehouse is not { IsActive: true }) {
            return Result<InventoryTransactionResponse>.Failure(InventoryErrors.WarehouseNotFound);
        }
        var material = await materialRepository.GetByIdAsync(
            command.MaterialId, currentUser.CompanyId, cancellationToken);
        if (material is null) {
            return Result<InventoryTransactionResponse>.Failure(InventoryErrors.MaterialNotFound);
        }

        var type = command.Direction == InventoryAdjustmentDirection.Increase
            ? InventoryTransactionType.AdjustmentIncrease
            : InventoryTransactionType.AdjustmentDecrease;
        var transaction = InventoryTransactionFactory.Create(
            currentUser, warehouse, material, type,
            command.Quantity, command.Note, command.ReferenceType, command.ReferenceId);
        var outcome = await inventoryRepository.ApplyAsync(transaction, cancellationToken);
        return outcome.Status == InventoryOperationStatus.InsufficientStock
            ? Result<InventoryTransactionResponse>.Failure(InventoryErrors.InsufficientStock)
            : Result<InventoryTransactionResponse>.Success(InventoryTransactionResponse.From(transaction));
    }
}
