using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.Materials;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Inventories.UpdateInventory;

public sealed class UpdateInventoryCommandHandler(
    IInventoryRepository repository,
    IMaterialRepository materialRepository,
    ICurrentUser currentUser) : IRequestHandler<UpdateInventoryCommand, Result<InventoryResponse>> {
    public async ValueTask<Result<InventoryResponse>> Handle(
        UpdateInventoryCommand command,
        CancellationToken cancellationToken) {
        var inventory = await repository.GetByIdAsync(
            command.InventoryId,
            currentUser.CompanyId,
            cancellationToken);
        if (inventory is null) {
            return Result<InventoryResponse>.Failure(InventoryErrors.NotFound);
        }

        var material = await materialRepository.GetByIdAsync(
            command.MaterialId,
            currentUser.CompanyId,
            cancellationToken);
        if (material is null) {
            return Result<InventoryResponse>.Failure(InventoryErrors.MaterialNotFound);
        }

        var warehouse = command.Warehouse.Trim();
        if (await repository.EntryExistsAsync(
                currentUser.CompanyId,
                material.Id,
                warehouse,
                inventory.Id,
                cancellationToken)) {
            return Result<InventoryResponse>.Failure(InventoryErrors.EntryAlreadyExists);
        }

        inventory.MaterialId = material.Id;
        inventory.Material = material;
        inventory.Warehouse = warehouse;
        inventory.Quantity = command.Quantity;
        inventory.UpdatedAt = DateTime.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);
        return Result<InventoryResponse>.Success(InventoryResponse.From(inventory));
    }
}
