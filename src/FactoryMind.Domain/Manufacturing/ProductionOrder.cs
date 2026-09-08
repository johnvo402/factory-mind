using FactoryMind.Domain.Identity;

namespace FactoryMind.Domain.Manufacturing;

public sealed class ProductionOrder {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }
    public string Number { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public Guid? BillOfMaterialId { get; set; }
    public BillOfMaterial? BillOfMaterial { get; set; }
    public Guid? RoutingId { get; set; }
    public Routing? Routing { get; set; }
    public decimal Quantity { get; set; }
    public string Status { get; set; } = ProductionOrderStatuses.Planned;
    public DateTime? DueDate { get; set; }
    public string Priority { get; set; } = ProductionOrderPriorities.Normal;
    public DateTime? ReleasedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<ProductionOrderOperation> Operations { get; set; } = [];
}

public static class ProductionOrderPriorities {
    public const string Low = "low";
    public const string Normal = "normal";
    public const string High = "high";
    public const string Urgent = "urgent";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) {
        Low,
        Normal,
        High,
        Urgent
    };
}

public static class ProductionOrderDeliveryStatuses {
    public const string NoDueDate = "no_due_date";
    public const string OnTrack = "on_track";
    public const string DueSoon = "due_soon";
    public const string Overdue = "overdue";
    public const string CompletedOnTime = "completed_on_time";
    public const string CompletedLate = "completed_late";
    public const string Cancelled = "cancelled";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) {
        NoDueDate,
        OnTrack,
        DueSoon,
        Overdue,
        CompletedOnTime,
        CompletedLate,
        Cancelled
    };
}

public static class ProductionOrderStatuses {
    public const string Planned = "planned";
    public const string Released = "released";
    public const string InProgress = "in_progress";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) {
        Planned,
        Released,
        InProgress,
        Completed,
        Cancelled
    };
}
