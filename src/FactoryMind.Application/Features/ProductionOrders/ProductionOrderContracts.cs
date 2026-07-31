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

    void Add(ProductionOrder order);
    void Remove(ProductionOrder order);
    Task SaveChangesAsync(CancellationToken cancellationToken);
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
}
