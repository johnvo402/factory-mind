using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;

namespace FactoryMind.Application.Features.WorkCenters;

public static class WorkCenterConstraints {
    public const int MaximumCodeLength = 50;
    public const int MaximumNameLength = 200;
    public const int MaximumDescriptionLength = 500;
    public const int MaximumSearchLength = 200;
}

public sealed record WorkCenterResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt) {
    public static WorkCenterResponse From(WorkCenter workCenter) => new(
        workCenter.Id,
        workCenter.Code,
        workCenter.Name,
        workCenter.Description,
        workCenter.IsActive,
        workCenter.CreatedAt,
        workCenter.UpdatedAt);
}

public interface IWorkCenterRepository {
    Task<IReadOnlyList<WorkCenter>> GetByCompanyAsync(
        Guid companyId,
        string? search,
        CancellationToken cancellationToken);
    Task<WorkCenter?> GetByIdAsync(Guid id, Guid companyId, CancellationToken cancellationToken);
    Task<bool> CodeExistsAsync(
        Guid companyId,
        string code,
        Guid? excludedId,
        CancellationToken cancellationToken);
    void Add(WorkCenter workCenter);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public static class WorkCenterErrors {
    public static readonly Error NotFound = new(
        "work_centers.not_found", "Work center was not found.", 404);
    public static readonly Error CodeAlreadyExists = new(
        "work_centers.code_already_exists", "A work center with this code already exists.", 409);
}
