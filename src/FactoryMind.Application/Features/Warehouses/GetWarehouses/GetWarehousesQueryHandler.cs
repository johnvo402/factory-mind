using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.BusinessData;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Warehouses.GetWarehouses;

public sealed class GetWarehousesQueryHandler(
    IWarehouseRepository repository,
    ICurrentUser currentUser)
    : IRequestHandler<GetWarehousesQuery, Result<IReadOnlyList<WarehouseResponse>>> {
    public async ValueTask<Result<IReadOnlyList<WarehouseResponse>>> Handle(
        GetWarehousesQuery query,
        CancellationToken cancellationToken) {
        var warehouses = await repository.GetByCompanyAsync(
            currentUser.CompanyId,
            BusinessDataNormalization.Search(query.Search),
            cancellationToken);
        return Result<IReadOnlyList<WarehouseResponse>>.Success(
            warehouses.Select(WarehouseResponse.From).ToList());
    }
}
