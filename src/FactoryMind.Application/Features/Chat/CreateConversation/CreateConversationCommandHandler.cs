using FactoryMind.Application.Common.Identity;
using FactoryMind.Domain.Chat;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Chat.CreateConversation;

public sealed class CreateConversationCommandHandler(
    IConversationRepository repository,
    ICurrentUser currentUser) : IRequestHandler<CreateConversationCommand, Result<ConversationResponse>> {
    public async ValueTask<Result<ConversationResponse>> Handle(
        CreateConversationCommand command,
        CancellationToken cancellationToken) {
        var now = DateTime.UtcNow;
        var conversation = new Conversation {
            CompanyId = currentUser.CompanyId,
            UserId = currentUser.UserId,
            Title = NormalizeTitle(command.Title),
            CreatedAt = now,
            UpdatedAt = now
        };

        repository.AddConversation(conversation);
        await repository.SaveChangesAsync(cancellationToken);

        return Result<ConversationResponse>.Success(ToResponse(conversation));
    }

    private static string NormalizeTitle(string? title) => string.IsNullOrWhiteSpace(title)
        ? Conversation.DefaultTitle
        : title.Trim();

    private static ConversationResponse ToResponse(Conversation conversation) => new(
        conversation.Id,
        conversation.Title,
        conversation.CreatedAt,
        conversation.UpdatedAt);
}
