using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.ExcelImports.ImportExcel;

public sealed record ImportExcelCommand(
    string EntityType,
    IReadOnlyDictionary<string, string> Mapping,
    Stream Content) : IRequest<Result<ExcelImportResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
