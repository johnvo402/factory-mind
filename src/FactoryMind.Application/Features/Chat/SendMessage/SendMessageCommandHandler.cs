using System.Runtime.CompilerServices;
using System.Text;
using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.Chat.Rag;
using FactoryMind.Domain.Chat;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Chat.SendMessage;

public sealed class SendMessageCommandHandler(
    IConversationRepository repository,
    IChatCompletionClient chatClient,
    IKnowledgeContextBuilder contextBuilder,
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
        var knowledgeContext = await contextBuilder.BuildAsync(
            currentUser.CompanyId,
            content,
            cancellationToken);
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

        var prompt = new List<ChatPromptMessage> {
            new(ChatRoles.System, knowledgeContext.Prompt)
        };
        prompt.AddRange(existingMessages
            .TakeLast(KnowledgeContextBuilder.MaximumHistoryMessages)
            .Select(message => new ChatPromptMessage(message.Role, message.Content)));
        prompt.Add(new ChatPromptMessage(ChatRoles.User, content));

        var updates = StreamAndPersistAsync(
            conversation,
            prompt,
            knowledgeContext.Sources);
        var stream = new ChatStream(conversation.Id, updates);
        return Result<ChatStream>.Success(stream);
    }

    private async IAsyncEnumerable<ChatStreamUpdate> StreamAndPersistAsync(
        Conversation conversation,
        IReadOnlyList<ChatPromptMessage> prompt,
        IReadOnlyList<CitationResponse> sources,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        var answer = new StringBuilder();

        await foreach (var token in chatClient.StreamAsync(prompt, cancellationToken)) {
            answer.Append(token);
            yield return new ChatTokenUpdate(token);
        }

        if (answer.Length == 0) {
            yield return new ChatCitationsUpdate([]);
            yield break;
        }

        var answerContent = answer.ToString();
        var citedSources = sources
            .Where(source => answerContent.Contains(
                $"[S{source.ReferenceNumber}]",
                StringComparison.OrdinalIgnoreCase))
            .ToList();
        conversation.UpdatedAt = DateTime.UtcNow;
        var assistantMessage = new ChatMessage {
            ConversationId = conversation.Id,
            Role = ChatRoles.Assistant,
            Content = answerContent,
            CreatedAt = conversation.UpdatedAt
        };
        foreach (var source in citedSources) {
            assistantMessage.Citations.Add(new ChatCitation {
                ReferenceNumber = source.ReferenceNumber,
                DocumentId = source.DocumentId,
                ChunkId = source.ChunkId,
                DocumentTitle = source.DocumentTitle,
                FileName = source.FileName,
                PageNumber = source.PageNumber,
                Excerpt = source.Excerpt,
                Score = source.Score,
                CreatedAt = assistantMessage.CreatedAt
            });
        }

        repository.AddMessage(assistantMessage);
        await repository.SaveChangesAsync(cancellationToken);
        yield return new ChatCitationsUpdate(citedSources);
    }

    private static string CreateTitle(string content) {
        var normalized = string.Join(' ', content.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return normalized.Length <= 80 ? normalized : $"{normalized[..77]}...";
    }
}
