using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.Materials;
using FactoryMind.Application.Features.Products;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Boms.CreateBom;

public sealed class CreateBomCommandHandler(
    IBomRepository repository,
    IProductRepository productRepository,
    IMaterialRepository materialRepository,
    ICurrentUser currentUser) : IRequestHandler<CreateBomCommand, Result<BomResponse>> {
    public async ValueTask<Result<BomResponse>> Handle(
        CreateBomCommand command,
        CancellationToken cancellationToken) {
        var validationError = BomSpecificationValidation.Validate(command.OutputQuantity, command.Items);
        if (validationError is not null) {
            return Result<BomResponse>.Failure(validationError);
        }

        var product = await productRepository.GetByIdAsync(
            command.ProductId,
            currentUser.CompanyId,
            cancellationToken);
        if (product is null) {
            return Result<BomResponse>.Failure(ProductErrors.NotFound);
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
        var bom = new BillOfMaterial {
            CompanyId = currentUser.CompanyId,
            ProductId = product.Id,
            Product = product,
            Revision = await repository.GetNextRevisionAsync(
                product.Id,
                currentUser.CompanyId,
                cancellationToken),
            OutputQuantity = command.OutputQuantity,
            Status = BillOfMaterialStatuses.Draft,
            CreatedAt = now,
            UpdatedAt = now
        };
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

        repository.Add(bom);
        await repository.SaveChangesAsync(cancellationToken);
        return Result<BomResponse>.Success(BomResponse.From(bom));
    }
}
