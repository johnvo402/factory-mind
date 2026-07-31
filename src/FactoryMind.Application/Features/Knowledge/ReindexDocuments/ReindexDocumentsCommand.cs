using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Knowledge.ReindexDocuments;

public sealed record ReindexDocumentsCommand : IRequest<Result<ReindexDocumentsResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}

public sealed record ReindexDocumentsResponse(int QueuedCount);
