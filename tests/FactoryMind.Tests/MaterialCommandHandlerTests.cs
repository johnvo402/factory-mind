using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.Materials;
using FactoryMind.Application.Features.Materials.CreateMaterial;
using FactoryMind.Application.Features.Materials.UpdateMaterial;
using FactoryMind.Domain.Manufacturing;

namespace FactoryMind.Tests;

public sealed class MaterialCommandHandlerTests {
    [Fact]
    public async Task Create_normalizes_material_and_uses_current_company() {
        var currentUser = new FakeCurrentUser();
        var repository = new FakeMaterialRepository();
        var handler = new CreateMaterialCommandHandler(repository, currentUser);

        var result = await handler.Handle(
            new CreateMaterialCommand(" mat-pp ", " Polypropylene Resin ", " kg "),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var material = Assert.Single(repository.Materials);
        Assert.Equal(currentUser.CompanyId, material.CompanyId);
        Assert.Equal("MAT-PP", material.Code);
        Assert.Equal("Polypropylene Resin", material.Name);
        Assert.Equal("kg", material.Unit);
    }

    [Fact]
    public async Task Update_cannot_access_material_from_another_company() {
        var currentUser = new FakeCurrentUser();
        var repository = new FakeMaterialRepository();
        var material = new Material {
            CompanyId = Guid.NewGuid(),
            Code = "MAT-PP",
            Name = "Other tenant material",
            Unit = "kg"
        };
        repository.Materials.Add(material);
        var handler = new UpdateMaterialCommandHandler(repository, currentUser);

        var result = await handler.Handle(
            new UpdateMaterialCommand(material.Id, material.Code, material.Name, material.Unit),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("materials.not_found", result.Error?.Code);
        Assert.Equal(0, repository.SaveChangesCount);
    }

    private sealed class FakeCurrentUser : ICurrentUser {
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid CompanyId { get; } = Guid.NewGuid();
        public string Role => "Admin";
    }

    private sealed class FakeMaterialRepository : IMaterialRepository {
        public List<Material> Materials { get; } = [];
        public int SaveChangesCount { get; private set; }

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
            CancellationToken cancellationToken) => Task.FromResult(Materials.Any(material =>
                material.CompanyId == companyId &&
                material.Code == code &&
                (!excludedMaterialId.HasValue || material.Id != excludedMaterialId.Value)));

        public Task<bool> HasBomItemsAsync(
            Guid materialId,
            Guid companyId,
            CancellationToken cancellationToken) => Task.FromResult(false);

        public void Add(Material material) => Materials.Add(material);
        public void Remove(Material material) => Materials.Remove(material);
        public Task SaveChangesAsync(CancellationToken cancellationToken) {
            SaveChangesCount++;
            return Task.CompletedTask;
        }
    }
}
