using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Chat.SendMessage;

public sealed record SendMessageCommand(Guid ConversationId, string Content)
    : IRequest<Result<ChatStream>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Authenticated;
}

public sealed record SendMessageRequest(string Content);
