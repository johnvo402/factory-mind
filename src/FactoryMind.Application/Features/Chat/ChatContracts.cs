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
    IReadOnlyList<CitationResponse> Citations,
    IReadOnlyList<BusinessEvidenceResponse> BusinessEvidence);

public sealed record CitationResponse(
    int ReferenceNumber,
    Guid DocumentId,
    Guid ChunkId,
    string DocumentTitle,
    string FileName,
    int PageNumber,
    string Excerpt,
    double Score);

public sealed record BusinessEvidenceResponse(
    int ReferenceNumber,
    Guid EntityId,
    string EntityType,
    string Title,
    string Detail);

public sealed record ChatPromptMessage(string Role, string Content);

public abstract record ChatStreamUpdate;

public sealed record ChatTokenUpdate(string Content) : ChatStreamUpdate;

public sealed record ChatCitationsUpdate(IReadOnlyList<CitationResponse> Citations) : ChatStreamUpdate;

public sealed record ChatBusinessEvidenceUpdate(
    IReadOnlyList<BusinessEvidenceResponse> BusinessEvidence) : ChatStreamUpdate;

public sealed record ChatStream(Guid ConversationId, IAsyncEnumerable<ChatStreamUpdate> Updates);

public sealed record KnowledgeContext(
    string Prompt,
    IReadOnlyList<CitationResponse> Sources);

public sealed record BusinessContext(
    string Prompt,
    IReadOnlyList<BusinessEvidenceResponse> Evidence);

public sealed record ChatContext(
    string Prompt,
    IReadOnlyList<CitationResponse> Sources,
    IReadOnlyList<BusinessEvidenceResponse> BusinessEvidence);

public enum ChatIntent {
    Business,
    Knowledge,
    Hybrid
}

[Flags]
public enum BusinessDataScope {
    None = 0,
    Machines = 1,
    Materials = 2,
    Inventory = 4,
    Products = 8,
    ProductionOrders = 16,
    All = Machines | Materials | Inventory | Products | ProductionOrders
}

public sealed record IntentRoute(
    ChatIntent Intent,
    BusinessDataScope BusinessScopes,
    string? MachineStatus = null,
    string? ProductionOrderStatus = null);

public sealed record BusinessDataRecord(
    Guid EntityId,
    string EntityType,
    string Title,
    string Detail);

public interface IIntentRouter {
    IntentRoute Route(string question);
}

public interface IKnowledgeContextBuilder {
    Task<KnowledgeContext> BuildAsync(
        Guid companyId,
        string question,
        CancellationToken cancellationToken);
}

public interface IBusinessContextRepository {
    Task<IReadOnlyList<BusinessDataRecord>> RetrieveAsync(
        Guid companyId,
        BusinessDataScope scopes,
        string? machineStatus,
        string? productionOrderStatus,
        int limitPerScope,
        CancellationToken cancellationToken);
}

public interface IBusinessContextBuilder {
    Task<BusinessContext> BuildAsync(
        Guid companyId,
        IntentRoute route,
        CancellationToken cancellationToken);
}

public interface IChatContextBuilder {
    Task<ChatContext> BuildAsync(
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
