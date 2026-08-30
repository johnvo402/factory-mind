using FactoryMind.Domain.Manufacturing;

namespace FactoryMind.Application.Features.ProductInventories;

public static class ProductInventoryConstraints {
    public const int QuantityPrecision = 18;
    public const int QuantityScale = 3;
    public const int MaximumReferenceTypeLength = 100;
    public const int MaximumNoteLength = 500;
    public const int MaximumSearchLength = 200;
    public const int MaximumPageSize = 100;
}

public sealed record ProductInventoryBalanceResponse(
    Guid Id,
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    decimal Quantity,
    DateTime UpdatedAt) {
    public static ProductInventoryBalanceResponse From(ProductInventoryBalance balance) => new(
        balance.Id,
        balance.WarehouseId,
        balance.Warehouse?.Code ?? string.Empty,
        balance.Warehouse?.Name ?? string.Empty,
        balance.ProductId,
        balance.Product?.Code ?? string.Empty,
        balance.Product?.Name ?? string.Empty,
        balance.Quantity,
        balance.UpdatedAt);
}

public sealed record ProductInventoryTransactionResponse(
    Guid Id,
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    ProductInventoryTransactionType Type,
    decimal Quantity,
    decimal SignedQuantity,
    string? ReferenceType,
    Guid? ReferenceId,
    string? Note,
    DateTime CreatedAt) {
    public static ProductInventoryTransactionResponse From(ProductInventoryTransaction transaction) => new(
        transaction.Id,
        transaction.WarehouseId,
        transaction.Warehouse?.Code ?? string.Empty,
        transaction.Warehouse?.Name ?? string.Empty,
        transaction.ProductId,
        transaction.Product?.Code ?? string.Empty,
        transaction.Product?.Name ?? string.Empty,
        transaction.Type,
        transaction.Quantity,
        transaction.SignedQuantity(),
        transaction.ReferenceType,
        transaction.ReferenceId,
        transaction.Note,
        transaction.CreatedAt);
}

public sealed record ProductInventoryTransactionPageResponse(
    IReadOnlyList<ProductInventoryTransactionResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

public interface IProductInventoryRepository {
    Task<IReadOnlyList<ProductInventoryBalance>> GetBalancesAsync(
        Guid companyId,
        Guid? warehouseId,
        Guid? productId,
        string? search,
        CancellationToken cancellationToken);

    Task<(IReadOnlyList<ProductInventoryTransaction> Items, int TotalCount)> GetTransactionsAsync(
        Guid companyId,
        Guid? warehouseId,
        Guid? productId,
        ProductInventoryTransactionType? type,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
