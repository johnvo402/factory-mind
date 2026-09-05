using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;

namespace FactoryMind.Application.Features.Routings;

public static class RoutingConstraints {
    public const int MaximumStatusLength = 20;
    public const int MaximumOperationNameLength = 200;
    public const int MaximumOperationDescriptionLength = 500;
}

public sealed record RoutingOperationDefinition(
    int Sequence,
    string Name,
    Guid WorkCenterId,
    int SetupTimeMinutes,
    int RunTimeMinutes,
    string? Description);

public sealed record RoutingOperationResponse(
    Guid Id,
    int Sequence,
    string Name,
    Guid WorkCenterId,
    string WorkCenterCode,
    string WorkCenterName,
    int SetupTimeMinutes,
    int RunTimeMinutes,
    string? Description) {
    public static RoutingOperationResponse From(RoutingOperation operation) => new(
        operation.Id,
        operation.Sequence,
        operation.Name,
        operation.WorkCenterId,
        operation.WorkCenter?.Code ?? string.Empty,
        operation.WorkCenter?.Name ?? string.Empty,
        operation.SetupTimeMinutes,
        operation.RunTimeMinutes,
        operation.Description);
}

public sealed record RoutingResponse(
    Guid Id,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    int Revision,
    string Status,
    IReadOnlyList<RoutingOperationResponse> Operations,
    DateTime CreatedAt,
    DateTime UpdatedAt) {
    public static RoutingResponse From(Routing routing) => new(
        routing.Id,
        routing.ProductId,
        routing.Product?.Code ?? string.Empty,
        routing.Product?.Name ?? string.Empty,
        routing.Revision,
        routing.Status,
        routing.Operations.OrderBy(operation => operation.Sequence)
            .Select(RoutingOperationResponse.From).ToList(),
        routing.CreatedAt,
        routing.UpdatedAt);
}

public enum RoutingActivationStatus {
    Success,
    NotFound,
    StateConflict,
    OperationsRequired,
    InvalidSequence,
    InvalidTiming,
    WorkCenterUnavailable
}

public sealed record RoutingActivationResult(RoutingActivationStatus Status, Routing? Routing);

public interface IRoutingRepository {
    Task<IReadOnlyList<Routing>> GetByProductAsync(
        Guid productId, Guid companyId, CancellationToken cancellationToken);
    Task<Routing?> GetByIdAsync(
        Guid routingId, Guid productId, Guid companyId, CancellationToken cancellationToken);
    Task<int> GetNextRevisionAsync(
        Guid productId, Guid companyId, CancellationToken cancellationToken);
    void Add(Routing routing);
    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task<Routing?> ReplaceDraftOperationsAsync(
        Routing routing,
        IReadOnlyList<RoutingOperation> operations,
        DateTime updatedAt,
        CancellationToken cancellationToken);
    Task<RoutingActivationResult> TryActivateAsync(
        Guid routingId,
        Guid productId,
        Guid companyId,
        DateTime activatedAt,
        CancellationToken cancellationToken);
}

public static class RoutingErrors {
    public static readonly Error NotFound = new("routings.not_found", "Routing was not found.", 404);
    public static readonly Error ActiveNotFound = new(
        "routings.active_not_found", "The product does not have an active routing.", 409);
    public static readonly Error DraftRequired = new(
        "routings.draft_required", "Only a draft routing can be changed or activated.", 409);
    public static readonly Error OperationsRequired = new(
        "routings.operations_required", "A routing must contain at least one operation before activation.", 409);
    public static readonly Error SequenceInvalid = new(
        "routings.sequence_invalid", "Every operation sequence must be positive and unique.", 409);
    public static readonly Error TimingInvalid = new(
        "routings.timing_invalid", "Operation setup and run time must be zero or greater.", 400);
    public static readonly Error OperationNameRequired = new(
        "routings.operation_name_required", "Every routing operation requires a name.", 400);
    public static readonly Error WorkCenterNotFound = new(
        "routings.work_center_not_found", "Work center was not found.", 404);
    public static readonly Error WorkCenterInactive = new(
        "routings.work_center_inactive", "Every routing operation must use an active work center.", 409);
}

public static class RoutingSpecificationValidation {
    public static Error? Validate(IReadOnlyList<RoutingOperationDefinition> operations) {
        if (operations.Any(operation => operation.Sequence <= 0) ||
            operations.Select(operation => operation.Sequence).Distinct().Count() != operations.Count) {
            return RoutingErrors.SequenceInvalid;
        }
        if (operations.Any(operation => string.IsNullOrWhiteSpace(operation.Name))) {
            return RoutingErrors.OperationNameRequired;
        }
        return operations.Any(operation => operation.SetupTimeMinutes < 0 || operation.RunTimeMinutes < 0)
            ? RoutingErrors.TimingInvalid
            : null;
    }
}
