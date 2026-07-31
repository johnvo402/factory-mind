using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Chat.GetConversations;

public sealed record GetConversationsQuery
    : IRequest<Result<IReadOnlyList<ConversationResponse>>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Authenticated;
}
