using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.BusinessData;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Products.GetProducts;

public sealed class GetProductsQueryHandler(
    IProductRepository repository,
    ICurrentUser currentUser) : IRequestHandler<GetProductsQuery, Result<IReadOnlyList<ProductResponse>>> {
    public async ValueTask<Result<IReadOnlyList<ProductResponse>>> Handle(
        GetProductsQuery query,
        CancellationToken cancellationToken) {
        var products = await repository.GetByCompanyAsync(
            currentUser.CompanyId,
            BusinessDataNormalization.Search(query.Search),
            cancellationToken);
        return Result<IReadOnlyList<ProductResponse>>.Success(
            products.Select(ProductResponse.From).ToList());
    }
}
