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
        var chatClient = new FakeChatCompletionClient("Available", " now.");
        var handler = new SendMessageCommandHandler(repository, chatClient, currentUser);

        var result = await handler.Handle(
            new SendMessageCommand(conversation.Id, "  Which machine is available?  "),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var question = Assert.Single(repository.AddedMessages);
        Assert.Equal(ChatRoles.User, question.Role);
        Assert.Equal("Which machine is available?", question.Content);
        Assert.Equal("Which machine is available?", conversation.Title);

        var tokens = new List<string>();
        await foreach (var token in result.Value!.Tokens) {
            tokens.Add(token);
        }

        Assert.Equal(["Available", " now."], tokens);
        var answer = Assert.Single(repository.AddedMessages, message => message.Role == ChatRoles.Assistant);
        Assert.Equal("Available now.", answer.Content);
        Assert.Equal(2, repository.SaveChangesCount);
        Assert.Equal(ChatRoles.User, chatClient.Prompt[^1].Role);
    }

    [Fact]
    public async Task Send_message_cannot_access_another_tenant_conversation() {
        var currentUser = new FakeCurrentUser();
        var repository = new FakeConversationRepository();
        var chatClient = new FakeChatCompletionClient("unused");
        var handler = new SendMessageCommandHandler(repository, chatClient, currentUser);
        var conversationId = Guid.NewGuid();

        var result = await handler.Handle(
            new SendMessageCommand(conversationId, "Question"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("chat.conversation_not_found", result.Error?.Code);
        Assert.Equal((conversationId, currentUser.CompanyId, currentUser.UserId), repository.RequestedConversation);
        Assert.Empty(repository.AddedMessages);
        Assert.Empty(chatClient.Prompt);
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
