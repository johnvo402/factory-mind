using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.Inventories;
using FactoryMind.Application.Features.Inventories.CreateInventory;
using FactoryMind.Application.Features.Materials;
using FactoryMind.Domain.Manufacturing;

namespace FactoryMind.Tests;

public sealed class InventoryCommandHandlerTests {
    [Fact]
    public async Task Create_uses_current_company_material_and_trims_warehouse() {
        var currentUser = new FakeCurrentUser();
        var materialRepository = new FakeMaterialRepository();
        var inventoryRepository = new FakeInventoryRepository();
        var material = new Material {
            CompanyId = currentUser.CompanyId,
            Code = "MAT-PP",
            Name = "Polypropylene Resin",
            Unit = "kg"
        };
        materialRepository.Materials.Add(material);
        var handler = new CreateInventoryCommandHandler(
            inventoryRepository,
            materialRepository,
            currentUser);

        var result = await handler.Handle(
            new CreateInventoryCommand(material.Id, " Main Warehouse ", 1200.125m),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var inventory = Assert.Single(inventoryRepository.Inventories);
        Assert.Equal(currentUser.CompanyId, inventory.CompanyId);
        Assert.Equal(material.Id, inventory.MaterialId);
        Assert.Equal("Main Warehouse", inventory.Warehouse);
        Assert.Equal(1200.125m, inventory.Quantity);
        Assert.Equal("MAT-PP", result.Value?.MaterialCode);
    }

    [Fact]
    public async Task Create_rejects_material_from_another_company() {
        var currentUser = new FakeCurrentUser();
        var materialRepository = new FakeMaterialRepository();
        var inventoryRepository = new FakeInventoryRepository();
        var material = new Material { CompanyId = Guid.NewGuid(), Code = "MAT-X" };
        materialRepository.Materials.Add(material);
        var handler = new CreateInventoryCommandHandler(
            inventoryRepository,
            materialRepository,
            currentUser);

        var result = await handler.Handle(
            new CreateInventoryCommand(material.Id, "Main Warehouse", 10m),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("inventories.material_not_found", result.Error?.Code);
        Assert.Empty(inventoryRepository.Inventories);
    }

    private sealed class FakeCurrentUser : ICurrentUser {
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid CompanyId { get; } = Guid.NewGuid();
        public string Role => "Admin";
    }

    private sealed class FakeInventoryRepository : IInventoryRepository {
        public List<Inventory> Inventories { get; } = [];

        public Task<IReadOnlyList<Inventory>> GetByCompanyAsync(
            Guid companyId,
            string? search,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Inventory>>(
                Inventories.Where(inventory => inventory.CompanyId == companyId).ToList());

        public Task<Inventory?> GetByIdAsync(
            Guid inventoryId,
            Guid companyId,
            CancellationToken cancellationToken) => Task.FromResult(Inventories.SingleOrDefault(inventory =>
                inventory.Id == inventoryId && inventory.CompanyId == companyId));

        public Task<bool> EntryExistsAsync(
            Guid companyId,
            Guid materialId,
            string warehouse,
            Guid? excludedInventoryId,
            CancellationToken cancellationToken) => Task.FromResult(Inventories.Any(inventory =>
                inventory.CompanyId == companyId &&
                inventory.MaterialId == materialId &&
                inventory.Warehouse == warehouse &&
                (!excludedInventoryId.HasValue || inventory.Id != excludedInventoryId.Value)));

        public void Add(Inventory inventory) => Inventories.Add(inventory);
        public void Remove(Inventory inventory) => Inventories.Remove(inventory);
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

        public void Add(Material material) => Materials.Add(material);
        public void Remove(Material material) => Materials.Remove(material);
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
