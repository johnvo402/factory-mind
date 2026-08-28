using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.Materials;
using FactoryMind.Application.Features.Warehouses;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Inventories.ReceiveInventory;

public sealed class ReceiveInventoryCommandHandler(
    IInventoryRepository inventoryRepository,
    IWarehouseRepository warehouseRepository,
    IMaterialRepository materialRepository,
    ICurrentUser currentUser)
    : IRequestHandler<ReceiveInventoryCommand, Result<InventoryTransactionResponse>> {
    public async ValueTask<Result<InventoryTransactionResponse>> Handle(
        ReceiveInventoryCommand command,
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

        var transaction = InventoryTransactionFactory.Create(
            currentUser, warehouse, material, InventoryTransactionType.Receipt,
            command.Quantity, command.Note, command.ReferenceType, command.ReferenceId);
        await inventoryRepository.ApplyAsync(transaction, cancellationToken);
        return Result<InventoryTransactionResponse>.Success(InventoryTransactionResponse.From(transaction));
    }
}
