using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.BusinessData;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Warehouses.CreateWarehouse;

public sealed class CreateWarehouseCommandHandler(
    IWarehouseRepository repository,
    ICurrentUser currentUser) : IRequestHandler<CreateWarehouseCommand, Result<WarehouseResponse>> {
    public async ValueTask<Result<WarehouseResponse>> Handle(
        CreateWarehouseCommand command,
        CancellationToken cancellationToken) {
        var code = BusinessDataNormalization.Code(command.Code);
        if (await repository.CodeExistsAsync(currentUser.CompanyId, code, null, cancellationToken)) {
            return Result<WarehouseResponse>.Failure(WarehouseErrors.CodeAlreadyExists);
        }
        var now = DateTime.UtcNow;
        var warehouse = new Warehouse {
            CompanyId = currentUser.CompanyId,
            Code = code,
            Name = BusinessDataNormalization.Name(command.Name),
            Description = NormalizeOptional(command.Description),
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        repository.Add(warehouse);
        await repository.SaveChangesAsync(cancellationToken);
        return Result<WarehouseResponse>.Success(WarehouseResponse.From(warehouse));
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
