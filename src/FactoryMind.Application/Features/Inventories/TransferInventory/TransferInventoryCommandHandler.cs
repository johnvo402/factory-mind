using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.Materials;
using FactoryMind.Application.Features.Warehouses;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Inventories.TransferInventory;

public sealed class TransferInventoryCommandHandler(
    IInventoryRepository inventoryRepository,
    IWarehouseRepository warehouseRepository,
    IMaterialRepository materialRepository,
    ICurrentUser currentUser)
    : IRequestHandler<TransferInventoryCommand, Result<IReadOnlyList<InventoryTransactionResponse>>> {
    public async ValueTask<Result<IReadOnlyList<InventoryTransactionResponse>>> Handle(
        TransferInventoryCommand command,
        CancellationToken cancellationToken) {
        if (command.SourceWarehouseId == command.DestinationWarehouseId) {
            return Result<IReadOnlyList<InventoryTransactionResponse>>.Failure(
                InventoryErrors.SameWarehouseTransfer);
        }
        var source = await warehouseRepository.GetByIdAsync(
            command.SourceWarehouseId, currentUser.CompanyId, cancellationToken);
        var destination = await warehouseRepository.GetByIdAsync(
            command.DestinationWarehouseId, currentUser.CompanyId, cancellationToken);
        if (source is not { IsActive: true } || destination is not { IsActive: true }) {
            return Result<IReadOnlyList<InventoryTransactionResponse>>.Failure(
                InventoryErrors.WarehouseNotFound);
        }
        var material = await materialRepository.GetByIdAsync(
            command.MaterialId, currentUser.CompanyId, cancellationToken);
        if (material is null) {
            return Result<IReadOnlyList<InventoryTransactionResponse>>.Failure(
                InventoryErrors.MaterialNotFound);
        }

        var correlationId = Guid.NewGuid();
        var referenceType = string.IsNullOrWhiteSpace(command.ReferenceType)
            ? "WarehouseTransfer"
            : command.ReferenceType.Trim();
        var transferOut = InventoryTransactionFactory.Create(
            currentUser, source, material, InventoryTransactionType.TransferOut,
            command.Quantity, command.Note, referenceType, correlationId);
        var transferIn = InventoryTransactionFactory.Create(
            currentUser, destination, material, InventoryTransactionType.TransferIn,
            command.Quantity, command.Note, referenceType, correlationId);
        var outcome = await inventoryRepository.TransferAsync(transferOut, transferIn, cancellationToken);
        return outcome.Status == InventoryOperationStatus.InsufficientStock
            ? Result<IReadOnlyList<InventoryTransactionResponse>>.Failure(InventoryErrors.InsufficientStock)
            : Result<IReadOnlyList<InventoryTransactionResponse>>.Success(
                outcome.Transactions.Select(InventoryTransactionResponse.From).ToList());
    }
}
