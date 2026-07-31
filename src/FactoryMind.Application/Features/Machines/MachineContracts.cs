using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;

namespace FactoryMind.Application.Features.Machines;

public static class MachineConstraints {
    public const int MaximumCodeLength = 50;
    public const int MaximumNameLength = 200;
    public const int MaximumSearchLength = 200;
}

public sealed record MachineResponse(
    Guid Id,
    string Code,
    string Name,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt) {
    public static MachineResponse From(Machine machine) => new(
        machine.Id,
        machine.Code,
        machine.Name,
        machine.Status,
        machine.CreatedAt,
        machine.UpdatedAt);
}

public interface IMachineRepository {
    Task<IReadOnlyList<Machine>> GetByCompanyAsync(
        Guid companyId,
        string? search,
        CancellationToken cancellationToken);

    Task<Machine?> GetByIdAsync(
        Guid machineId,
        Guid companyId,
        CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(
        Guid companyId,
        string code,
        Guid? excludedMachineId,
        CancellationToken cancellationToken);

    void Add(Machine machine);
    void Remove(Machine machine);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public static class MachineErrors {
    public static readonly Error NotFound = new(
        "machines.not_found",
        "Machine was not found.",
        404);

    public static readonly Error CodeAlreadyExists = new(
        "machines.code_already_exists",
        "A machine with this code already exists.",
        409);
}
