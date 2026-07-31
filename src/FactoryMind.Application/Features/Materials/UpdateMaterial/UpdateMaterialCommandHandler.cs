using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.BusinessData;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Materials.UpdateMaterial;

public sealed class UpdateMaterialCommandHandler(
    IMaterialRepository repository,
    ICurrentUser currentUser) : IRequestHandler<UpdateMaterialCommand, Result<MaterialResponse>> {
    public async ValueTask<Result<MaterialResponse>> Handle(
        UpdateMaterialCommand command,
        CancellationToken cancellationToken) {
        var material = await repository.GetByIdAsync(
            command.MaterialId,
            currentUser.CompanyId,
            cancellationToken);
        if (material is null) {
            return Result<MaterialResponse>.Failure(MaterialErrors.NotFound);
        }

        var code = BusinessDataNormalization.Code(command.Code);
        if (await repository.CodeExistsAsync(
                currentUser.CompanyId,
                code,
                material.Id,
                cancellationToken)) {
            return Result<MaterialResponse>.Failure(MaterialErrors.CodeAlreadyExists);
        }

        material.Code = code;
        material.Name = BusinessDataNormalization.Name(command.Name);
        material.Unit = command.Unit.Trim();
        material.UpdatedAt = DateTime.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);
        return Result<MaterialResponse>.Success(MaterialResponse.From(material));
    }
}
