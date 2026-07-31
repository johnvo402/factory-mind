using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.BusinessData;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Inventories.GetInventories;

public sealed class GetInventoriesQueryHandler(
    IInventoryRepository repository,
    ICurrentUser currentUser) : IRequestHandler<GetInventoriesQuery, Result<IReadOnlyList<InventoryResponse>>> {
    public async ValueTask<Result<IReadOnlyList<InventoryResponse>>> Handle(
        GetInventoriesQuery query,
        CancellationToken cancellationToken) {
        var inventories = await repository.GetByCompanyAsync(
            currentUser.CompanyId,
            BusinessDataNormalization.Search(query.Search),
            cancellationToken);
        return Result<IReadOnlyList<InventoryResponse>>.Success(
            inventories.Select(InventoryResponse.From).ToList());
    }
}
