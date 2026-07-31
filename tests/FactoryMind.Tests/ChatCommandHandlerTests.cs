using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.Chat;
using FactoryMind.Application.Features.Chat.CreateConversation;
using FactoryMind.Application.Features.Chat.GetMessages;
using FactoryMind.Application.Features.Chat.SendMessage;
using FactoryMind.Domain.Chat;

namespace FactoryMind.Tests;

public sealed class ChatCommandHandlerTests {
    [Fact]
    public async Task Create_conversation_uses_the_current_tenant_and_user() {
        var currentUser = new FakeCurrentUser();
        var repository = new FakeConversationRepository();
        var handler = new CreateConversationCommandHandler(repository, currentUser);

        var result = await handler.Handle(new CreateConversationCommand("  Daily production  "), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var conversation = Assert.Single(repository.AddedConversations);
        Assert.Equal(currentUser.CompanyId, conversation.CompanyId);
        Assert.Equal(currentUser.UserId, conversation.UserId);
        Assert.Equal("Daily production", conversation.Title);
        Assert.Equal(1, repository.SaveChangesCount);
    }

    [Fact]
    public async Task Send_message_persists_the_question_and_completed_answer() {
        var currentUser = new FakeCurrentUser();
        var conversation = new Conversation {
            CompanyId = currentUser.CompanyId,
            UserId = currentUser.UserId
        };
        var repository = new FakeConversationRepository { OwnedConversation = conversation };
        var source = new CitationResponse(
            1,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Machine manual",
            "manual.pdf",
            4,
            "Machine A is available.",
            0.91);
        var unusedSource = source with {
            ReferenceNumber = 2,
            DocumentId = Guid.NewGuid(),
            ChunkId = Guid.NewGuid(),
            DocumentTitle = "Unused manual"
        };
        var contextBuilder = new FakeKnowledgeContextBuilder(source, unusedSource);
        var chatClient = new FakeChatCompletionClient("Available now", " [S1].");
        var handler = new SendMessageCommandHandler(repository, chatClient, contextBuilder, currentUser);

        var result = await handler.Handle(
            new SendMessageCommand(conversation.Id, "  Which machine is available?  "),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var question = Assert.Single(repository.AddedMessages);
        Assert.Equal(ChatRoles.User, question.Role);
        Assert.Equal("Which machine is available?", question.Content);
        Assert.Equal("Which machine is available?", conversation.Title);

        var updates = new List<ChatStreamUpdate>();
        await foreach (var update in result.Value!.Updates) {
            updates.Add(update);
        }

        Assert.Equal(["Available now", " [S1]."], updates.OfType<ChatTokenUpdate>().Select(update => update.Content));
        var citations = Assert.Single(updates.OfType<ChatCitationsUpdate>()).Citations;
        Assert.Equal(source, Assert.Single(citations));
        var answer = Assert.Single(repository.AddedMessages, message => message.Role == ChatRoles.Assistant);
        Assert.Equal("Available now [S1].", answer.Content);
        var persistedCitation = Assert.Single(answer.Citations);
        Assert.Equal(source.DocumentId, persistedCitation.DocumentId);
        Assert.Equal(source.ChunkId, persistedCitation.ChunkId);
        Assert.Equal(source.ReferenceNumber, persistedCitation.ReferenceNumber);
        Assert.Equal(2, repository.SaveChangesCount);
        Assert.Equal(currentUser.CompanyId, contextBuilder.CompanyId);
        Assert.Equal("Which machine is available?", contextBuilder.Question);
        Assert.Equal(ChatRoles.System, chatClient.Prompt[0].Role);
        Assert.Equal("Knowledge context", chatClient.Prompt[0].Content);
        Assert.Equal(ChatRoles.User, chatClient.Prompt[^1].Role);
    }

    [Fact]
    public async Task Send_message_cannot_access_another_tenant_conversation() {
        var currentUser = new FakeCurrentUser();
        var repository = new FakeConversationRepository();
        var chatClient = new FakeChatCompletionClient("unused");
        var contextBuilder = new FakeKnowledgeContextBuilder();
        var handler = new SendMessageCommandHandler(repository, chatClient, contextBuilder, currentUser);
        var conversationId = Guid.NewGuid();

        var result = await handler.Handle(
            new SendMessageCommand(conversationId, "Question"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("chat.conversation_not_found", result.Error?.Code);
        Assert.Equal((conversationId, currentUser.CompanyId, currentUser.UserId), repository.RequestedConversation);
        Assert.Empty(repository.AddedMessages);
        Assert.Empty(chatClient.Prompt);
        Assert.Equal(0, contextBuilder.BuildCount);
    }

    [Fact]
    public async Task Get_messages_returns_not_found_outside_the_current_owner_scope() {
        var currentUser = new FakeCurrentUser();
        var repository = new FakeConversationRepository();
        var handler = new GetMessagesQueryHandler(repository, currentUser);

        var result = await handler.Handle(new GetMessagesQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(404, result.Error?.StatusCode);
    }

    [Fact]
    public async Task Get_messages_returns_persisted_citation_snapshots() {
        var currentUser = new FakeCurrentUser();
        var conversation = new Conversation {
            CompanyId = currentUser.CompanyId,
            UserId = currentUser.UserId
        };
        var message = new ChatMessage {
            ConversationId = conversation.Id,
            Role = ChatRoles.Assistant,
            Content = "Use lockout procedure [S1]."
        };
        message.Citations.Add(new ChatCitation {
            ReferenceNumber = 1,
            DocumentId = Guid.NewGuid(),
            ChunkId = Guid.NewGuid(),
            DocumentTitle = "Safety manual",
            FileName = "safety.pdf",
            PageNumber = 8,
            Excerpt = "Lock the energy source.",
            Score = 0.89
        });
        var repository = new FakeConversationRepository { OwnedConversation = conversation };
        repository.ExistingMessages.Add(message);
        var handler = new GetMessagesQueryHandler(repository, currentUser);

        var result = await handler.Handle(new GetMessagesQuery(conversation.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var response = Assert.Single(result.Value!);
        var citation = Assert.Single(response.Citations);
        Assert.Equal("Safety manual", citation.DocumentTitle);
        Assert.Equal(8, citation.PageNumber);
    }

    [Fact]
    public async Task Send_message_limits_history_to_the_latest_twenty_messages() {
        var currentUser = new FakeCurrentUser();
        var conversation = new Conversation {
            CompanyId = currentUser.CompanyId,
            UserId = currentUser.UserId,
            Title = "Existing conversation"
        };
        var repository = new FakeConversationRepository { OwnedConversation = conversation };
        for (var index = 0; index < 25; index++) {
            repository.ExistingMessages.Add(new ChatMessage {
                ConversationId = conversation.Id,
                Role = ChatRoles.User,
                Content = $"History {index}"
            });
        }

        var chatClient = new FakeChatCompletionClient("Done");
        var handler = new SendMessageCommandHandler(
            repository,
            chatClient,
            new FakeKnowledgeContextBuilder(),
            currentUser);

        var result = await handler.Handle(
            new SendMessageCommand(conversation.Id, "Current question"),
            CancellationToken.None);
        await foreach (var _ in result.Value!.Updates) {
        }

        Assert.Equal(22, chatClient.Prompt.Count);
        Assert.Equal("Knowledge context", chatClient.Prompt[0].Content);
        Assert.Equal("History 5", chatClient.Prompt[1].Content);
        Assert.Equal("Current question", chatClient.Prompt[^1].Content);
    }

    private sealed class FakeCurrentUser : ICurrentUser {
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid CompanyId { get; } = Guid.NewGuid();
        public string Role => "User";
    }

    private sealed class FakeChatCompletionClient(params string[] tokens) : IChatCompletionClient {
        public IReadOnlyList<ChatPromptMessage> Prompt { get; private set; } = [];

        public async IAsyncEnumerable<string> StreamAsync(
            IReadOnlyList<ChatPromptMessage> messages,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken) {
            Prompt = messages;
            await Task.Yield();

            foreach (var token in tokens) {
                cancellationToken.ThrowIfCancellationRequested();
                yield return token;
            }
        }
    }

    private sealed class FakeKnowledgeContextBuilder(params CitationResponse[] sources)
        : IKnowledgeContextBuilder {
        public Guid? CompanyId { get; private set; }
        public string? Question { get; private set; }
        public int BuildCount { get; private set; }

        public Task<KnowledgeContext> BuildAsync(
            Guid companyId,
            string question,
            CancellationToken cancellationToken) {
            CompanyId = companyId;
            Question = question;
            BuildCount++;
            return Task.FromResult(new KnowledgeContext("Knowledge context", sources));
        }
    }

    private sealed class FakeConversationRepository : IConversationRepository {
        public Conversation? OwnedConversation { get; init; }
        public List<Conversation> AddedConversations { get; } = [];
        public List<ChatMessage> AddedMessages { get; } = [];
        public List<ChatMessage> ExistingMessages { get; } = [];
        public (Guid ConversationId, Guid CompanyId, Guid UserId)? RequestedConversation { get; private set; }
        public int SaveChangesCount { get; private set; }

        public Task<IReadOnlyList<Conversation>> GetOwnedConversationsAsync(
            Guid companyId,
            Guid userId,
            CancellationToken cancellationToken) {
            return Task.FromResult<IReadOnlyList<Conversation>>(
                OwnedConversation is null ? [] : [OwnedConversation]);
        }

        public Task<Conversation?> GetOwnedConversationAsync(
            Guid conversationId,
            Guid companyId,
            Guid userId,
            CancellationToken cancellationToken) {
            RequestedConversation = (conversationId, companyId, userId);
            return Task.FromResult(OwnedConversation?.Id == conversationId ? OwnedConversation : null);
        }

        public Task<IReadOnlyList<ChatMessage>> GetOwnedMessagesAsync(
            Guid conversationId,
            Guid companyId,
            Guid userId,
            CancellationToken cancellationToken) {
            return Task.FromResult<IReadOnlyList<ChatMessage>>(ExistingMessages);
        }

        public void AddConversation(Conversation conversation) => AddedConversations.Add(conversation);

        public void AddMessage(ChatMessage message) => AddedMessages.Add(message);

        public Task SaveChangesAsync(CancellationToken cancellationToken) {
            SaveChangesCount++;
            return Task.CompletedTask;
        }
    }
}
