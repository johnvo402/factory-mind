using FactoryMind.Application.Common.Identity;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Chat.GetConversations;

public sealed class GetConversationsQueryHandler(
    IConversationRepository repository,
    ICurrentUser currentUser) : IRequestHandler<GetConversationsQuery, Result<IReadOnlyList<ConversationResponse>>> {
    public async ValueTask<Result<IReadOnlyList<ConversationResponse>>> Handle(
        GetConversationsQuery query,
        CancellationToken cancellationToken) {
        var conversations = await repository.GetOwnedConversationsAsync(
            currentUser.CompanyId,
            currentUser.UserId,
            cancellationToken);
        var response = conversations
            .Select(conversation => new ConversationResponse(
                conversation.Id,
                conversation.Title,
                conversation.CreatedAt,
                conversation.UpdatedAt))
            .ToList();

        return Result<IReadOnlyList<ConversationResponse>>.Success(response);
    }
}
