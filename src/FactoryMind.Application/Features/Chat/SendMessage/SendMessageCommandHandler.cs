using System.Runtime.CompilerServices;
using System.Text;
using FactoryMind.Application.Common.Identity;
using FactoryMind.Domain.Chat;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Chat.SendMessage;

public sealed class SendMessageCommandHandler(
    IConversationRepository repository,
    IChatCompletionClient chatClient,
    ICurrentUser currentUser) : IRequestHandler<SendMessageCommand, Result<ChatStream>> {
    public async ValueTask<Result<ChatStream>> Handle(
        SendMessageCommand command,
        CancellationToken cancellationToken) {
        var conversation = await repository.GetOwnedConversationAsync(
            command.ConversationId,
            currentUser.CompanyId,
            currentUser.UserId,
            cancellationToken);
        if (conversation is null) {
            return Result<ChatStream>.Failure(ChatErrors.ConversationNotFound);
        }

        var existingMessages = await repository.GetOwnedMessagesAsync(
            conversation.Id,
            currentUser.CompanyId,
            currentUser.UserId,
            cancellationToken);
        var content = command.Content.Trim();
        var now = DateTime.UtcNow;

        if (existingMessages.Count == 0 && conversation.Title == Conversation.DefaultTitle) {
            conversation.Title = CreateTitle(content);
        }

        conversation.UpdatedAt = now;
        repository.AddMessage(new ChatMessage {
            ConversationId = conversation.Id,
            Role = ChatRoles.User,
            Content = content,
            CreatedAt = now
        });
        await repository.SaveChangesAsync(cancellationToken);

        var prompt = existingMessages
            .Select(message => new ChatPromptMessage(message.Role, message.Content))
            .Append(new ChatPromptMessage(ChatRoles.User, content))
            .ToList();
        var stream = new ChatStream(conversation.Id, StreamAndPersistAsync(conversation, prompt));
        return Result<ChatStream>.Success(stream);
    }

    private async IAsyncEnumerable<string> StreamAndPersistAsync(
        Conversation conversation,
        IReadOnlyList<ChatPromptMessage> prompt,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        var answer = new StringBuilder();

        await foreach (var token in chatClient.StreamAsync(prompt, cancellationToken)) {
            answer.Append(token);
            yield return token;
        }

        if (answer.Length == 0) {
            yield break;
        }

        conversation.UpdatedAt = DateTime.UtcNow;
        repository.AddMessage(new ChatMessage {
            ConversationId = conversation.Id,
            Role = ChatRoles.Assistant,
            Content = answer.ToString(),
            CreatedAt = conversation.UpdatedAt
        });
        await repository.SaveChangesAsync(cancellationToken);
    }

    private static string CreateTitle(string content) {
        var normalized = string.Join(' ', content.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return normalized.Length <= 80 ? normalized : $"{normalized[..77]}...";
    }
}
