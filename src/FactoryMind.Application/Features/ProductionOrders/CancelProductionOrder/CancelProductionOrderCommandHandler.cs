using FactoryMind.Application.Common.Identity;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.ProductionOrders.CancelProductionOrder;

public sealed class CancelProductionOrderCommandHandler(
    IProductionExecutionRepository repository,
    ICurrentUser currentUser)
    : IRequestHandler<CancelProductionOrderCommand, Result<ProductionOrderResponse>> {
    public async ValueTask<Result<ProductionOrderResponse>> Handle(
        CancelProductionOrderCommand command,
        CancellationToken cancellationToken) {
        var order = await repository.GetAsync(
            command.ProductionOrderId,
            currentUser.CompanyId,
            cancellationToken);
        if (order is null) {
            return Result<ProductionOrderResponse>.Failure(ProductionOrderErrors.NotFound);
        }
        if (order.Status is not (ProductionOrderStatuses.Planned or ProductionOrderStatuses.Released)) {
            return Result<ProductionOrderResponse>.Failure(ProductionOrderErrors.InvalidTransition);
        }

        var outcome = await repository.TryCancelAsync(
            order.Id,
            currentUser.CompanyId,
            DateTime.UtcNow,
            cancellationToken);
        return outcome.Status == ProductionExecutionStatus.Success
            ? Result<ProductionOrderResponse>.Success(ProductionOrderResponse.From(outcome.Order!))
            : Result<ProductionOrderResponse>.Failure(ProductionOrderErrors.InvalidTransition);
    }
}
