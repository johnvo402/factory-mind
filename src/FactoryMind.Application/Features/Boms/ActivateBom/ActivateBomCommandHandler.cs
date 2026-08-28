using FactoryMind.Application.Common.Identity;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Boms.ActivateBom;

public sealed class ActivateBomCommandHandler(
    IBomRepository repository,
    ICurrentUser currentUser) : IRequestHandler<ActivateBomCommand, Result<BomResponse>> {
    public async ValueTask<Result<BomResponse>> Handle(
        ActivateBomCommand command,
        CancellationToken cancellationToken) {
        var bom = await repository.GetByIdAsync(
            command.BomId,
            command.ProductId,
            currentUser.CompanyId,
            cancellationToken);
        if (bom is null) {
            return Result<BomResponse>.Failure(BomErrors.NotFound);
        }

        if (bom.Status != BillOfMaterialStatuses.Draft) {
            return Result<BomResponse>.Failure(BomErrors.DraftRequired);
        }

        if (bom.Items.Count == 0) {
            return Result<BomResponse>.Failure(BomErrors.ItemsRequired);
        }

        await repository.ActivateAsync(bom, DateTime.UtcNow, cancellationToken);
        return Result<BomResponse>.Success(BomResponse.From(bom));
    }
}
