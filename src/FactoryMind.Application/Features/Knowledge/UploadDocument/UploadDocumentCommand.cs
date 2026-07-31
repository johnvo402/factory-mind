using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Knowledge.UploadDocument;

public sealed record UploadDocumentCommand(
    string? Title,
    string FileName,
    string ContentType,
    long Length,
    Stream Content) : IRequest<Result<DocumentResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Authenticated;
}
