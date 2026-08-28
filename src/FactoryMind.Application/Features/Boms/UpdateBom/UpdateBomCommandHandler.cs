using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.Materials;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Boms.UpdateBom;

public sealed class UpdateBomCommandHandler(
    IBomRepository repository,
    IMaterialRepository materialRepository,
    ICurrentUser currentUser) : IRequestHandler<UpdateBomCommand, Result<BomResponse>> {
    public async ValueTask<Result<BomResponse>> Handle(
        UpdateBomCommand command,
        CancellationToken cancellationToken) {
        var validationError = BomSpecificationValidation.Validate(command.OutputQuantity, command.Items);
        if (validationError is not null) {
            return Result<BomResponse>.Failure(validationError);
        }

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

        var resolvedMaterials = new Dictionary<Guid, Material>();
        foreach (var definition in command.Items) {
            var material = await materialRepository.GetByIdAsync(
                definition.MaterialId,
                currentUser.CompanyId,
                cancellationToken);
            if (material is null) {
                return Result<BomResponse>.Failure(BomErrors.MaterialNotFound);
            }

            resolvedMaterials.Add(material.Id, material);
        }

        var now = DateTime.UtcNow;
        bom.OutputQuantity = command.OutputQuantity;
        bom.UpdatedAt = now;
        bom.Items.Clear();
        foreach (var definition in command.Items) {
            bom.Items.Add(new BomItem {
                BillOfMaterialId = bom.Id,
                BillOfMaterial = bom,
                MaterialId = definition.MaterialId,
                Material = resolvedMaterials[definition.MaterialId],
                Quantity = definition.Quantity,
                ScrapPercentage = definition.ScrapPercentage,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result<BomResponse>.Success(BomResponse.From(bom));
    }
}
