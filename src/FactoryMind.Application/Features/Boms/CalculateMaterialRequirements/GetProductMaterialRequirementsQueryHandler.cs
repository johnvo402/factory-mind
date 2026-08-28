using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.Products;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Boms.CalculateMaterialRequirements;

public sealed class GetProductMaterialRequirementsQueryHandler(
    IBomRepository repository,
    IProductRepository productRepository,
    MaterialRequirementCalculator calculator,
    ICurrentUser currentUser) : IRequestHandler<
        GetProductMaterialRequirementsQuery,
        Result<MaterialRequirementsResponse>> {
    public async ValueTask<Result<MaterialRequirementsResponse>> Handle(
        GetProductMaterialRequirementsQuery query,
        CancellationToken cancellationToken) {
        if (query.Quantity <= 0) {
            return Result<MaterialRequirementsResponse>.Failure(BomErrors.RequestedQuantityInvalid);
        }

        var product = await productRepository.GetByIdAsync(
            query.ProductId,
            currentUser.CompanyId,
            cancellationToken);
        if (product is null) {
            return Result<MaterialRequirementsResponse>.Failure(ProductErrors.NotFound);
        }

        var bom = await repository.GetActiveAsync(
            product.Id,
            currentUser.CompanyId,
            cancellationToken);
        if (bom is null) {
            return Result<MaterialRequirementsResponse>.Failure(BomErrors.ActiveNotFound);
        }

        var availability = await repository.GetAvailableQuantitiesAsync(
            currentUser.CompanyId,
            bom.Items.Select(item => item.MaterialId).ToList(),
            cancellationToken);
        return Result<MaterialRequirementsResponse>.Success(
            calculator.Calculate(bom, query.Quantity, availability));
    }
}
