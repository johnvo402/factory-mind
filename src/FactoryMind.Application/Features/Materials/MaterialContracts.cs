using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;

namespace FactoryMind.Application.Features.Materials;

public static class MaterialConstraints {
    public const int MaximumCodeLength = 50;
    public const int MaximumNameLength = 200;
    public const int MaximumUnitLength = 30;
    public const int MaximumSearchLength = 200;
}

public sealed record MaterialResponse(
    Guid Id,
    string Code,
    string Name,
    string Unit,
    DateTime CreatedAt,
    DateTime UpdatedAt) {
    public static MaterialResponse From(Material material) => new(
        material.Id,
        material.Code,
        material.Name,
        material.Unit,
        material.CreatedAt,
        material.UpdatedAt);
}

public interface IMaterialRepository {
    Task<IReadOnlyList<Material>> GetByCompanyAsync(
        Guid companyId,
        string? search,
        CancellationToken cancellationToken);

    Task<Material?> GetByIdAsync(
        Guid materialId,
        Guid companyId,
        CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(
        Guid companyId,
        string code,
        Guid? excludedMaterialId,
        CancellationToken cancellationToken);

    void Add(Material material);
    void Remove(Material material);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public static class MaterialErrors {
    public static readonly Error NotFound = new(
        "materials.not_found",
        "Material was not found.",
        404);

    public static readonly Error CodeAlreadyExists = new(
        "materials.code_already_exists",
        "A material with this code already exists.",
        409);
}
