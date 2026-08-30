using FactoryMind.Domain.Identity;

namespace FactoryMind.Domain.Manufacturing;

public sealed class ProductInventoryTransaction {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }
    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public ProductInventoryTransactionType Type { get; set; }
    public decimal Quantity { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? Note { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public decimal SignedQuantity() => Type.ToSignedQuantity(Quantity);
}

public enum ProductInventoryTransactionType {
    ProductionOutput
}

public static class ProductInventoryTransactionTypeExtensions {
    public static decimal ToSignedQuantity(
        this ProductInventoryTransactionType type,
        decimal quantity) => type switch {
        ProductInventoryTransactionType.ProductionOutput => quantity,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown product inventory transaction type.")
    };
}
