using FactoryMind.Application.Common.Identity;
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

        repository.Remove(order);
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
