using FactoryMind.Application.Common.Identity;
using FactoryMind.Domain.Knowledge;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Knowledge.ReindexDocuments;

public sealed class ReindexDocumentsCommandHandler(
    IDocumentRepository repository,
    IDocumentProcessingQueue processingQueue,
    ICurrentUser currentUser) : IRequestHandler<ReindexDocumentsCommand, Result<ReindexDocumentsResponse>> {
    public async ValueTask<Result<ReindexDocumentsResponse>> Handle(
        ReindexDocumentsCommand command,
        CancellationToken cancellationToken) {
        var documents = await repository.GetByCompanyAsync(currentUser.CompanyId, cancellationToken);
        var readyDocuments = documents
            .Where(document => document.Status == DocumentStatuses.Ready)
            .ToList();

        try {
            foreach (var document in readyDocuments) {
                processingQueue.Enqueue(document.Id, document.CompanyId);
            }
        } catch (DocumentProcessingQueueException) {
            return Result<ReindexDocumentsResponse>.Failure(DocumentErrors.QueueUnavailable);
        }

        return Result<ReindexDocumentsResponse>.Success(new(readyDocuments.Count));
    }
}
