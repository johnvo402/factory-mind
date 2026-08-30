using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.BusinessData;
using FactoryMind.Application.Features.Products;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.ProductionOrders.CreateProductionOrder;

public sealed class CreateProductionOrderCommandHandler(
    IProductionOrderRepository repository,
    IProductRepository productRepository,
    ICurrentUser currentUser) : IRequestHandler<CreateProductionOrderCommand, Result<ProductionOrderResponse>> {
    public async ValueTask<Result<ProductionOrderResponse>> Handle(
        CreateProductionOrderCommand command,
        CancellationToken cancellationToken) {
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
                null,
                cancellationToken)) {
            return Result<ProductionOrderResponse>.Failure(ProductionOrderErrors.NumberAlreadyExists);
        }

        var now = DateTime.UtcNow;
        var order = new ProductionOrder {
            CompanyId = currentUser.CompanyId,
            Number = number,
            ProductId = product.Id,
            Product = product,
            Quantity = command.Quantity,
            Status = ProductionOrderStatuses.Planned,
            CreatedAt = now,
            UpdatedAt = now
        };
        repository.Add(order);
        await repository.SaveChangesAsync(cancellationToken);
        return Result<ProductionOrderResponse>.Success(ProductionOrderResponse.From(order));
    }
}
