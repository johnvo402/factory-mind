using FactoryMind.Application.Features.Knowledge;
using Hangfire;

namespace FactoryMind.Infrastructure.Jobs;

public sealed class HangfireDocumentProcessingQueue(
    IBackgroundJobClient backgroundJobs) : IDocumentProcessingQueue {
    public void Enqueue(Guid documentId, Guid companyId) {
        try {
            backgroundJobs.Enqueue<DocumentProcessingJob>(job => job.RunAsync(
                documentId,
                companyId,
                CancellationToken.None));
        } catch (Exception exception) {
            throw new DocumentProcessingQueueException(
                "Document processing could not be queued.",
                exception);
        }
    }
}
