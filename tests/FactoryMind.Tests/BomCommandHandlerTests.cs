using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.Boms;
using FactoryMind.Application.Features.Boms.ActivateBom;
using FactoryMind.Application.Features.Boms.CalculateMaterialRequirements;
using FactoryMind.Application.Features.Boms.CreateBom;
using FactoryMind.Application.Features.Materials;
using FactoryMind.Application.Features.Products;
using FactoryMind.Domain.Manufacturing;

namespace FactoryMind.Tests;

public sealed class BomCommandHandlerTests {
    [Theory]
    [InlineData(0, 1, "boms.output_quantity_invalid")]
    [InlineData(1, 0, "boms.item_quantity_invalid")]
    public async Task Create_rejects_non_positive_quantities(
        decimal outputQuantity,
        decimal itemQuantity,
        string expectedCode) {
        var context = new BomTestContext();
        var handler = context.CreateHandler();

        var result = await handler.Handle(new CreateBomCommand(
            context.Product.Id,
            outputQuantity,
            [new BomItemDefinition(context.Material.Id, itemQuantity, null)]), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedCode, result.Error?.Code);
        Assert.Empty(context.Boms.Boms);
    }

    [Fact]
    public async Task Create_rejects_duplicate_material() {
        var context = new BomTestContext();

        var result = await context.CreateHandler().Handle(new CreateBomCommand(
            context.Product.Id,
            1,
            [
                new BomItemDefinition(context.Material.Id, 1, null),
                new BomItemDefinition(context.Material.Id, 2, 5)
            ]), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("boms.duplicate_material", result.Error?.Code);
    }

    [Fact]
    public async Task Create_rejects_product_from_another_company() {
        var context = new BomTestContext();
        var foreignProduct = new Product {
            CompanyId = Guid.NewGuid(),
            Code = "OTHER",
            Name = "Other tenant product"
        };
        context.Products.Products.Add(foreignProduct);

        var result = await context.CreateHandler().Handle(new CreateBomCommand(
            foreignProduct.Id,
            1,
            []), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("products.not_found", result.Error?.Code);
        Assert.Empty(context.Boms.Boms);
    }

    [Fact]
    public async Task Create_rejects_material_from_another_company() {
        var context = new BomTestContext();
        var foreignMaterial = new Material {
            CompanyId = Guid.NewGuid(),
            Code = "OTHER",
            Name = "Other tenant material",
            Unit = "kg"
        };
        context.Materials.Materials.Add(foreignMaterial);

        var result = await context.CreateHandler().Handle(new CreateBomCommand(
            context.Product.Id,
            1,
            [new BomItemDefinition(foreignMaterial.Id, 1, null)]), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("boms.material_not_found", result.Error?.Code);
        Assert.Empty(context.Boms.Boms);
    }

    [Fact]
    public async Task Activation_archives_previous_active_revision() {
        var context = new BomTestContext();
        var active = context.AddBom(1, BillOfMaterialStatuses.Active);
        var draft = context.AddBom(2, BillOfMaterialStatuses.Draft);
        var handler = new ActivateBomCommandHandler(context.Boms, context.User);

        var result = await handler.Handle(
            new ActivateBomCommand(context.Product.Id, draft.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BillOfMaterialStatuses.Archived, active.Status);
        Assert.Equal(BillOfMaterialStatuses.Active, draft.Status);
        Assert.Single(context.Boms.Boms, bom => bom.Status == BillOfMaterialStatuses.Active);
    }

    [Fact]
    public async Task Material_requirements_report_missing_active_bom() {
        var context = new BomTestContext();
        context.AddBom(1, BillOfMaterialStatuses.Draft);
        var handler = new GetProductMaterialRequirementsQueryHandler(
            context.Boms,
            context.Products,
            new MaterialRequirementCalculator(),
            context.User);

        var result = await handler.Handle(
            new GetProductMaterialRequirementsQuery(context.Product.Id, 10),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("boms.active_not_found", result.Error?.Code);
    }

    private sealed class BomTestContext {
        public FakeCurrentUser User { get; } = new();
        public FakeBomRepository Boms { get; } = new();
        public FakeProductRepository Products { get; } = new();
        public FakeMaterialRepository Materials { get; } = new();
        public Product Product { get; }
        public Material Material { get; }

        public BomTestContext() {
            Product = new Product {
                CompanyId = User.CompanyId,
                Code = "PRD-001",
                Name = "Industrial table"
            };
            Material = new Material {
                CompanyId = User.CompanyId,
                Code = "MAT-STEEL",
                Name = "Steel",
                Unit = "kg"
            };
            Products.Products.Add(Product);
            Materials.Materials.Add(Material);
        }

        public CreateBomCommandHandler CreateHandler() => new(Boms, Products, Materials, User);

        public BillOfMaterial AddBom(int revision, string status) {
            var bom = new BillOfMaterial {
                CompanyId = User.CompanyId,
                ProductId = Product.Id,
                Product = Product,
                Revision = revision,
                OutputQuantity = 1,
                Status = status,
                Items = [new BomItem {
                    MaterialId = Material.Id,
                    Material = Material,
                    Quantity = 1
                }]
            };
            Boms.Boms.Add(bom);
            return bom;
        }
    }

    private sealed class FakeCurrentUser : ICurrentUser {
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid CompanyId { get; } = Guid.NewGuid();
        public string Role => "Admin";
    }

    private sealed class FakeBomRepository : IBomRepository {
        public List<BillOfMaterial> Boms { get; } = [];
        public Dictionary<Guid, decimal> Availability { get; } = [];

        public Task<IReadOnlyList<BillOfMaterial>> GetByProductAsync(
            Guid productId,
            Guid companyId,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BillOfMaterial>>(
                Boms.Where(bom => bom.ProductId == productId && bom.CompanyId == companyId).ToList());

        public Task<BillOfMaterial?> GetByIdAsync(
            Guid bomId,
            Guid productId,
            Guid companyId,
            CancellationToken cancellationToken) => Task.FromResult(Boms.SingleOrDefault(bom =>
                bom.Id == bomId && bom.ProductId == productId && bom.CompanyId == companyId));

        public Task<BillOfMaterial?> GetActiveAsync(
            Guid productId,
            Guid companyId,
            CancellationToken cancellationToken) => Task.FromResult(Boms.SingleOrDefault(bom =>
                bom.ProductId == productId &&
                bom.CompanyId == companyId &&
                bom.Status == BillOfMaterialStatuses.Active));

        public Task<int> GetNextRevisionAsync(
            Guid productId,
            Guid companyId,
            CancellationToken cancellationToken) => Task.FromResult(
                Boms.Where(bom => bom.ProductId == productId && bom.CompanyId == companyId)
                    .Select(bom => bom.Revision)
                    .DefaultIfEmpty()
                    .Max() + 1);

        public Task<IReadOnlyDictionary<Guid, decimal>> GetAvailableQuantitiesAsync(
            Guid companyId,
            IReadOnlyCollection<Guid> materialIds,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyDictionary<Guid, decimal>>(
                Availability.Where(pair => materialIds.Contains(pair.Key))
                    .ToDictionary(pair => pair.Key, pair => pair.Value));

        public void Add(BillOfMaterial bom) => Boms.Add(bom);
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ActivateAsync(
            BillOfMaterial bom,
            DateTime activatedAt,
            CancellationToken cancellationToken) {
            foreach (var active in Boms.Where(candidate =>
                         candidate.CompanyId == bom.CompanyId &&
                         candidate.ProductId == bom.ProductId &&
                         candidate.Status == BillOfMaterialStatuses.Active)) {
                active.Status = BillOfMaterialStatuses.Archived;
            }
            bom.Status = BillOfMaterialStatuses.Active;
            bom.UpdatedAt = activatedAt;
            return Task.CompletedTask;
        }
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

    private sealed class FakeMaterialRepository : IMaterialRepository {
        public List<Material> Materials { get; } = [];

        public Task<IReadOnlyList<Material>> GetByCompanyAsync(
            Guid companyId,
            string? search,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Material>>(
                Materials.Where(material => material.CompanyId == companyId).ToList());

        public Task<Material?> GetByIdAsync(
            Guid materialId,
            Guid companyId,
            CancellationToken cancellationToken) => Task.FromResult(Materials.SingleOrDefault(material =>
                material.Id == materialId && material.CompanyId == companyId));

        public Task<bool> CodeExistsAsync(
            Guid companyId,
            string code,
            Guid? excludedMaterialId,
            CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<bool> HasBomItemsAsync(
            Guid materialId,
            Guid companyId,
            CancellationToken cancellationToken) => Task.FromResult(false);

        public void Add(Material material) => Materials.Add(material);
        public void Remove(Material material) => Materials.Remove(material);
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
