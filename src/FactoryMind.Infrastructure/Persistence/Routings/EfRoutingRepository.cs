using FactoryMind.Application.Features.Routings;
using FactoryMind.Domain.Manufacturing;
using Microsoft.EntityFrameworkCore;

namespace FactoryMind.Infrastructure.Persistence.Routings;

public sealed class EfRoutingRepository(FactoryMindDbContext dbContext) : IRoutingRepository {
    public async Task<IReadOnlyList<Routing>> GetByProductAsync(
        Guid productId,
        Guid companyId,
        CancellationToken cancellationToken) => await RoutingQuery(asNoTracking: true)
        .Where(routing => routing.ProductId == productId && routing.CompanyId == companyId)
        .OrderByDescending(routing => routing.Revision)
        .ToListAsync(cancellationToken);

    public Task<Routing?> GetByIdAsync(
        Guid routingId,
        Guid productId,
        Guid companyId,
        CancellationToken cancellationToken) => RoutingQuery(asNoTracking: false).SingleOrDefaultAsync(
            routing => routing.Id == routingId &&
                routing.ProductId == productId &&
                routing.CompanyId == companyId,
            cancellationToken);

    public async Task<int> GetNextRevisionAsync(
        Guid productId,
        Guid companyId,
        CancellationToken cancellationToken) {
        var revision = await dbContext.Routings
            .Where(routing => routing.ProductId == productId && routing.CompanyId == companyId)
            .MaxAsync(routing => (int?)routing.Revision, cancellationToken);
        return revision.GetValueOrDefault() + 1;
    }

    public void Add(Routing routing) => dbContext.Routings.Add(routing);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public async Task<Routing?> ReplaceDraftOperationsAsync(
        Routing routing,
        IReadOnlyList<RoutingOperation> operations,
        DateTime updatedAt,
        CancellationToken cancellationToken) {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var routingUpdated = await dbContext.Routings
            .Where(candidate => candidate.Id == routing.Id &&
                candidate.CompanyId == routing.CompanyId &&
                candidate.ProductId == routing.ProductId &&
                candidate.Status == RoutingStatuses.Draft)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(candidate => candidate.UpdatedAt, updatedAt), cancellationToken);
        if (routingUpdated == 0) {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        await dbContext.RoutingOperations
            .Where(operation => operation.RoutingId == routing.Id)
            .ExecuteDeleteAsync(cancellationToken);
        foreach (var operation in operations) {
            operation.Routing = null;
            operation.WorkCenter = null;
        }

        dbContext.ChangeTracker.Clear();
        dbContext.RoutingOperations.AddRange(operations);
        await dbContext.SaveChangesAsync(cancellationToken);
        var updatedRouting = await RoutingQuery(asNoTracking: true)
            .SingleAsync(candidate => candidate.Id == routing.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return updatedRouting;
    }

    public async Task<RoutingActivationResult> TryActivateAsync(
        Guid routingId,
        Guid productId,
        Guid companyId,
        DateTime activatedAt,
        CancellationToken cancellationToken) {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var routing = await dbContext.Routings
            .FromSqlInterpolated($"""
                SELECT * FROM routings
                WHERE "Id" = {routingId}
                  AND "ProductId" = {productId}
                  AND "CompanyId" = {companyId}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);
        if (routing is null) {
            await transaction.RollbackAsync(cancellationToken);
            return new(RoutingActivationStatus.NotFound, null);
        }
        if (routing.Status != RoutingStatuses.Draft) {
            await transaction.RollbackAsync(cancellationToken);
            return new(RoutingActivationStatus.StateConflict, null);
        }

        await dbContext.Entry(routing).Reference(candidate => candidate.Product).LoadAsync(cancellationToken);
        await dbContext.Entry(routing).Collection(candidate => candidate.Operations).Query()
            .Include(operation => operation.WorkCenter)
            .LoadAsync(cancellationToken);
        if (routing.Operations.Count == 0) {
            await transaction.RollbackAsync(cancellationToken);
            return new(RoutingActivationStatus.OperationsRequired, null);
        }
        if (routing.Operations.Any(operation => operation.Sequence <= 0) ||
            routing.Operations.Select(operation => operation.Sequence).Distinct().Count() !=
            routing.Operations.Count) {
            await transaction.RollbackAsync(cancellationToken);
            return new(RoutingActivationStatus.InvalidSequence, null);
        }
        if (routing.Operations.Any(operation => operation.SetupTimeMinutes < 0 || operation.RunTimeMinutes < 0)) {
            await transaction.RollbackAsync(cancellationToken);
            return new(RoutingActivationStatus.InvalidTiming, null);
        }

        foreach (var workCenterId in routing.Operations.Select(operation => operation.WorkCenterId).Distinct().Order()) {
            var active = await dbContext.WorkCenters
                .FromSqlInterpolated($"""
                    SELECT * FROM work_centers
                    WHERE "Id" = {workCenterId}
                      AND "CompanyId" = {companyId}
                      AND "IsActive" = TRUE
                    FOR SHARE
                    """)
                .AsNoTracking()
                .AnyAsync(cancellationToken);
            if (!active) {
                await transaction.RollbackAsync(cancellationToken);
                return new(RoutingActivationStatus.WorkCenterUnavailable, null);
            }
        }

        await dbContext.Routings
            .Where(candidate => candidate.CompanyId == companyId &&
                candidate.ProductId == productId &&
                candidate.Id != routingId &&
                candidate.Status == RoutingStatuses.Active)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(candidate => candidate.Status, RoutingStatuses.Archived)
                .SetProperty(candidate => candidate.UpdatedAt, activatedAt), cancellationToken);
        routing.Status = RoutingStatuses.Active;
        routing.UpdatedAt = activatedAt;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(RoutingActivationStatus.Success, routing);
    }

    private IQueryable<Routing> RoutingQuery(bool asNoTracking) {
        IQueryable<Routing> query = dbContext.Routings
            .Include(routing => routing.Product)
            .Include(routing => routing.Operations)
                .ThenInclude(operation => operation.WorkCenter);
        return asNoTracking ? query.AsNoTracking() : query;
    }
}
