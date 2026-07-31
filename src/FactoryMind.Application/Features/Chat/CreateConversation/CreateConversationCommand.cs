using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Chat.CreateConversation;

public sealed record CreateConversationCommand(string? Title)
    : IRequest<Result<ConversationResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Authenticated;
}
