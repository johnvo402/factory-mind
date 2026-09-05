using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.Products;
using FactoryMind.Application.Features.ProductionOrders;
using FactoryMind.Application.Features.ProductionOrders.CompleteProductionOrder;
using FactoryMind.Application.Features.Warehouses;
using FactoryMind.Domain.Manufacturing;

namespace FactoryMind.Tests;

public sealed class ProductionCompletionCommandHandlerTests {
    [Fact]
    public async Task Complete_requires_an_InProgress_order() {
        var context = new CompletionTestContext(ProductionOrderStatuses.Planned, warehouseIsActive: true);
        var handler = context.CreateHandler();

        var result = await handler.Handle(
            new CompleteProductionOrderCommand(context.Order.Id, context.Warehouse.Id),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ProductionOrderErrors.InvalidTransition.Code, result.Error?.Code);
        Assert.Equal(0, context.Execution.CompleteCalls);
    }

    [Fact]
    public async Task Complete_rejects_an_inactive_destination_warehouse_before_writing_output() {
        var context = new CompletionTestContext(ProductionOrderStatuses.InProgress, warehouseIsActive: false);
        var handler = context.CreateHandler();

        var result = await handler.Handle(
            new CompleteProductionOrderCommand(context.Order.Id, context.Warehouse.Id),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("inventories.warehouse_not_found", result.Error?.Code);
        Assert.Equal(0, context.Execution.CompleteCalls);
    }

    private sealed class CompletionTestContext {
        public FakeCurrentUser User { get; } = new();
        public ProductionOrder Order { get; }
        public Product Product { get; }
        public Warehouse Warehouse { get; }
        public FakeProductionExecutionRepository Execution { get; }
        public FakeProductRepository Products { get; }
        public FakeWarehouseRepository Warehouses { get; }

        public CompletionTestContext(string orderStatus, bool warehouseIsActive) {
            Product = new Product {
                CompanyId = User.CompanyId,
                Code = "P-COMPLETE",
                Name = "Completion Product"
            };
            Warehouse = new Warehouse {
                CompanyId = User.CompanyId,
                Code = "FG-COMPLETE",
                Name = "Finished Goods",
                IsActive = warehouseIsActive
            };
            Order = new ProductionOrder {
                CompanyId = User.CompanyId,
                ProductId = Product.Id,
                Product = Product,
                BillOfMaterialId = Guid.NewGuid(),
                Quantity = 10m,
                Status = orderStatus,
                StartedAt = DateTime.UtcNow
            };
            Execution = new FakeProductionExecutionRepository(Order);
            Products = new FakeProductRepository(Product);
            Warehouses = new FakeWarehouseRepository(Warehouse);
        }

        public CompleteProductionOrderCommandHandler CreateHandler() => new(
            Execution,
            Products,
            Warehouses,
            User);
    }

    private sealed class FakeCurrentUser : ICurrentUser {
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid CompanyId { get; } = Guid.NewGuid();
        public string Role => "Manager";
    }

    private sealed class FakeProductionExecutionRepository(ProductionOrder order)
        : IProductionExecutionRepository {
        public int CompleteCalls { get; private set; }

        public Task<ProductionOrder?> GetAsync(
            Guid productionOrderId,
            Guid companyId,
            CancellationToken cancellationToken) => Task.FromResult<ProductionOrder?>(
            order.Id == productionOrderId && order.CompanyId == companyId ? order : null);

        public Task<ProductionExecutionResult> TryCompleteAsync(
            Guid productionOrderId,
            Guid companyId,
            ProductInventoryTransaction outputTransaction,
            DateTime completedAt,
            CancellationToken cancellationToken) {
            CompleteCalls++;
            return Task.FromResult(new ProductionExecutionResult(ProductionExecutionStatus.Success, order));
        }

        public Task<ProductionExecutionResult> TryReleaseAsync(
            Guid productionOrderId,
            Guid companyId,
            DateTime releasedAt,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ProductionExecutionResult> TryStartAsync(
            Guid productionOrderId,
            Guid companyId,
            IReadOnlyList<InventoryTransaction> consumptionTransactions,
            DateTime startedAt,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ProductionExecutionResult> TryCancelAsync(
            Guid productionOrderId,
            Guid companyId,
            DateTime cancelledAt,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeProductRepository(Product product) : IProductRepository {
        public Task<Product?> GetByIdAsync(
            Guid productId,
            Guid companyId,
            CancellationToken cancellationToken) => Task.FromResult<Product?>(
            product.Id == productId && product.CompanyId == companyId ? product : null);

        public Task<IReadOnlyList<Product>> GetByCompanyAsync(
            Guid companyId,
            string? search,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> CodeExistsAsync(
            Guid companyId,
            string code,
            Guid? excludedProductId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> HasReferencesAsync(
            Guid productId,
            Guid companyId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public void Add(Product productToAdd) => throw new NotSupportedException();
        public void Remove(Product productToRemove) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeWarehouseRepository(Warehouse warehouse) : IWarehouseRepository {
        public Task<Warehouse?> GetByIdAsync(
            Guid warehouseId,
            Guid companyId,
            CancellationToken cancellationToken) => Task.FromResult<Warehouse?>(
            warehouse.Id == warehouseId && warehouse.CompanyId == companyId ? warehouse : null);

        public Task<IReadOnlyList<Warehouse>> GetByCompanyAsync(
            Guid companyId,
            string? search,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> CodeExistsAsync(
            Guid companyId,
            string code,
            Guid? excludedWarehouseId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public void Add(Warehouse warehouseToAdd) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
