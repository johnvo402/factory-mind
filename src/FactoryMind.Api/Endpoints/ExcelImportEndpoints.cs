using System.Text.Json;
using FactoryMind.Application.Common.Authorization;
using FactoryMind.Application.Features.ExcelImports;
using FactoryMind.Application.Features.ExcelImports.ImportExcel;
using FactoryMind.Application.Features.ExcelImports.PreviewExcelImport;
using FactoryMind.Api.Routing;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace FactoryMind.Api.Endpoints;

public static class ExcelImportEndpoints {
    public static IEndpointRouteBuilder MapExcelImportEndpoints(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup(ApiRoutes.ExcelImports.Group)
            .RequireAuthorization(AuthorizationPolicies.Manager);

        group.MapPost(ApiRoutes.ExcelImports.Preview, async (
            [FromForm] PreviewExcelImportForm form,
            ISender sender,
            CancellationToken cancellationToken) => {
                await using var content = form.File.OpenReadStream();
                return (await sender.Send(
                    new PreviewExcelImportCommand(form.EntityType, content),
                    cancellationToken)).ToHttpResult();
            })
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(ExcelImportConstraints.MaximumRequestSize))
            .WithRequestValidation<PreviewExcelImportForm>();

        group.MapPost(ApiRoutes.ExcelImports.Import, async (
            [FromForm] ImportExcelForm form,
            ISender sender,
            CancellationToken cancellationToken) => {
                var mapping = DeserializeMapping(form.Mapping);
                await using var content = form.File.OpenReadStream();
                return (await sender.Send(
                    new ImportExcelCommand(form.EntityType, mapping, content),
                    cancellationToken)).ToHttpResult();
            })
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(ExcelImportConstraints.MaximumRequestSize))
            .WithRequestValidation<ImportExcelForm>();

        return endpoints;
    }

    private static IReadOnlyDictionary<string, string> DeserializeMapping(string json) {
        try {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                ?? new Dictionary<string, string>();
        } catch (JsonException) {
            return new Dictionary<string, string>();
        }
    }
}
