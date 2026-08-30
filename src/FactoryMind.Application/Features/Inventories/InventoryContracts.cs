using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;

namespace FactoryMind.Application.Features.Inventories;

public static class InventoryConstraints {
    public const int QuantityPrecision = 18;
    public const int QuantityScale = 6;
    public const int MaximumReferenceTypeLength = 100;
    public const int MaximumNoteLength = 500;
    public const int MaximumSearchLength = 200;
    public const int MaximumPageSize = 100;
}

public sealed record InventoryBalanceResponse(
    Guid Id,
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    Guid MaterialId,
    string MaterialCode,
    string MaterialName,
    string Unit,
    decimal Quantity,
    DateTime UpdatedAt) {
    public static InventoryBalanceResponse From(InventoryBalance balance) => new(
        balance.Id,
        balance.WarehouseId,
        balance.Warehouse?.Code ?? string.Empty,
        balance.Warehouse?.Name ?? string.Empty,
        balance.MaterialId,
        balance.Material?.Code ?? string.Empty,
        balance.Material?.Name ?? string.Empty,
        balance.Material?.Unit ?? string.Empty,
        balance.Quantity,
        balance.UpdatedAt);
}

public sealed record InventoryTransactionResponse(
    Guid Id,
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    Guid MaterialId,
    string MaterialCode,
    string MaterialName,
    string Unit,
    InventoryTransactionType Type,
    decimal Quantity,
    decimal SignedQuantity,
    string? ReferenceType,
    Guid? ReferenceId,
    string? Note,
    DateTime CreatedAt) {
    public static InventoryTransactionResponse From(InventoryTransaction transaction) => new(
        transaction.Id,
        transaction.WarehouseId,
        transaction.Warehouse?.Code ?? string.Empty,
        transaction.Warehouse?.Name ?? string.Empty,
        transaction.MaterialId,
        transaction.Material?.Code ?? string.Empty,
        transaction.Material?.Name ?? string.Empty,
        transaction.Material?.Unit ?? string.Empty,
        transaction.Type,
        transaction.Quantity,
        transaction.SignedQuantity(),
        transaction.ReferenceType,
        transaction.ReferenceId,
        transaction.Note,
        transaction.CreatedAt);
}

public sealed record InventoryTransactionPageResponse(
    IReadOnlyList<InventoryTransactionResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record InventoryOperationResult(
    InventoryOperationStatus Status,
    IReadOnlyList<InventoryTransaction> Transactions);

public enum InventoryOperationStatus {
    Success,
    InsufficientStock
}

public interface IInventoryRepository {
    Task<IReadOnlyList<InventoryBalance>> GetBalancesAsync(
        Guid companyId,
        Guid? warehouseId,
        Guid? materialId,
        string? search,
        CancellationToken cancellationToken);

    Task<(IReadOnlyList<InventoryTransaction> Items, int TotalCount)> GetTransactionsAsync(
        Guid companyId,
        Guid? warehouseId,
        Guid? materialId,
        InventoryTransactionType? type,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<InventoryOperationResult> ApplyAsync(
        InventoryTransaction transaction,
        CancellationToken cancellationToken);

    Task<InventoryOperationResult> TransferAsync(
        InventoryTransaction transferOut,
        InventoryTransaction transferIn,
        CancellationToken cancellationToken);
}

public static class InventoryErrors {
    public static readonly Error MaterialNotFound = new(
        "inventories.material_not_found",
        "Material was not found.",
        404);

    public static readonly Error WarehouseNotFound = new(
        "inventories.warehouse_not_found",
        "An active warehouse was not found.",
        404);

    public static readonly Error InsufficientStock = new(
        "inventories.insufficient_stock",
        "Available inventory is insufficient for this operation.",
        409);

    public static readonly Error SameWarehouseTransfer = new(
        "inventories.same_warehouse_transfer",
        "Source and destination warehouses must be different.",
        422);
}
