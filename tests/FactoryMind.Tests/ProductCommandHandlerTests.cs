using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.Products;
using FactoryMind.Application.Features.Products.CreateProduct;
using FactoryMind.Application.Features.Products.DeleteProduct;
using FactoryMind.Domain.Manufacturing;

namespace FactoryMind.Tests;

public sealed class ProductCommandHandlerTests {
    [Fact]
    public async Task Create_rejects_duplicate_code_within_current_company() {
        var currentUser = new FakeCurrentUser();
        var repository = new FakeProductRepository();
        repository.Products.Add(new Product {
            CompanyId = currentUser.CompanyId,
            Code = "PRD-001",
            Name = "Existing product"
        });
        var handler = new CreateProductCommandHandler(repository, currentUser);

        var result = await handler.Handle(
            new CreateProductCommand("prd-001", "Duplicate"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("products.code_already_exists", result.Error?.Code);
        Assert.Single(repository.Products);
    }

    [Fact]
    public async Task Delete_removes_current_company_product() {
        var currentUser = new FakeCurrentUser();
        var repository = new FakeProductRepository();
        var product = new Product {
            CompanyId = currentUser.CompanyId,
            Code = "PRD-001",
            Name = "Storage box"
        };
        repository.Products.Add(product);
        var handler = new DeleteProductCommandHandler(repository, currentUser);

        var result = await handler.Handle(new DeleteProductCommand(product.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(repository.Products);
        Assert.Equal(1, repository.SaveChangesCount);
    }

    private sealed class FakeCurrentUser : ICurrentUser {
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid CompanyId { get; } = Guid.NewGuid();
        public string Role => "Admin";
    }

    private sealed class FakeProductRepository : IProductRepository {
        public List<Product> Products { get; } = [];
        public int SaveChangesCount { get; private set; }

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
            CancellationToken cancellationToken) => Task.FromResult(Products.Any(product =>
                product.CompanyId == companyId &&
                product.Code == code &&
                (!excludedProductId.HasValue || product.Id != excludedProductId.Value)));

        public Task<bool> HasBillOfMaterialsAsync(
            Guid productId,
            Guid companyId,
            CancellationToken cancellationToken) => Task.FromResult(false);

        public void Add(Product product) => Products.Add(product);
        public void Remove(Product product) => Products.Remove(product);
        public Task SaveChangesAsync(CancellationToken cancellationToken) {
            SaveChangesCount++;
            return Task.CompletedTask;
        }
    }
}
