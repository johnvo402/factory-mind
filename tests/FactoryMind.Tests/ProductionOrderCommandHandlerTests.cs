using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.ProductionOrders;
using FactoryMind.Application.Features.ProductionOrders.CreateProductionOrder;
using FactoryMind.Application.Features.Products;
using FactoryMind.Domain.Manufacturing;

namespace FactoryMind.Tests;

public sealed class ProductionOrderCommandHandlerTests {
    [Fact]
    public async Task Create_normalizes_number_status_and_uses_current_company_product() {
        var currentUser = new FakeCurrentUser();
        var productRepository = new FakeProductRepository();
        var orderRepository = new FakeProductionOrderRepository();
        var product = new Product {
            CompanyId = currentUser.CompanyId,
            Code = "PRD-001",
            Name = "Storage Box"
        };
        productRepository.Products.Add(product);
        var handler = new CreateProductionOrderCommandHandler(
            orderRepository,
            productRepository,
            currentUser);

        var result = await handler.Handle(
            new CreateProductionOrderCommand(" po-001 ", product.Id, 500.125m, " IN_PROGRESS "),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var order = Assert.Single(orderRepository.Orders);
        Assert.Equal(currentUser.CompanyId, order.CompanyId);
        Assert.Equal("PO-001", order.Number);
        Assert.Equal(ProductionOrderStatuses.InProgress, order.Status);
        Assert.Equal("PRD-001", result.Value?.ProductCode);
    }

    [Fact]
    public async Task Create_rejects_product_from_another_company() {
        var currentUser = new FakeCurrentUser();
        var productRepository = new FakeProductRepository();
        var orderRepository = new FakeProductionOrderRepository();
        var product = new Product { CompanyId = Guid.NewGuid(), Code = "PRD-X" };
        productRepository.Products.Add(product);
        var handler = new CreateProductionOrderCommandHandler(
            orderRepository,
            productRepository,
            currentUser);

        var result = await handler.Handle(
            new CreateProductionOrderCommand("PO-001", product.Id, 100m, "planned"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("production_orders.product_not_found", result.Error?.Code);
        Assert.Empty(orderRepository.Orders);
    }

    private sealed class FakeCurrentUser : ICurrentUser {
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid CompanyId { get; } = Guid.NewGuid();
        public string Role => "Admin";
    }

    private sealed class FakeProductionOrderRepository : IProductionOrderRepository {
        public List<ProductionOrder> Orders { get; } = [];

        public Task<IReadOnlyList<ProductionOrder>> GetByCompanyAsync(
            Guid companyId,
            string? search,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ProductionOrder>>(
                Orders.Where(order => order.CompanyId == companyId).ToList());

        public Task<ProductionOrder?> GetByIdAsync(
            Guid productionOrderId,
            Guid companyId,
            CancellationToken cancellationToken) => Task.FromResult(Orders.SingleOrDefault(order =>
                order.Id == productionOrderId && order.CompanyId == companyId));

        public Task<bool> NumberExistsAsync(
            Guid companyId,
            string number,
            Guid? excludedProductionOrderId,
            CancellationToken cancellationToken) => Task.FromResult(Orders.Any(order =>
                order.CompanyId == companyId &&
                order.Number == number &&
                (!excludedProductionOrderId.HasValue || order.Id != excludedProductionOrderId.Value)));

        public void Add(ProductionOrder order) => Orders.Add(order);
        public void Remove(ProductionOrder order) => Orders.Remove(order);
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeProductRepository : IProductRepository {
        public List<Product> Products { get; } = [];

        public Task<IReadOnlyList<Product>> GetByCompanyAsync(
            Guid companyId,
            string? search,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Product>>(
                Products.Where(product => product.CompanyId == companyId).ToList());

        public Task<Product?> GetByIdAsync(
            Guid productId,
            Guid companyId,
            CancellationToken cancellationToken) => Task.FromResult(Products.SingleOrDefault(product =>
                product.Id == productId && product.CompanyId == companyId));

        public Task<bool> CodeExistsAsync(
            Guid companyId,
            string code,
            Guid? excludedProductId,
            CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<bool> HasBillOfMaterialsAsync(
            Guid productId,
            Guid companyId,
            CancellationToken cancellationToken) => Task.FromResult(false);

        public void Add(Product product) => Products.Add(product);
        public void Remove(Product product) => Products.Remove(product);
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
