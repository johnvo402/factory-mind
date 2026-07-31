using FactoryMind.Domain.Chat;
using FactoryMind.Shared.Contracts;

namespace FactoryMind.Application.Features.Chat;

public sealed record ConversationResponse(
    Guid Id,
    string Title,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record MessageResponse(
    Guid Id,
    string Role,
    string Content,
    DateTime CreatedAt);

public sealed record ChatPromptMessage(string Role, string Content);

public sealed record ChatStream(Guid ConversationId, IAsyncEnumerable<string> Tokens);

public interface IConversationRepository {
    Task<IReadOnlyList<Conversation>> GetOwnedConversationsAsync(
        Guid companyId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<Conversation?> GetOwnedConversationAsync(
        Guid conversationId,
        Guid companyId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ChatMessage>> GetOwnedMessagesAsync(
        Guid conversationId,
        Guid companyId,
        Guid userId,
        CancellationToken cancellationToken);

    void AddConversation(Conversation conversation);
    void AddMessage(ChatMessage message);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IChatCompletionClient {
    IAsyncEnumerable<string> StreamAsync(
        IReadOnlyList<ChatPromptMessage> messages,
        CancellationToken cancellationToken);
}

public static class ChatErrors {
    public static readonly Error ConversationNotFound = new(
        "chat.conversation_not_found",
        "Conversation was not found.",
        404);
}
