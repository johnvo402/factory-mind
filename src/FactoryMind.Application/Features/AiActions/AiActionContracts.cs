using System.Text.Json;
using FactoryMind.Domain.Chat;
using FactoryMind.Shared.Contracts;

namespace FactoryMind.Application.Features.AiActions;

public sealed class AiActionSettings {
    public const string SectionName = "AiActions";
    public int ProposalExpirationMinutes { get; set; } = 10;
    public int MaximumPendingProposalsPerUser { get; set; } = 10;
}

public sealed record AiActionDefinition(string Name, string Description, JsonElement Parameters);
public sealed record AiActionCall(string Name, JsonElement Arguments);
public sealed record AiActionPlan(IReadOnlyList<AiActionCall> Calls);

public interface IAiActionPlanner {
    Task<AiActionPlan> PlanAsync(
        IReadOnlyList<Features.Chat.ChatPromptMessage> messages,
        IReadOnlyList<AiActionDefinition> actions,
        CancellationToken cancellationToken);
}

public interface IAiActionProposalRegistry {
    IReadOnlyList<AiActionDefinition> Definitions { get; }
    bool TryReadProductionOrderNumber(AiActionCall call, out string number);
}

public sealed record AiActionProposalSummary(
    string ProductionOrderNumber,
    string Product,
    decimal Quantity,
    int BomRevision,
    int RoutingRevision);

public sealed record AiActionProposalResponse(
    Guid ProposalId,
    string ActionType,
    string Status,
    string Title,
    AiActionProposalSummary Summary,
    DateTime ExpiresAt,
    string? FailureCode) {
    public static AiActionProposalResponse From(AiActionProposal proposal) => new(
        proposal.Id,
        proposal.ActionType,
        proposal.Status,
        $"Release {proposal.ProductionOrderNumberSnapshot}",
        new(
            proposal.ProductionOrderNumberSnapshot,
            $"{proposal.ProductCodeSnapshot} - {proposal.ProductNameSnapshot}",
            proposal.QuantitySnapshot,
            proposal.BillOfMaterialRevisionSnapshot,
            proposal.RoutingRevisionSnapshot),
        proposal.ExpiresAt,
        proposal.FailureCode);
}

public sealed record AiActionProposalAttempt(AiActionProposalResponse? Proposal, string? Notice);

public interface IAiActionOrchestrator {
    Task<AiActionProposalAttempt> ProposeAsync(
        Guid conversationId,
        Guid sourceUserMessageId,
        string question,
        IReadOnlyList<Features.Chat.ChatPromptMessage> recentMessages,
        CancellationToken cancellationToken);
}

public interface IAiActionProposalRepository {
    Task<AiActionProposal?> GetOwnedAsync(
        Guid proposalId,
        Guid companyId,
        Guid userId,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<AiActionProposal>> GetOwnedByConversationAsync(
        Guid conversationId,
        Guid companyId,
        Guid userId,
        CancellationToken cancellationToken);
    Task<AiActionProposal?> FindReusablePendingAsync(
        Guid conversationId,
        Guid companyId,
        Guid userId,
        Guid productionOrderId,
        string productionOrderNumber,
        Guid productId,
        Guid bomId,
        Guid routingId,
        decimal quantity,
        DateTime now,
        CancellationToken cancellationToken);
    Task<int> CountActivePendingAsync(
        Guid companyId,
        Guid userId,
        DateTime now,
        CancellationToken cancellationToken);
    Task AddAsync(AiActionProposal proposal, CancellationToken cancellationToken);
    Task<bool> TryTransitionAsync(
        Guid proposalId,
        Guid companyId,
        Guid userId,
        string expectedStatus,
        string newStatus,
        string eventType,
        DateTime now,
        string? failureCode,
        CancellationToken cancellationToken);
}

public static class AiActionErrors {
    public static readonly Error NotFound = new(
        "action_not_found", "AI action proposal was not found.", 404);
    public static readonly Error Expired = new(
        "action_expired", "The AI action proposal has expired.", 409);
    public static readonly Error Stale = new(
        "action_stale", "The reviewed production data changed. Create a new proposal.", 409);
    public static readonly Error InvalidState = new(
        "action_invalid_state", "The AI action proposal cannot be changed in its current state.", 409);
    public static readonly Error WorkCenterUnavailable = new(
        "work_center_unavailable", "A required work center is unavailable.", 409);
    public static readonly Error ActiveBomNotFound = new(
        "active_bom_not_found", "An active bill of material is required.", 409);
    public static readonly Error ActiveRoutingNotFound = new(
        "active_routing_not_found", "An active routing with operations is required.", 409);
}
