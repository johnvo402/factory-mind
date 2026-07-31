using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.BusinessData;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Materials.CreateMaterial;

public sealed class CreateMaterialCommandHandler(
    IMaterialRepository repository,
    ICurrentUser currentUser) : IRequestHandler<CreateMaterialCommand, Result<MaterialResponse>> {
    public async ValueTask<Result<MaterialResponse>> Handle(
        CreateMaterialCommand command,
        CancellationToken cancellationToken) {
        var code = BusinessDataNormalization.Code(command.Code);
        if (await repository.CodeExistsAsync(currentUser.CompanyId, code, null, cancellationToken)) {
            return Result<MaterialResponse>.Failure(MaterialErrors.CodeAlreadyExists);
        }

        var now = DateTime.UtcNow;
        var material = new Material {
            CompanyId = currentUser.CompanyId,
            Code = code,
            Name = BusinessDataNormalization.Name(command.Name),
            Unit = command.Unit.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };
        repository.Add(material);
        await repository.SaveChangesAsync(cancellationToken);
        return Result<MaterialResponse>.Success(MaterialResponse.From(material));
    }
}
