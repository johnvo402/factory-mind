using FactoryMind.Application.Common.Identity;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Boms.ArchiveBom;

public sealed class ArchiveBomCommandHandler(
    IBomRepository repository,
    ICurrentUser currentUser) : IRequestHandler<ArchiveBomCommand, Result<BomResponse>> {
    public async ValueTask<Result<BomResponse>> Handle(
        ArchiveBomCommand command,
        CancellationToken cancellationToken) {
        var bom = await repository.GetByIdAsync(
            command.BomId,
            command.ProductId,
            currentUser.CompanyId,
            cancellationToken);
        if (bom is null) {
            return Result<BomResponse>.Failure(BomErrors.NotFound);
        }

        if (bom.Status == BillOfMaterialStatuses.Archived) {
            return Result<BomResponse>.Failure(BomErrors.AlreadyArchived);
        }

        bom.Status = BillOfMaterialStatuses.Archived;
        bom.UpdatedAt = DateTime.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);
        return Result<BomResponse>.Success(BomResponse.From(bom));
    }
}
