using FactoryMind.Application.Common.Identity;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Warehouses.GetWarehouse;

public sealed class GetWarehouseQueryHandler(
    IWarehouseRepository repository,
    ICurrentUser currentUser) : IRequestHandler<GetWarehouseQuery, Result<WarehouseResponse>> {
    public async ValueTask<Result<WarehouseResponse>> Handle(
        GetWarehouseQuery query,
        CancellationToken cancellationToken) {
        var warehouse = await repository.GetByIdAsync(
            query.WarehouseId, currentUser.CompanyId, cancellationToken);
        return warehouse is null
            ? Result<WarehouseResponse>.Failure(WarehouseErrors.NotFound)
            : Result<WarehouseResponse>.Success(WarehouseResponse.From(warehouse));
    }
}
