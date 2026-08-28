using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;

namespace FactoryMind.Application.Features.Warehouses;

public static class WarehouseConstraints {
    public const int MaximumCodeLength = 50;
    public const int MaximumNameLength = 200;
    public const int MaximumDescriptionLength = 500;
    public const int MaximumSearchLength = 200;
}

public sealed record WarehouseResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt) {
    public static WarehouseResponse From(Warehouse warehouse) => new(
        warehouse.Id,
        warehouse.Code,
        warehouse.Name,
        warehouse.Description,
        warehouse.IsActive,
        warehouse.CreatedAt,
        warehouse.UpdatedAt);
}

public interface IWarehouseRepository {
    Task<IReadOnlyList<Warehouse>> GetByCompanyAsync(
        Guid companyId,
        string? search,
        CancellationToken cancellationToken);

    Task<Warehouse?> GetByIdAsync(
        Guid warehouseId,
        Guid companyId,
        CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(
        Guid companyId,
        string code,
        Guid? excludedWarehouseId,
        CancellationToken cancellationToken);

    void Add(Warehouse warehouse);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public static class WarehouseErrors {
    public static readonly Error NotFound = new(
        "warehouses.not_found",
        "Warehouse was not found.",
        404);

    public static readonly Error CodeAlreadyExists = new(
        "warehouses.code_already_exists",
        "A warehouse with this code already exists.",
        409);
}
