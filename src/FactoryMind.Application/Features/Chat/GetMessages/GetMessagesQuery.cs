using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Chat.GetMessages;

public sealed record GetMessagesQuery(Guid ConversationId)
    : IRequest<Result<IReadOnlyList<MessageResponse>>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Authenticated;
}
