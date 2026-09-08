using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.Boms;
using FactoryMind.Application.Features.Routings;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.ProductionOrders.ReleaseProductionOrder;

public sealed class ReleaseProductionOrderCommandHandler(
    IProductionExecutionRepository repository,
    ICurrentUser currentUser,
    IProductionOrderDeliveryRiskCalculator riskCalculator)
    : IRequestHandler<ReleaseProductionOrderCommand, Result<ProductionOrderResponse>> {
    public async ValueTask<Result<ProductionOrderResponse>> Handle(
        ReleaseProductionOrderCommand command,
        CancellationToken cancellationToken) {
        var order = await repository.GetAsync(
            command.ProductionOrderId,
            currentUser.CompanyId,
            cancellationToken);
        if (order is null) {
            return Result<ProductionOrderResponse>.Failure(ProductionOrderErrors.NotFound);
        }
        if (order.Status != ProductionOrderStatuses.Planned) {
            return Result<ProductionOrderResponse>.Failure(ProductionOrderErrors.InvalidTransition);
        }

        var outcome = await repository.TryReleaseAsync(
            order.Id,
            currentUser.CompanyId,
            riskCalculator.UtcNow,
            command.Expectation,
            cancellationToken);
        return outcome.Status switch {
            ProductionExecutionStatus.Success => Result<ProductionOrderResponse>.Success(
                ProductionOrderResponse.From(outcome.Order!, riskCalculator)),
            ProductionExecutionStatus.ActiveBomNotFound =>
                Result<ProductionOrderResponse>.Failure(BomErrors.ActiveNotFound),
            ProductionExecutionStatus.ActiveRoutingNotFound =>
                Result<ProductionOrderResponse>.Failure(RoutingErrors.ActiveNotFound),
            ProductionExecutionStatus.RoutingWorkCenterUnavailable =>
                Result<ProductionOrderResponse>.Failure(RoutingErrors.WorkCenterInactive),
            ProductionExecutionStatus.SnapshotStale =>
                Result<ProductionOrderResponse>.Failure(ProductionOrderErrors.SnapshotStale),
            _ => Result<ProductionOrderResponse>.Failure(ProductionOrderErrors.InvalidTransition)
        };
    }
}
