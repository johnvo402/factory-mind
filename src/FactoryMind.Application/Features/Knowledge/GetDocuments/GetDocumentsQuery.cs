using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Knowledge.GetDocuments;

public sealed record GetDocumentsQuery
    : IRequest<Result<IReadOnlyList<DocumentResponse>>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Authenticated;
}
