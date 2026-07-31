using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.ExcelImports.PreviewExcelImport;

public sealed record PreviewExcelImportCommand(
    string EntityType,
    Stream Content) : IRequest<Result<ExcelPreviewResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
