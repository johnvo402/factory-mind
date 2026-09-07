using System.Text.Json;
using FactoryMind.Domain.Chat;
using FactoryMind.Application.Features.AiActions;
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

public sealed record AiActionProposalUpdate(AiActionProposalResponse Proposal) : ChatStreamUpdate;

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
    WorkCenters = 32,
    Routings = 64,
    ProductionOperations = 128,
    All = Machines | Materials | Inventory | Products | ProductionOrders
        | WorkCenters | Routings | ProductionOperations
}

public sealed record IntentRoute(
    ChatIntent Intent,
    BusinessDataScope BusinessScopes,
    string? MachineStatus = null,
    string? ProductionOrderStatus = null,
    bool IsFallback = false);

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
        string question,
        BusinessDataScope scopes,
        string? machineStatus,
        string? productionOrderStatus,
        int limitPerScope,
        CancellationToken cancellationToken);
}

public interface IBusinessContextBuilder {
    Task<BusinessContext> BuildAsync(
        Guid companyId,
        string question,
        IntentRoute route,
        CancellationToken cancellationToken);

    Task<BusinessContext> BuildAsync(
        Guid companyId,
        string question,
        IntentRoute route,
        IReadOnlyList<BusinessDataRecord> priorityRecords,
        CancellationToken cancellationToken) =>
        BuildAsync(companyId, question, route, cancellationToken);
}

public interface IChatContextBuilder {
    Task<ChatContext> BuildAsync(
        Guid companyId,
        string question,
        CancellationToken cancellationToken);

    Task<ChatContext> BuildAsync(
        Guid companyId,
        string question,
        IReadOnlyList<BusinessDataRecord> priorityRecords,
        CancellationToken cancellationToken) =>
        BuildAsync(companyId, question, cancellationToken);
}

public sealed record AiToolDefinition(string Name, string Description, JsonElement Parameters);

public sealed record AiToolCall(string Name, JsonElement Arguments);

public sealed record AiToolPlan(IReadOnlyList<AiToolCall> Calls);

public interface IAiToolPlanner {
    Task<AiToolPlan> PlanAsync(
        IReadOnlyList<ChatPromptMessage> messages,
        IReadOnlyList<AiToolDefinition> tools,
        CancellationToken cancellationToken);
}

public static class ToolExecutionStatuses {
    public const string Success = "success";
    public const string NotFound = "not_found";
    public const string InvalidArguments = "invalid_arguments";
    public const string NotApplicable = "not_applicable";
    public const string Error = "error";
    public const string UnknownTool = "unknown_tool";
}

public sealed record ToolExecutionResult(
    string Status,
    IReadOnlyList<BusinessDataRecord> Records,
    string? ErrorCode = null);

public interface IManufacturingReadTool {
    AiToolDefinition Definition { get; }

    Task<ToolExecutionResult> ExecuteAsync(
        Guid companyId,
        JsonElement arguments,
        CancellationToken cancellationToken);
}

public interface IManufacturingToolRegistry {
    IReadOnlyList<AiToolDefinition> Definitions { get; }

    Task<ToolExecutionResult> ExecuteAsync(
        Guid companyId,
        AiToolCall call,
        CancellationToken cancellationToken);
}

public interface IAiToolOrchestrator {
    Task<IReadOnlyList<BusinessDataRecord>> CollectAsync(
        Guid companyId,
        string question,
        IReadOnlyList<ChatPromptMessage> recentMessages,
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
