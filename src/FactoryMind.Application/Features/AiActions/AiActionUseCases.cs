using System.Diagnostics;
using FactoryMind.Application.Common.Authorization;
using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.ProductionOrders;
using FactoryMind.Application.Features.ProductionOrders.ReleaseProductionOrder;
using FactoryMind.Domain.Chat;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;
using FactoryMind.Shared.Observability;
using Mediator;

namespace FactoryMind.Application.Features.AiActions;

public sealed record GetAiActionProposalQuery(Guid ProposalId)
    : IRequest<Result<AiActionProposalResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Authenticated;
}

public sealed record GetConversationAiActionProposalsQuery(Guid ConversationId)
    : IRequest<Result<IReadOnlyList<AiActionProposalResponse>>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Authenticated;
}

public sealed record ConfirmAiActionProposalCommand(Guid ProposalId)
    : IRequest<Result<AiActionProposalResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}

public sealed record CancelAiActionProposalCommand(Guid ProposalId)
    : IRequest<Result<AiActionProposalResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Authenticated;
}

public sealed class GetAiActionProposalQueryHandler(
    IAiActionProposalRepository repository,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : IRequestHandler<GetAiActionProposalQuery, Result<AiActionProposalResponse>> {
    public async ValueTask<Result<AiActionProposalResponse>> Handle(
        GetAiActionProposalQuery query,
        CancellationToken cancellationToken) {
        var proposal = await AiActionLifecycle.GetOwnedAndExpireAsync(
            repository, query.ProposalId, currentUser, timeProvider, cancellationToken);
        return proposal is null
            ? Result<AiActionProposalResponse>.Failure(AiActionErrors.NotFound)
            : Result<AiActionProposalResponse>.Success(AiActionProposalResponse.From(proposal));
    }
}

public sealed class GetConversationAiActionProposalsQueryHandler(
    IAiActionProposalRepository repository,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : IRequestHandler<GetConversationAiActionProposalsQuery, Result<IReadOnlyList<AiActionProposalResponse>>> {
    public async ValueTask<Result<IReadOnlyList<AiActionProposalResponse>>> Handle(
        GetConversationAiActionProposalsQuery query,
        CancellationToken cancellationToken) {
        var proposals = await repository.GetOwnedByConversationAsync(
            query.ConversationId, currentUser.CompanyId, currentUser.UserId, cancellationToken);
        foreach (var proposal in proposals.Where(candidate =>
                     candidate.Status == AiActionProposalStatuses.Pending
                     && candidate.ExpiresAt <= timeProvider.GetUtcNow().UtcDateTime)) {
            var transitioned = await repository.TryTransitionAsync(
                proposal.Id, currentUser.CompanyId, currentUser.UserId,
                AiActionProposalStatuses.Pending, AiActionProposalStatuses.Expired,
                AiActionEventTypes.Expired, timeProvider.GetUtcNow().UtcDateTime,
                "action_expired", cancellationToken);
            if (transitioned) {
                proposal.Status = AiActionProposalStatuses.Expired;
                proposal.FailureCode = "action_expired";
            }
        }
        return Result<IReadOnlyList<AiActionProposalResponse>>.Success(
            proposals.Select(AiActionProposalResponse.From).ToList());
    }
}

public sealed class CancelAiActionProposalCommandHandler(
    IAiActionProposalRepository repository,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : IRequestHandler<CancelAiActionProposalCommand, Result<AiActionProposalResponse>> {
    public async ValueTask<Result<AiActionProposalResponse>> Handle(
        CancelAiActionProposalCommand command,
        CancellationToken cancellationToken) {
        var proposal = await AiActionLifecycle.GetOwnedAndExpireAsync(
            repository, command.ProposalId, currentUser, timeProvider, cancellationToken);
        if (proposal is null) {
            return Result<AiActionProposalResponse>.Failure(AiActionErrors.NotFound);
        }
        if (proposal.Status == AiActionProposalStatuses.Cancelled) {
            return Result<AiActionProposalResponse>.Success(AiActionProposalResponse.From(proposal));
        }
        if (proposal.Status != AiActionProposalStatuses.Pending) {
            return Result<AiActionProposalResponse>.Failure(
                proposal.Status == AiActionProposalStatuses.Expired
                    ? AiActionErrors.Expired
                    : AiActionErrors.InvalidState);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var transitioned = await repository.TryTransitionAsync(
            proposal.Id, currentUser.CompanyId, currentUser.UserId,
            AiActionProposalStatuses.Pending, AiActionProposalStatuses.Cancelled,
            AiActionEventTypes.Cancelled, now, "action_cancelled", cancellationToken);
        var updated = await repository.GetOwnedAsync(
            proposal.Id, currentUser.CompanyId, currentUser.UserId, cancellationToken);
        if (!transitioned && updated?.Status != AiActionProposalStatuses.Cancelled) {
            return Result<AiActionProposalResponse>.Failure(AiActionErrors.InvalidState);
        }
        FactoryMindTelemetry.AiActionProposals.Add(1, FactoryMindTelemetry.Tags(
            ("action", proposal.ActionType), ("outcome", "cancelled"), ("reason", "user_cancelled")));
        return Result<AiActionProposalResponse>.Success(AiActionProposalResponse.From(updated!));
    }
}

public sealed class ConfirmAiActionProposalCommandHandler(
    IAiActionProposalRepository repository,
    IProductionExecutionRepository productionOrders,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    ISender sender)
    : IRequestHandler<ConfirmAiActionProposalCommand, Result<AiActionProposalResponse>> {
    public async ValueTask<Result<AiActionProposalResponse>> Handle(
        ConfirmAiActionProposalCommand command,
        CancellationToken cancellationToken) {
        var started = Stopwatch.GetTimestamp();
        using var activity = FactoryMindTelemetry.ActivitySource.StartActivity("factorymind.ai.action.confirm");
        activity?.SetTag("action.type", AiActionTypes.ReleaseProductionOrder);
        var proposal = await AiActionLifecycle.GetOwnedAndExpireAsync(
            repository, command.ProposalId, currentUser, timeProvider, cancellationToken);
        if (proposal is null) {
            return Failure(AiActionErrors.NotFound, "not_found", started);
        }
        if (proposal.Status == AiActionProposalStatuses.Succeeded) {
            RecordConfirmation("success", "already_succeeded", started);
            return Result<AiActionProposalResponse>.Success(AiActionProposalResponse.From(proposal));
        }
        if (proposal.Status == AiActionProposalStatuses.Expired) {
            return Failure(AiActionErrors.Expired, "expired", started);
        }
        if (proposal.Status != AiActionProposalStatuses.Pending
            && proposal.Status != AiActionProposalStatuses.Confirmed) {
            return Failure(AiActionErrors.InvalidState, "invalid_state", started);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (proposal.Status == AiActionProposalStatuses.Pending) {
            await repository.TryTransitionAsync(
                proposal.Id, currentUser.CompanyId, currentUser.UserId,
                AiActionProposalStatuses.Pending, AiActionProposalStatuses.Confirmed,
                AiActionEventTypes.Confirmed, now, null, cancellationToken);
        }

        proposal = await repository.GetOwnedAsync(
            proposal.Id, currentUser.CompanyId, currentUser.UserId, cancellationToken);
        if (proposal!.Status == AiActionProposalStatuses.Succeeded) {
            return Result<AiActionProposalResponse>.Success(AiActionProposalResponse.From(proposal));
        }
        if (proposal.Status != AiActionProposalStatuses.Confirmed) {
            return Failure(
                proposal.Status == AiActionProposalStatuses.Expired
                    ? AiActionErrors.Expired
                    : AiActionErrors.InvalidState,
                proposal.Status == AiActionProposalStatuses.Expired ? "expired" : "invalid_state",
                started);
        }

        var current = await productionOrders.GetReleaseSnapshotByNumberAsync(
            proposal.ProductionOrderNumberSnapshot, currentUser.CompanyId, cancellationToken);
        if (current is null || current.ProductionOrderId != proposal.ProductionOrderId) {
            return await TransitionFailure(proposal, AiActionProposalStatuses.Stale,
                AiActionEventTypes.Stale, AiActionErrors.Stale, "stale", started, cancellationToken);
        }

        if (current.Status == ProductionOrderStatuses.Released) {
            var released = await productionOrders.GetAsync(
                proposal.ProductionOrderId, currentUser.CompanyId, cancellationToken);
            if (released is not null
                && released.Status == ProductionOrderStatuses.Released
                && released.Number == proposal.ProductionOrderNumberSnapshot
                && released.ProductId == proposal.ProductIdSnapshot
                && released.Quantity == proposal.QuantitySnapshot
                && released.BillOfMaterialId == proposal.BillOfMaterialIdSnapshot
                && released.RoutingId == proposal.RoutingIdSnapshot) {
                return await CompleteSuccess(proposal, now, "reconciled", started, cancellationToken);
            }
            return await TransitionFailure(proposal, AiActionProposalStatuses.Stale,
                AiActionEventTypes.Stale, AiActionErrors.Stale, "stale", started, cancellationToken);
        }

        if (current.Status != proposal.ProductionOrderStatusSnapshot
            || !SnapshotFieldsMatch(proposal, current)) {
            return await TransitionFailure(proposal, AiActionProposalStatuses.Stale,
                AiActionEventTypes.Stale, AiActionErrors.Stale, "stale", started, cancellationToken);
        }
        if (!current.WorkCentersAvailable) {
            return await TransitionFailure(proposal, AiActionProposalStatuses.Failed,
                AiActionEventTypes.ExecutionFailed, AiActionErrors.WorkCenterUnavailable,
                "work_center_unavailable", started, cancellationToken);
        }

        using var execution = FactoryMindTelemetry.ActivitySource.StartActivity("factorymind.ai.action.execute");
        var release = await sender.Send(new ReleaseProductionOrderCommand(
            proposal.ProductionOrderId,
            new(
                proposal.ProductionOrderNumberSnapshot,
                proposal.ProductionOrderStatusSnapshot,
                proposal.ProductIdSnapshot,
                proposal.QuantitySnapshot,
                proposal.BillOfMaterialIdSnapshot,
                proposal.RoutingIdSnapshot)), cancellationToken);
        if (release.IsSuccess) {
            return await CompleteSuccess(proposal, now, "success", started, cancellationToken);
        }

        if (release.Error?.Code == ProductionOrderErrors.SnapshotStale.Code) {
            return await TransitionFailure(proposal, AiActionProposalStatuses.Stale,
                AiActionEventTypes.Stale, AiActionErrors.Stale, "stale", started, cancellationToken);
        }

        var postRelease = await productionOrders.GetAsync(
            proposal.ProductionOrderId, currentUser.CompanyId, cancellationToken);
        if (postRelease?.Status == ProductionOrderStatuses.Released
            && postRelease.Number == proposal.ProductionOrderNumberSnapshot
            && postRelease.ProductId == proposal.ProductIdSnapshot
            && postRelease.Quantity == proposal.QuantitySnapshot
            && postRelease.BillOfMaterialId == proposal.BillOfMaterialIdSnapshot
            && postRelease.RoutingId == proposal.RoutingIdSnapshot) {
            return await CompleteSuccess(proposal, now, "already_succeeded", started, cancellationToken);
        }

        return await TransitionFailure(proposal, AiActionProposalStatuses.Failed,
            AiActionEventTypes.ExecutionFailed, AiActionErrors.InvalidState,
            "invalid_state", started, cancellationToken);
    }

    private static bool SnapshotFieldsMatch(AiActionProposal proposal, ProductionOrderReleaseSnapshot current) =>
        current.Number == proposal.ProductionOrderNumberSnapshot
        && current.ProductId == proposal.ProductIdSnapshot
        && current.Quantity == proposal.QuantitySnapshot
        && current.ActiveBillOfMaterialId == proposal.BillOfMaterialIdSnapshot
        && current.ActiveRoutingId == proposal.RoutingIdSnapshot;

    private async Task<Result<AiActionProposalResponse>> CompleteSuccess(
        AiActionProposal proposal,
        DateTime now,
        string reason,
        long started,
        CancellationToken cancellationToken) {
        var transitioned = await repository.TryTransitionAsync(
            proposal.Id, currentUser.CompanyId, currentUser.UserId,
            AiActionProposalStatuses.Confirmed, AiActionProposalStatuses.Succeeded,
            AiActionEventTypes.ExecutionSucceeded, now, null, cancellationToken);
        var updated = await repository.GetOwnedAsync(
            proposal.Id, currentUser.CompanyId, currentUser.UserId, cancellationToken);
        RecordConfirmation("success", reason, started);
        if (transitioned) {
            FactoryMindTelemetry.AiActionExecutions.Add(1, FactoryMindTelemetry.Tags(
                ("action", proposal.ActionType), ("outcome", "success"), ("reason", reason)));
        }
        return Result<AiActionProposalResponse>.Success(AiActionProposalResponse.From(updated!));
    }

    private async Task<Result<AiActionProposalResponse>> TransitionFailure(
        AiActionProposal proposal,
        string status,
        string eventType,
        Error error,
        string reason,
        long started,
        CancellationToken cancellationToken) {
        var transitioned = await repository.TryTransitionAsync(
            proposal.Id, currentUser.CompanyId, currentUser.UserId,
            AiActionProposalStatuses.Confirmed, status, eventType,
            timeProvider.GetUtcNow().UtcDateTime, error.Code, cancellationToken);
        RecordConfirmation("rejected", reason, started);
        if (transitioned) {
            FactoryMindTelemetry.AiActionExecutions.Add(1, FactoryMindTelemetry.Tags(
                ("action", proposal.ActionType), ("outcome", status), ("reason", reason)));
        }
        return Result<AiActionProposalResponse>.Failure(error);
    }

    private static Result<AiActionProposalResponse> Failure(Error error, string reason, long started) {
        RecordConfirmation("rejected", reason, started);
        return Result<AiActionProposalResponse>.Failure(error);
    }

    private static void RecordConfirmation(string outcome, string reason, long started) {
        var tags = FactoryMindTelemetry.Tags(
            ("action", AiActionTypes.ReleaseProductionOrder), ("outcome", outcome), ("reason", reason));
        FactoryMindTelemetry.AiActionConfirmations.Add(1, tags);
        FactoryMindTelemetry.AiActionDuration.Record(
            Stopwatch.GetElapsedTime(started).TotalMilliseconds, tags);
        Activity.Current?.SetTag("outcome", outcome);
    }
}

internal static class AiActionLifecycle {
    public static async Task<AiActionProposal?> GetOwnedAndExpireAsync(
        IAiActionProposalRepository repository,
        Guid proposalId,
        ICurrentUser currentUser,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) {
        var proposal = await repository.GetOwnedAsync(
            proposalId, currentUser.CompanyId, currentUser.UserId, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (proposal?.Status == AiActionProposalStatuses.Pending && proposal.ExpiresAt <= now) {
            await repository.TryTransitionAsync(
                proposal.Id, currentUser.CompanyId, currentUser.UserId,
                AiActionProposalStatuses.Pending, AiActionProposalStatuses.Expired,
                AiActionEventTypes.Expired, now, "action_expired", cancellationToken);
            proposal = await repository.GetOwnedAsync(
                proposal.Id, currentUser.CompanyId, currentUser.UserId, cancellationToken);
        }
        return proposal;
    }
}
