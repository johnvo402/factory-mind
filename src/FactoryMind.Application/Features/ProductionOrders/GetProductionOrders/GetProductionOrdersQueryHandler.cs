using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.BusinessData;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.ProductionOrders.GetProductionOrders;

public sealed class GetProductionOrdersQueryHandler(
    IProductionOrderRepository repository,
    ICurrentUser currentUser,
    IProductionOrderDeliveryRiskCalculator riskCalculator)
    : IRequestHandler<GetProductionOrdersQuery, Result<ProductionOrderPageResponse>> {
    public async ValueTask<Result<ProductionOrderPageResponse>> Handle(
        GetProductionOrdersQuery query,
        CancellationToken cancellationToken) {
        var criteria = new ProductionOrderListCriteria(
            BusinessDataNormalization.Search(query.Search),
            query.Status,
            query.Priority,
            query.DeliveryStatus,
            riskCalculator.NormalizeDueDate(query.DueFrom),
            riskCalculator.NormalizeDueDate(query.DueTo),
            query.ProductId,
            query.Page,
            query.PageSize,
            query.SortBy,
            query.SortDirection,
            riskCalculator.UtcNow,
            riskCalculator.DueSoonThrough,
            query.PlanningOrder);
        var result = await repository.GetByCompanyAsync(
            currentUser.CompanyId,
            criteria,
            cancellationToken);
        return Result<ProductionOrderPageResponse>.Success(new ProductionOrderPageResponse(
            result.Items.Select(order => ProductionOrderResponse.From(order, riskCalculator)).ToList(),
            query.Page,
            query.PageSize,
            result.TotalCount));
    }
}
