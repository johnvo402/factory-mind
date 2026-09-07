using System.Diagnostics;
using FactoryMind.Application.Features.AiActions;
using FactoryMind.Domain.Chat;
using Microsoft.EntityFrameworkCore;

namespace FactoryMind.Infrastructure.Persistence.Chat;

public sealed class EfAiActionProposalRepository(FactoryMindDbContext dbContext)
    : IAiActionProposalRepository {
    public Task<AiActionProposal?> GetOwnedAsync(
        Guid proposalId,
        Guid companyId,
        Guid userId,
        CancellationToken cancellationToken) => dbContext.AiActionProposals.AsNoTracking()
        .SingleOrDefaultAsync(proposal => proposal.Id == proposalId
            && proposal.CompanyId == companyId
            && proposal.CreatedByUserId == userId, cancellationToken);

    public async Task<IReadOnlyList<AiActionProposal>> GetOwnedByConversationAsync(
        Guid conversationId,
        Guid companyId,
        Guid userId,
        CancellationToken cancellationToken) => await dbContext.AiActionProposals.AsNoTracking()
        .Where(proposal => proposal.ConversationId == conversationId
            && proposal.CompanyId == companyId
            && proposal.CreatedByUserId == userId)
        .OrderBy(proposal => proposal.CreatedAt)
        .Take(50)
        .ToListAsync(cancellationToken);

    public Task<AiActionProposal?> FindReusablePendingAsync(
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
        CancellationToken cancellationToken) => dbContext.AiActionProposals.AsNoTracking()
        .OrderByDescending(proposal => proposal.CreatedAt)
        .FirstOrDefaultAsync(proposal => proposal.ConversationId == conversationId
            && proposal.CompanyId == companyId
            && proposal.CreatedByUserId == userId
            && proposal.ProductionOrderId == productionOrderId
            && proposal.ProductionOrderNumberSnapshot == productionOrderNumber
            && proposal.ProductIdSnapshot == productId
            && proposal.BillOfMaterialIdSnapshot == bomId
            && proposal.RoutingIdSnapshot == routingId
            && proposal.QuantitySnapshot == quantity
            && proposal.Status == AiActionProposalStatuses.Pending
            && proposal.ExpiresAt > now, cancellationToken);

    public Task<int> CountActivePendingAsync(
        Guid companyId,
        Guid userId,
        DateTime now,
        CancellationToken cancellationToken) => dbContext.AiActionProposals.CountAsync(
        proposal => proposal.CompanyId == companyId
            && proposal.CreatedByUserId == userId
            && proposal.Status == AiActionProposalStatuses.Pending
            && proposal.ExpiresAt > now,
        cancellationToken);

    public async Task AddAsync(AiActionProposal proposal, CancellationToken cancellationToken) {
        dbContext.AiActionProposals.Add(proposal);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> TryTransitionAsync(
        Guid proposalId,
        Guid companyId,
        Guid userId,
        string expectedStatus,
        string newStatus,
        string eventType,
        DateTime now,
        string? failureCode,
        CancellationToken cancellationToken) {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var affected = await dbContext.AiActionProposals
            .Where(proposal => proposal.Id == proposalId
                && proposal.CompanyId == companyId
                && proposal.CreatedByUserId == userId
                && proposal.Status == expectedStatus)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(proposal => proposal.Status, newStatus)
                .SetProperty(proposal => proposal.FailureCode, failureCode)
                .SetProperty(proposal => proposal.ConfirmedAt,
                    proposal => newStatus == AiActionProposalStatuses.Confirmed ? now : proposal.ConfirmedAt)
                .SetProperty(proposal => proposal.ExecutedAt,
                    proposal => newStatus == AiActionProposalStatuses.Succeeded ? now : proposal.ExecutedAt)
                .SetProperty(proposal => proposal.CancelledAt,
                    proposal => newStatus == AiActionProposalStatuses.Cancelled ? now : proposal.CancelledAt),
                cancellationToken);
        if (affected != 1) {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        dbContext.AiActionEvents.Add(new AiActionEvent {
            ProposalId = proposalId,
            CompanyId = companyId,
            UserId = userId,
            EventType = eventType,
            CreatedAt = now,
            FailureCode = failureCode,
            TraceId = Activity.Current?.TraceId.ToString()
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
