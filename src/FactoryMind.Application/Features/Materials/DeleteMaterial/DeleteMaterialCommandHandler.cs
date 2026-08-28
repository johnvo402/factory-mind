using FactoryMind.Application.Common.Identity;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Materials.DeleteMaterial;

public sealed class DeleteMaterialCommandHandler(
    IMaterialRepository repository,
    ICurrentUser currentUser) : IRequestHandler<DeleteMaterialCommand, Result> {
    public async ValueTask<Result> Handle(
        DeleteMaterialCommand command,
        CancellationToken cancellationToken) {
        var material = await repository.GetByIdAsync(
            command.MaterialId,
            currentUser.CompanyId,
            cancellationToken);
        if (material is null) {
            return Result.Failure(MaterialErrors.NotFound);
        }

        if (await repository.HasBomItemsAsync(
                material.Id,
                currentUser.CompanyId,
                cancellationToken)) {
            return Result.Failure(MaterialErrors.ReferencedByBom);
        }

        repository.Remove(material);
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
