using FactoryMind.Application.Common.Identity;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.ProductionOrders.DeleteProductionOrder;

public sealed class DeleteProductionOrderCommandHandler(
    IProductionOrderRepository repository,
    ICurrentUser currentUser) : IRequestHandler<DeleteProductionOrderCommand, Result> {
    public async ValueTask<Result> Handle(
        DeleteProductionOrderCommand command,
        CancellationToken cancellationToken) {
        var order = await repository.GetByIdAsync(
            command.ProductionOrderId,
            currentUser.CompanyId,
            cancellationToken);
        if (order is null) {
            return Result.Failure(ProductionOrderErrors.NotFound);
        }

        if (order.Status != ProductionOrderStatuses.Planned ||
            !await repository.TryDeletePlannedAsync(order.Id, currentUser.CompanyId, cancellationToken)) {
            return Result.Failure(ProductionOrderErrors.PlannedRequired);
        }
        return Result.Success();
    }
}
