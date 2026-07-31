using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.BusinessData;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.ProductionOrders.GetProductionOrders;

public sealed class GetProductionOrdersQueryHandler(
    IProductionOrderRepository repository,
    ICurrentUser currentUser) : IRequestHandler<GetProductionOrdersQuery, Result<IReadOnlyList<ProductionOrderResponse>>> {
    public async ValueTask<Result<IReadOnlyList<ProductionOrderResponse>>> Handle(
        GetProductionOrdersQuery query,
        CancellationToken cancellationToken) {
        var orders = await repository.GetByCompanyAsync(
            currentUser.CompanyId,
            BusinessDataNormalization.Search(query.Search),
            cancellationToken);
        return Result<IReadOnlyList<ProductionOrderResponse>>.Success(
            orders.Select(ProductionOrderResponse.From).ToList());
    }
}
