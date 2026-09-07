using FactoryMind.Domain.Identity;

namespace FactoryMind.Domain.Chat;

public sealed class AiActionProposal {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }
    public Guid CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public Guid ConversationId { get; set; }
    public Conversation? Conversation { get; set; }
    public Guid SourceUserMessageId { get; set; }
    public ChatMessage? SourceUserMessage { get; set; }
    public string ActionType { get; set; } = AiActionTypes.ReleaseProductionOrder;
    public string TargetEntityType { get; set; } = "production_order";
    public Guid ProductionOrderId { get; set; }
    public string TargetDisplay { get; set; } = string.Empty;
    public string Status { get; set; } = AiActionProposalStatuses.Pending;
    public string ProductionOrderNumberSnapshot { get; set; } = string.Empty;
    public string ProductionOrderStatusSnapshot { get; set; } = string.Empty;
    public Guid ProductIdSnapshot { get; set; }
    public string ProductCodeSnapshot { get; set; } = string.Empty;
    public string ProductNameSnapshot { get; set; } = string.Empty;
    public decimal QuantitySnapshot { get; set; }
    public Guid BillOfMaterialIdSnapshot { get; set; }
    public int BillOfMaterialRevisionSnapshot { get; set; }
    public Guid RoutingIdSnapshot { get; set; }
    public int RoutingRevisionSnapshot { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? FailureCode { get; set; }
    public ICollection<AiActionEvent> Events { get; set; } = [];
}

public sealed class AiActionEvent {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProposalId { get; set; }
    public AiActionProposal? Proposal { get; set; }
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? FailureCode { get; set; }
    public string? CorrelationId { get; set; }
    public string? TraceId { get; set; }
}

public static class AiActionTypes {
    public const string ReleaseProductionOrder = "release_production_order";
}

public static class AiActionProposalStatuses {
    public const string Pending = "pending";
    public const string Confirmed = "confirmed";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
    public const string Expired = "expired";
    public const string Stale = "stale";
}

public static class AiActionEventTypes {
    public const string Proposed = "proposed";
    public const string Confirmed = "confirmed";
    public const string ExecutionSucceeded = "execution_succeeded";
    public const string ExecutionFailed = "execution_failed";
    public const string Cancelled = "cancelled";
    public const string Expired = "expired";
    public const string Stale = "stale";
}
