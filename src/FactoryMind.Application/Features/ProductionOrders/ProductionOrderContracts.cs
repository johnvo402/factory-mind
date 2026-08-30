using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;

namespace FactoryMind.Application.Features.ProductionOrders;

public static class ProductionOrderConstraints {
    public const int MaximumNumberLength = 50;
    public const int MaximumStatusLength = 30;
    public const int QuantityPrecision = 18;
    public const int QuantityScale = 3;
}

public sealed record ProductionOrderResponse(
    Guid Id,
    string Number,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    decimal Quantity,
    string Status,
    Guid? BillOfMaterialId,
    int? BomRevision,
    DateTime? ReleasedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    DateTime? CancelledAt,
    DateTime CreatedAt,
    DateTime UpdatedAt) {
    public static ProductionOrderResponse From(ProductionOrder order) => new(
        order.Id,
        order.Number,
        order.ProductId,
        order.Product?.Code ?? string.Empty,
        order.Product?.Name ?? string.Empty,
        order.Quantity,
        order.Status,
        order.BillOfMaterialId,
        order.BillOfMaterial?.Revision,
        order.ReleasedAt,
        order.StartedAt,
        order.CompletedAt,
        order.CancelledAt,
        order.CreatedAt,
        order.UpdatedAt);
}

public interface IProductionOrderRepository {
    Task<IReadOnlyList<ProductionOrder>> GetByCompanyAsync(
        Guid companyId,
        string? search,
        CancellationToken cancellationToken);

    Task<ProductionOrder?> GetByIdAsync(
        Guid productionOrderId,
        Guid companyId,
        CancellationToken cancellationToken);

    Task<bool> NumberExistsAsync(
        Guid companyId,
        string number,
        Guid? excludedProductionOrderId,
        CancellationToken cancellationToken);

    Task<bool> TryUpdatePlannedAsync(
        ProductionOrder order,
        CancellationToken cancellationToken);

    Task<bool> TryDeletePlannedAsync(
        Guid productionOrderId,
        Guid companyId,
        CancellationToken cancellationToken);

    void Add(ProductionOrder order);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed record ProductionMaterialAllocation(
    Guid MaterialId,
    Guid WarehouseId,
    decimal Quantity);

public sealed record ProductionExecutionResult(
    ProductionExecutionStatus Status,
    ProductionOrder? Order);

public enum ProductionExecutionStatus {
    Success,
    StateConflict,
    ActiveBomNotFound,
    InsufficientStock,
    WarehouseUnavailable,
    MaterialUnavailable,
    ProductUnavailable
}

public interface IProductionExecutionRepository {
    Task<ProductionOrder?> GetAsync(
        Guid productionOrderId,
        Guid companyId,
        CancellationToken cancellationToken);

    Task<ProductionExecutionResult> TryReleaseAsync(
        Guid productionOrderId,
        Guid companyId,
        DateTime releasedAt,
        CancellationToken cancellationToken);

    Task<ProductionExecutionResult> TryStartAsync(
        Guid productionOrderId,
        Guid companyId,
        IReadOnlyList<InventoryTransaction> consumptionTransactions,
        DateTime startedAt,
        CancellationToken cancellationToken);

    Task<ProductionExecutionResult> TryCancelAsync(
        Guid productionOrderId,
        Guid companyId,
        DateTime cancelledAt,
        CancellationToken cancellationToken);

    Task<ProductionExecutionResult> TryCompleteAsync(
        Guid productionOrderId,
        Guid companyId,
        ProductInventoryTransaction outputTransaction,
        DateTime completedAt,
        CancellationToken cancellationToken);
}

public static class ProductionOrderErrors {
    public static readonly Error NotFound = new(
        "production_orders.not_found",
        "Production order was not found.",
        404);

    public static readonly Error ProductNotFound = new(
        "production_orders.product_not_found",
        "Product was not found.",
        404);

    public static readonly Error NumberAlreadyExists = new(
        "production_orders.number_already_exists",
        "A production order with this number already exists.",
        409);

    public static readonly Error PlannedRequired = new(
        "production_orders.planned_required",
        "Only a planned production order can be changed or deleted.",
        409);

    public static readonly Error InvalidTransition = new(
        "production_orders.invalid_transition",
        "The production order is not in a valid state for this operation.",
        409);

    public static readonly Error LockedBomRequired = new(
        "production_orders.locked_bom_required",
        "The production order does not have a locked bill of materials revision.",
        409);

    public static readonly Error AllocationsRequired = new(
        "production_orders.allocations_required",
        "Allocations are required for every material in the locked bill of materials.",
        409);

    public static readonly Error AllocationQuantityInvalid = new(
        "production_orders.allocation_quantity_invalid",
        "Every material allocation quantity must be greater than zero.",
        400);

    public static readonly Error ExtraAllocationMaterial = new(
        "production_orders.extra_allocation_material",
        "An allocation contains a material that is not required by the locked bill of materials.",
        409);

    public static readonly Error MissingAllocationMaterial = new(
        "production_orders.missing_allocation_material",
        "An allocation is missing for a material required by the locked bill of materials.",
        409);

    public static readonly Error AllocationTotalMismatch = new(
        "production_orders.allocation_total_mismatch",
        "Allocated quantity must exactly match the server-calculated requirement for every material.",
        409);
}
