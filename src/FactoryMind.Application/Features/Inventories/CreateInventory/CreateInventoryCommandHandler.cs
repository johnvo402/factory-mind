using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.Materials;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Inventories.CreateInventory;

public sealed class CreateInventoryCommandHandler(
    IInventoryRepository repository,
    IMaterialRepository materialRepository,
    ICurrentUser currentUser) : IRequestHandler<CreateInventoryCommand, Result<InventoryResponse>> {
    public async ValueTask<Result<InventoryResponse>> Handle(
        CreateInventoryCommand command,
        CancellationToken cancellationToken) {
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
                null,
                cancellationToken)) {
            return Result<InventoryResponse>.Failure(InventoryErrors.EntryAlreadyExists);
        }

        var now = DateTime.UtcNow;
        var inventory = new Inventory {
            CompanyId = currentUser.CompanyId,
            MaterialId = material.Id,
            Material = material,
            Warehouse = warehouse,
            Quantity = command.Quantity,
            CreatedAt = now,
            UpdatedAt = now
        };
        repository.Add(inventory);
        await repository.SaveChangesAsync(cancellationToken);
        return Result<InventoryResponse>.Success(InventoryResponse.From(inventory));
    }
}
