using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.BusinessData;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Materials.GetMaterials;

public sealed class GetMaterialsQueryHandler(
    IMaterialRepository repository,
    ICurrentUser currentUser) : IRequestHandler<GetMaterialsQuery, Result<IReadOnlyList<MaterialResponse>>> {
    public async ValueTask<Result<IReadOnlyList<MaterialResponse>>> Handle(
        GetMaterialsQuery query,
        CancellationToken cancellationToken) {
        var materials = await repository.GetByCompanyAsync(
            currentUser.CompanyId,
            BusinessDataNormalization.Search(query.Search),
            cancellationToken);
        return Result<IReadOnlyList<MaterialResponse>>.Success(
            materials.Select(MaterialResponse.From).ToList());
    }
}
