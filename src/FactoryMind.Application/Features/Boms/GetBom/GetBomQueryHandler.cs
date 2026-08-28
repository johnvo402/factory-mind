using FactoryMind.Application.Common.Identity;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Boms.GetBom;

public sealed class GetBomQueryHandler(
    IBomRepository repository,
    ICurrentUser currentUser) : IRequestHandler<GetBomQuery, Result<BomResponse>> {
    public async ValueTask<Result<BomResponse>> Handle(
        GetBomQuery query,
        CancellationToken cancellationToken) {
        var bom = await repository.GetByIdAsync(
            query.BomId,
            query.ProductId,
            currentUser.CompanyId,
            cancellationToken);
        return bom is null
            ? Result<BomResponse>.Failure(BomErrors.NotFound)
            : Result<BomResponse>.Success(BomResponse.From(bom));
    }
}
