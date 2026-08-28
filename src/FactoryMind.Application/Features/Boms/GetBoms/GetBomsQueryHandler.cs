using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.Products;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Boms.GetBoms;

public sealed class GetBomsQueryHandler(
    IBomRepository repository,
    IProductRepository productRepository,
    ICurrentUser currentUser) : IRequestHandler<GetBomsQuery, Result<IReadOnlyList<BomResponse>>> {
    public async ValueTask<Result<IReadOnlyList<BomResponse>>> Handle(
        GetBomsQuery query,
        CancellationToken cancellationToken) {
        var product = await productRepository.GetByIdAsync(
            query.ProductId,
            currentUser.CompanyId,
            cancellationToken);
        if (product is null) {
            return Result<IReadOnlyList<BomResponse>>.Failure(ProductErrors.NotFound);
        }

        var boms = await repository.GetByProductAsync(
            product.Id,
            currentUser.CompanyId,
            cancellationToken);
        return Result<IReadOnlyList<BomResponse>>.Success(boms.Select(BomResponse.From).ToList());
    }
}
