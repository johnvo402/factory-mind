using FactoryMind.Application.Features.Knowledge.ProcessDocument;
using Hangfire;
using Mediator;

namespace FactoryMind.Infrastructure.Jobs;

public sealed class DocumentProcessingJob(ISender sender) {
    [Queue("documents")]
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task RunAsync(
        Guid documentId,
        Guid companyId,
        CancellationToken cancellationToken) {
        var result = await sender.Send(
            new ProcessDocumentCommand(documentId, companyId),
            cancellationToken);
        if (result.IsFailure) {
            throw new InvalidOperationException(result.Error!.Message);
        }
    }
}
