using FactoryMind.Application.Features.Chat;
using FactoryMind.Domain.Chat;
using Microsoft.EntityFrameworkCore;

namespace FactoryMind.Infrastructure.Persistence.Chat;

public sealed class EfConversationRepository(FactoryMindDbContext dbContext) : IConversationRepository {
    public async Task<IReadOnlyList<Conversation>> GetOwnedConversationsAsync(
        Guid companyId,
        Guid userId,
        CancellationToken cancellationToken) {
        return await dbContext.Conversations
            .AsNoTracking()
            .Where(conversation => conversation.CompanyId == companyId && conversation.UserId == userId)
            .OrderByDescending(conversation => conversation.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<Conversation?> GetOwnedConversationAsync(
        Guid conversationId,
        Guid companyId,
        Guid userId,
        CancellationToken cancellationToken) {
        return dbContext.Conversations.SingleOrDefaultAsync(
            conversation => conversation.Id == conversationId
                && conversation.CompanyId == companyId
                && conversation.UserId == userId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<ChatMessage>> GetOwnedMessagesAsync(
        Guid conversationId,
        Guid companyId,
        Guid userId,
        CancellationToken cancellationToken) {
        return await dbContext.Messages
            .AsNoTracking()
            .Where(message => message.ConversationId == conversationId
                && message.Conversation!.CompanyId == companyId
                && message.Conversation.UserId == userId)
            .OrderBy(message => message.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public void AddConversation(Conversation conversation) => dbContext.Conversations.Add(conversation);

    public void AddMessage(ChatMessage message) => dbContext.Messages.Add(message);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
