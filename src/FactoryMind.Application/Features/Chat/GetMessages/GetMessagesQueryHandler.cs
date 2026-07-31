using FactoryMind.Application.Common.Identity;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Chat.GetMessages;

public sealed class GetMessagesQueryHandler(
    IConversationRepository repository,
    ICurrentUser currentUser) : IRequestHandler<GetMessagesQuery, Result<IReadOnlyList<MessageResponse>>> {
    public async ValueTask<Result<IReadOnlyList<MessageResponse>>> Handle(
        GetMessagesQuery query,
        CancellationToken cancellationToken) {
        var conversation = await repository.GetOwnedConversationAsync(
            query.ConversationId,
            currentUser.CompanyId,
            currentUser.UserId,
            cancellationToken);
        if (conversation is null) {
            return Result<IReadOnlyList<MessageResponse>>.Failure(ChatErrors.ConversationNotFound);
        }

        var messages = await repository.GetOwnedMessagesAsync(
            query.ConversationId,
            currentUser.CompanyId,
            currentUser.UserId,
            cancellationToken);
        var response = messages
            .Select(message => new MessageResponse(
                message.Id,
                message.Role,
                message.Content,
                message.CreatedAt,
                message.Citations
                    .OrderBy(citation => citation.ReferenceNumber)
                    .Select(citation => new CitationResponse(
                        citation.ReferenceNumber,
                        citation.DocumentId,
                        citation.ChunkId,
                        citation.DocumentTitle,
                        citation.FileName,
                        citation.PageNumber,
                        citation.Excerpt,
                        citation.Score))
                    .ToList(),
                message.BusinessEvidence
                    .OrderBy(evidence => evidence.ReferenceNumber)
                    .Select(evidence => new BusinessEvidenceResponse(
                        evidence.ReferenceNumber,
                        evidence.EntityId,
                        evidence.EntityType,
                        evidence.Title,
                        evidence.Detail))
                    .ToList()))
            .ToList();

        return Result<IReadOnlyList<MessageResponse>>.Success(response);
    }
}
