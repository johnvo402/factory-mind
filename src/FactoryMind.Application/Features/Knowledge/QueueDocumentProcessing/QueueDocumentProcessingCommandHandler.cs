using FactoryMind.Application.Common.Identity;
using FactoryMind.Domain.Knowledge;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Knowledge.QueueDocumentProcessing;

public sealed class QueueDocumentProcessingCommandHandler(
    IDocumentRepository repository,
    IDocumentProcessingQueue processingQueue,
    ICurrentUser currentUser) : IRequestHandler<QueueDocumentProcessingCommand, Result<DocumentResponse>> {
    public async ValueTask<Result<DocumentResponse>> Handle(
        QueueDocumentProcessingCommand command,
        CancellationToken cancellationToken) {
        var document = await repository.GetByIdAsync(
            command.DocumentId,
            currentUser.CompanyId,
            cancellationToken);
        if (document is null) {
            return Result<DocumentResponse>.Failure(DocumentErrors.NotFound);
        }

        if (document.Status == DocumentStatuses.Processing) {
            return Result<DocumentResponse>.Failure(DocumentErrors.ProcessingAlreadyRunning);
        }

        try {
            processingQueue.Enqueue(document.Id, document.CompanyId);
        } catch (DocumentProcessingQueueException) {
            return Result<DocumentResponse>.Failure(DocumentErrors.QueueUnavailable);
        }

        return Result<DocumentResponse>.Success(DocumentResponse.From(document));
    }
}
