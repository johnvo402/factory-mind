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
    Guid? WorkCenterId,
    string? WorkCenterCode,
    string? WorkCenterName,
    DateTime CreatedAt,
    DateTime UpdatedAt) {
    public static MachineResponse From(Machine machine) => new(
        machine.Id,
        machine.Code,
        machine.Name,
        machine.Status,
        machine.WorkCenterId,
        machine.WorkCenter?.Code,
        machine.WorkCenter?.Name,
        machine.CreatedAt,
        machine.UpdatedAt);
}

public enum MachineUpdateStatus {
    Success,
    NotFound,
    CodeAlreadyExists,
    WorkCenterNotFound,
    WorkCenterInactive,
    ActiveExecution
}

public sealed record MachineUpdateResult(MachineUpdateStatus Status, Machine? Machine);

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

    Task<bool> HasActiveOperationAsync(
        Guid machineId,
        Guid companyId,
        CancellationToken cancellationToken);

    Task<bool> HasOperationReferenceAsync(
        Guid machineId,
        Guid companyId,
        CancellationToken cancellationToken);

    Task<MachineUpdateResult> TryUpdateAsync(
        Guid machineId,
        Guid companyId,
        string code,
        string name,
        string status,
        Guid? workCenterId,
        DateTime updatedAt,
        CancellationToken cancellationToken);

    Task<bool> TryDeleteAsync(Machine machine, CancellationToken cancellationToken);

    void Add(Machine machine);
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

    public static readonly Error WorkCenterNotFound = new(
        "machines.work_center_not_found",
        "Work center was not found.",
        404);

    public static readonly Error WorkCenterInactive = new(
        "machines.work_center_inactive",
        "Machine work center must be active.",
        409);

    public static readonly Error RunningIsSystemManaged = new(
        "machines.running_is_system_managed",
        "Running status is controlled by production operation execution.",
        400);

    public static readonly Error ActiveExecution = new(
        "machines.active_execution",
        "Machine cannot be changed or deleted while executing an operation.",
        409);

    public static readonly Error HistoryProtected = new(
        "machines.history_protected",
        "Machine cannot be deleted because it is referenced by production history.",
        409);
}
