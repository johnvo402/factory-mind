using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;

namespace FactoryMind.Application.Features.Inventories;

public static class InventoryConstraints {
    public const int MaximumWarehouseLength = 100;
    public const int QuantityPrecision = 18;
    public const int QuantityScale = 3;
}

public sealed record InventoryResponse(
    Guid Id,
    Guid MaterialId,
    string MaterialCode,
    string MaterialName,
    string Unit,
    string Warehouse,
    decimal Quantity,
    DateTime CreatedAt,
    DateTime UpdatedAt) {
    public static InventoryResponse From(Inventory inventory) => new(
        inventory.Id,
        inventory.MaterialId,
        inventory.Material?.Code ?? string.Empty,
        inventory.Material?.Name ?? string.Empty,
        inventory.Material?.Unit ?? string.Empty,
        inventory.Warehouse,
        inventory.Quantity,
        inventory.CreatedAt,
        inventory.UpdatedAt);
}

public interface IInventoryRepository {
    Task<IReadOnlyList<Inventory>> GetByCompanyAsync(
        Guid companyId,
        string? search,
        CancellationToken cancellationToken);

    Task<Inventory?> GetByIdAsync(
        Guid inventoryId,
        Guid companyId,
        CancellationToken cancellationToken);

    Task<bool> EntryExistsAsync(
        Guid companyId,
        Guid materialId,
        string warehouse,
        Guid? excludedInventoryId,
        CancellationToken cancellationToken);

    void Add(Inventory inventory);
    void Remove(Inventory inventory);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public static class InventoryErrors {
    public static readonly Error NotFound = new(
        "inventories.not_found",
        "Inventory entry was not found.",
        404);

    public static readonly Error MaterialNotFound = new(
        "inventories.material_not_found",
        "Material was not found.",
        404);

    public static readonly Error EntryAlreadyExists = new(
        "inventories.entry_already_exists",
        "An inventory entry for this material and warehouse already exists.",
        409);
}
