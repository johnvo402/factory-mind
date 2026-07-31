using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.BusinessData;
using FactoryMind.Application.Features.Products;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.ProductionOrders.UpdateProductionOrder;

public sealed class UpdateProductionOrderCommandHandler(
    IProductionOrderRepository repository,
    IProductRepository productRepository,
    ICurrentUser currentUser) : IRequestHandler<UpdateProductionOrderCommand, Result<ProductionOrderResponse>> {
    public async ValueTask<Result<ProductionOrderResponse>> Handle(
        UpdateProductionOrderCommand command,
        CancellationToken cancellationToken) {
        var order = await repository.GetByIdAsync(
            command.ProductionOrderId,
            currentUser.CompanyId,
            cancellationToken);
        if (order is null) {
            return Result<ProductionOrderResponse>.Failure(ProductionOrderErrors.NotFound);
        }

        var product = await productRepository.GetByIdAsync(
            command.ProductId,
            currentUser.CompanyId,
            cancellationToken);
        if (product is null) {
            return Result<ProductionOrderResponse>.Failure(ProductionOrderErrors.ProductNotFound);
        }

        var number = BusinessDataNormalization.Code(command.Number);
        if (await repository.NumberExistsAsync(
                currentUser.CompanyId,
                number,
                order.Id,
                cancellationToken)) {
            return Result<ProductionOrderResponse>.Failure(ProductionOrderErrors.NumberAlreadyExists);
        }

        order.Number = number;
        order.ProductId = product.Id;
        order.Product = product;
        order.Quantity = command.Quantity;
        order.Status = command.Status.Trim().ToLowerInvariant();
        order.UpdatedAt = DateTime.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);
        return Result<ProductionOrderResponse>.Success(ProductionOrderResponse.From(order));
    }
}
