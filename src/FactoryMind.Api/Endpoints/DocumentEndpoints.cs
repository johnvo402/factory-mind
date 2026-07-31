using FactoryMind.Application.Common.Authorization;
using FactoryMind.Application.Features.Knowledge;
using FactoryMind.Application.Features.Knowledge.GetDocuments;
using FactoryMind.Application.Features.Knowledge.QueueDocumentProcessing;
using FactoryMind.Application.Features.Knowledge.UploadDocument;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace FactoryMind.Api.Endpoints;

public static class DocumentEndpoints {
    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup("/api/documents")
            .RequireAuthorization(AuthorizationPolicies.Authenticated);

        group.MapPost("", async (
            [FromForm] UploadDocumentForm form,
            ISender sender,
            CancellationToken cancellationToken) => {
            await using var content = form.File.OpenReadStream();
            var command = new UploadDocumentCommand(
                form.Title,
                form.File.FileName,
                form.File.ContentType,
                form.File.Length,
                content);
            return (await sender.Send(command, cancellationToken)).ToHttpResult();
        })
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(DocumentUploadConstraints.MaximumRequestSize))
            .WithRequestValidation<UploadDocumentForm>();

        group.MapGet("", async (ISender sender, CancellationToken cancellationToken) => {
            return (await sender.Send(new GetDocumentsQuery(), cancellationToken)).ToHttpResult();
        });

        group.MapPost("/{documentId:guid}/process", async (
            Guid documentId,
            ISender sender,
            CancellationToken cancellationToken) => {
            return (await sender.Send(
                new QueueDocumentProcessingCommand(documentId),
                cancellationToken)).ToHttpResult();
        });

        return endpoints;
    }
}
