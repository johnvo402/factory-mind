using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Knowledge.QueueDocumentProcessing;

public sealed record QueueDocumentProcessingCommand(
    Guid DocumentId) : IRequest<Result<DocumentResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Authenticated;
}
