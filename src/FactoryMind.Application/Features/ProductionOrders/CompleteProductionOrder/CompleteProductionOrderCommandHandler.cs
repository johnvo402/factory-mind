using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.Inventories;
using FactoryMind.Application.Features.Products;
using FactoryMind.Application.Features.Warehouses;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.ProductionOrders.CompleteProductionOrder;

public sealed class CompleteProductionOrderCommandHandler(
    IProductionExecutionRepository executionRepository,
    IProductRepository productRepository,
    IWarehouseRepository warehouseRepository,
    ICurrentUser currentUser)
    : IRequestHandler<CompleteProductionOrderCommand, Result<ProductionOrderResponse>> {
    public async ValueTask<Result<ProductionOrderResponse>> Handle(
        CompleteProductionOrderCommand command,
        CancellationToken cancellationToken) {
        var order = await executionRepository.GetAsync(
            command.ProductionOrderId,
            currentUser.CompanyId,
            cancellationToken);
        if (order is null) {
            return Result<ProductionOrderResponse>.Failure(ProductionOrderErrors.NotFound);
        }
        if (order.Status != ProductionOrderStatuses.InProgress ||
            order.BillOfMaterialId is null ||
            order.StartedAt is null ||
            order.Quantity <= 0) {
            return Result<ProductionOrderResponse>.Failure(ProductionOrderErrors.InvalidTransition);
        }

        var product = await productRepository.GetByIdAsync(
            order.ProductId,
            currentUser.CompanyId,
            cancellationToken);
        if (product is null) {
            return Result<ProductionOrderResponse>.Failure(ProductionOrderErrors.ProductNotFound);
        }

        var warehouse = await warehouseRepository.GetByIdAsync(
            command.WarehouseId,
            currentUser.CompanyId,
            cancellationToken);
        if (warehouse is not { IsActive: true }) {
            return Result<ProductionOrderResponse>.Failure(InventoryErrors.WarehouseNotFound);
        }

        var completedAt = DateTime.UtcNow;
        var output = new ProductInventoryTransaction {
            CompanyId = currentUser.CompanyId,
            WarehouseId = warehouse.Id,
            Warehouse = warehouse,
            ProductId = product.Id,
            Product = product,
            Type = ProductInventoryTransactionType.ProductionOutput,
            Quantity = order.Quantity,
            ReferenceType = "ProductionOrder",
            ReferenceId = order.Id,
            Note = "Production order output.",
            CreatedByUserId = currentUser.UserId,
            CreatedAt = completedAt
        };

        var outcome = await executionRepository.TryCompleteAsync(
            order.Id,
            currentUser.CompanyId,
            output,
            completedAt,
            cancellationToken);
        return outcome.Status switch {
            ProductionExecutionStatus.Success => Result<ProductionOrderResponse>.Success(
                ProductionOrderResponse.From(outcome.Order!)),
            ProductionExecutionStatus.WarehouseUnavailable =>
                Result<ProductionOrderResponse>.Failure(InventoryErrors.WarehouseNotFound),
            ProductionExecutionStatus.ProductUnavailable =>
                Result<ProductionOrderResponse>.Failure(ProductionOrderErrors.ProductNotFound),
            _ => Result<ProductionOrderResponse>.Failure(ProductionOrderErrors.InvalidTransition)
        };
    }
}
