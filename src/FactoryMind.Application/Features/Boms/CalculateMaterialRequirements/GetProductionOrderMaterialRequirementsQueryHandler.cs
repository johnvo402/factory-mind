using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.ProductionOrders;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Boms.CalculateMaterialRequirements;

public sealed class GetProductionOrderMaterialRequirementsQueryHandler(
    IBomRepository repository,
    IProductionOrderRepository productionOrderRepository,
    MaterialRequirementCalculator calculator,
    ICurrentUser currentUser) : IRequestHandler<
        GetProductionOrderMaterialRequirementsQuery,
        Result<MaterialRequirementsResponse>> {
    public async ValueTask<Result<MaterialRequirementsResponse>> Handle(
        GetProductionOrderMaterialRequirementsQuery query,
        CancellationToken cancellationToken) {
        var order = await productionOrderRepository.GetByIdAsync(
            query.ProductionOrderId,
            currentUser.CompanyId,
            cancellationToken);
        if (order is null) {
            return Result<MaterialRequirementsResponse>.Failure(ProductionOrderErrors.NotFound);
        }

        var bom = order.BillOfMaterialId.HasValue
            ? await repository.GetByIdAsync(
                order.BillOfMaterialId.Value,
                order.ProductId,
                currentUser.CompanyId,
                cancellationToken)
            : order.Status == ProductionOrderStatuses.Planned
                ? await repository.GetActiveAsync(
                    order.ProductId,
                    currentUser.CompanyId,
                    cancellationToken)
                : null;
        if (bom is null) {
            return Result<MaterialRequirementsResponse>.Failure(order.BillOfMaterialId.HasValue ||
                order.Status != ProductionOrderStatuses.Planned
                    ? ProductionOrderErrors.LockedBomRequired
                    : BomErrors.ActiveNotFound);
        }

        var availability = await repository.GetAvailableQuantitiesAsync(
            currentUser.CompanyId,
            bom.Items.Select(item => item.MaterialId).ToList(),
            cancellationToken);
        return Result<MaterialRequirementsResponse>.Success(
            calculator.Calculate(bom, order.Quantity, availability));
    }
}
