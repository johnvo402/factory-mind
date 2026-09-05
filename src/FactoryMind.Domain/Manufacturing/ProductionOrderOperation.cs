using FactoryMind.Domain.Identity;

namespace FactoryMind.Domain.Manufacturing;

public sealed class ProductionOrderOperation {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }
    public Guid ProductionOrderId { get; set; }
    public ProductionOrder? ProductionOrder { get; set; }
    public Guid? RoutingOperationId { get; set; }
    public RoutingOperation? RoutingOperation { get; set; }
    public int Sequence { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid WorkCenterId { get; set; }
    public WorkCenter? WorkCenter { get; set; }
    public string WorkCenterCode { get; set; } = string.Empty;
    public string WorkCenterName { get; set; } = string.Empty;
    public int SetupTimeMinutes { get; set; }
    public int RunTimeMinutes { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; } = ProductionOperationStatuses.Pending;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class ProductionOperationStatuses {
    public const string Pending = "pending";
    public const string InProgress = "in_progress";
    public const string Completed = "completed";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) {
        Pending,
        InProgress,
        Completed
    };
}
