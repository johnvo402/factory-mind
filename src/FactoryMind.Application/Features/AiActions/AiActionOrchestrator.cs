using System.Diagnostics;
using FactoryMind.Application.Common.Authorization;
using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.Chat;
using FactoryMind.Application.Features.ProductionOrders;
using FactoryMind.Domain.Chat;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.AI;
using FactoryMind.Shared.Observability;

namespace FactoryMind.Application.Features.AiActions;

public sealed class AiActionOrchestrator(
    AiActionIntentGate intentGate,
    IAiActionPlanner planner,
    IAiActionProposalRegistry registry,
    IAiActionProposalRepository proposals,
    IProductionExecutionRepository productionOrders,
    IPolicyChecker policyChecker,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    AiActionSettings settings) : IAiActionOrchestrator {
    public async Task<AiActionProposalAttempt> ProposeAsync(
        Guid conversationId,
        Guid sourceUserMessageId,
        string question,
        IReadOnlyList<ChatPromptMessage> recentMessages,
        CancellationToken cancellationToken) {
        if (intentGate.IsMultipleOrderRequest(question)) {
            return new(null, "AI release supports one Production Order per request. Please choose one order.");
        }
        if (!intentGate.IsEligible(question)) {
            return new(null, null);
        }

        using var activity = FactoryMindTelemetry.ActivitySource.StartActivity("factorymind.ai.action.propose");
        activity?.SetTag("action.type", AiActionTypes.ReleaseProductionOrder);
        if (!await policyChecker.IsAuthorizedAsync(AuthorizationPolicies.Manager, cancellationToken)) {
            RecordProposal("rejected", "unauthorized");
            return new(null, "Releasing a Production Order requires Manager authorization.");
        }

        AiActionPlan plan;
        try {
            var messages = recentMessages.TakeLast(6).ToList();
            messages.Add(new(ChatRoles.User, question));
            plan = await planner.PlanAsync(messages, registry.Definitions, cancellationToken);
        } catch (AiProviderException) {
            RecordProposal("rejected", "planner_unavailable");
            return new(null, "I could not prepare a release proposal. No action was taken.");
        }

        if (plan.Calls.Count != 1
            || !registry.TryReadProductionOrderNumber(plan.Calls[0], out var number)) {
            RecordProposal("rejected", "invalid_arguments");
            return new(null, "I could not identify one safe release target. No action was taken.");
        }

        var snapshot = await productionOrders.GetReleaseSnapshotByNumberAsync(
            number, currentUser.CompanyId, cancellationToken);
        if (snapshot is null) {
            RecordProposal("rejected", "not_found");
            return new(null, "That Production Order was not found. No action was taken.");
        }
        if (snapshot.Status != ProductionOrderStatuses.Planned) {
            RecordProposal("rejected", "invalid_state");
            return new(null, "Only a planned Production Order can be proposed for release.");
        }
        if (snapshot.ActiveBillOfMaterialId is null) {
            RecordProposal("rejected", "active_bom_not_found");
            return new(null, AiActionErrors.ActiveBomNotFound.Message);
        }
        if (snapshot.ActiveRoutingId is null || !snapshot.HasRoutingOperations) {
            RecordProposal("rejected", "active_routing_not_found");
            return new(null, AiActionErrors.ActiveRoutingNotFound.Message);
        }
        if (!snapshot.WorkCentersAvailable) {
            RecordProposal("rejected", "work_center_unavailable");
            return new(null, AiActionErrors.WorkCenterUnavailable.Message);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var existing = await proposals.FindReusablePendingAsync(
            conversationId,
            currentUser.CompanyId,
            currentUser.UserId,
            snapshot.ProductionOrderId,
            snapshot.Number,
            snapshot.ProductId,
            snapshot.ActiveBillOfMaterialId.Value,
            snapshot.ActiveRoutingId.Value,
            snapshot.Quantity,
            now,
            cancellationToken);
        if (existing is not null) {
            RecordProposal("pending", "reused");
            return new(AiActionProposalResponse.From(existing), null);
        }

        if (await proposals.CountActivePendingAsync(
                currentUser.CompanyId, currentUser.UserId, now, cancellationToken)
            >= settings.MaximumPendingProposalsPerUser) {
            RecordProposal("rejected", "limit_reached");
            return new(null, "You already have the maximum number of pending AI action proposals.");
        }

        var proposal = new AiActionProposal {
            CompanyId = currentUser.CompanyId,
            CreatedByUserId = currentUser.UserId,
            ConversationId = conversationId,
            SourceUserMessageId = sourceUserMessageId,
            ProductionOrderId = snapshot.ProductionOrderId,
            TargetDisplay = snapshot.Number,
            ProductionOrderNumberSnapshot = snapshot.Number,
            ProductionOrderStatusSnapshot = snapshot.Status,
            ProductIdSnapshot = snapshot.ProductId,
            ProductCodeSnapshot = snapshot.ProductCode,
            ProductNameSnapshot = snapshot.ProductName,
            QuantitySnapshot = snapshot.Quantity,
            BillOfMaterialIdSnapshot = snapshot.ActiveBillOfMaterialId.Value,
            BillOfMaterialRevisionSnapshot = snapshot.ActiveBillOfMaterialRevision!.Value,
            RoutingIdSnapshot = snapshot.ActiveRoutingId.Value,
            RoutingRevisionSnapshot = snapshot.ActiveRoutingRevision!.Value,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(settings.ProposalExpirationMinutes)
        };
        proposal.Events.Add(new AiActionEvent {
            ProposalId = proposal.Id,
            CompanyId = currentUser.CompanyId,
            UserId = currentUser.UserId,
            EventType = AiActionEventTypes.Proposed,
            CreatedAt = now,
            TraceId = Activity.Current?.TraceId.ToString()
        });
        await proposals.AddAsync(proposal, cancellationToken);
        RecordProposal("pending", "proposal_created");
        return new(AiActionProposalResponse.From(proposal), null);
    }

    private static void RecordProposal(string outcome, string reason) {
        FactoryMindTelemetry.AiActionProposals.Add(1, FactoryMindTelemetry.Tags(
            ("action", AiActionTypes.ReleaseProductionOrder),
            ("outcome", outcome),
            ("reason", reason)));
        Activity.Current?.SetTag("outcome", outcome);
    }
}
