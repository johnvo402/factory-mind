using FactoryMind.Domain.Identity;

namespace FactoryMind.Domain.Manufacturing;

public sealed class InventoryTransaction {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }
    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public Guid MaterialId { get; set; }
    public Material? Material { get; set; }
    public InventoryTransactionType Type { get; set; }
    public decimal Quantity { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? Note { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public decimal SignedQuantity() => Type.ToSignedQuantity(Quantity);
}

public enum InventoryTransactionType {
    Receipt,
    Issue,
    AdjustmentIncrease,
    AdjustmentDecrease,
    TransferIn,
    TransferOut,
    ProductionConsume
}

public static class InventoryTransactionTypeExtensions {
    public static decimal ToSignedQuantity(this InventoryTransactionType type, decimal quantity) => type switch {
        InventoryTransactionType.Receipt or
        InventoryTransactionType.AdjustmentIncrease or
        InventoryTransactionType.TransferIn => quantity,
        InventoryTransactionType.Issue or
        InventoryTransactionType.AdjustmentDecrease or
        InventoryTransactionType.TransferOut or
        InventoryTransactionType.ProductionConsume => -quantity,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown inventory transaction type.")
    };
}

public enum InventoryAdjustmentDirection {
    Increase,
    Decrease
}
