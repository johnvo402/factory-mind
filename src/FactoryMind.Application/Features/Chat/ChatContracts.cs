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
    DateTime CreatedAt,
    IReadOnlyList<CitationResponse> Citations);

public sealed record CitationResponse(
    int ReferenceNumber,
    Guid DocumentId,
    Guid ChunkId,
    string DocumentTitle,
    string FileName,
    int PageNumber,
    string Excerpt,
    double Score);

public sealed record ChatPromptMessage(string Role, string Content);

public abstract record ChatStreamUpdate;

public sealed record ChatTokenUpdate(string Content) : ChatStreamUpdate;

public sealed record ChatCitationsUpdate(IReadOnlyList<CitationResponse> Citations) : ChatStreamUpdate;

public sealed record ChatStream(Guid ConversationId, IAsyncEnumerable<ChatStreamUpdate> Updates);

public sealed record KnowledgeContext(
    string Prompt,
    IReadOnlyList<CitationResponse> Sources);

public interface IKnowledgeContextBuilder {
    Task<KnowledgeContext> BuildAsync(
        Guid companyId,
        string question,
        CancellationToken cancellationToken);
}

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
