using System.Diagnostics;
using System.Text;
using FactoryMind.Shared.Observability;

namespace FactoryMind.Application.Features.Chat.Rag;

public sealed class BusinessContextBuilder(
    IBusinessContextRepository repository) : IBusinessContextBuilder {
    public const int LimitPerScope = 5;
    public const int MaximumContextLength = 6_000;
    public const int MaximumDetailLength = 500;

    private const string Instructions =
        "Use the live company business data below as the source of truth. "
        + "Cite every supported business claim with the matching evidence label shown below. "
        + "Do not invent missing values.\n\n";

    public async Task<BusinessContext> BuildAsync(
        Guid companyId,
        string question,
        IntentRoute route,
        CancellationToken cancellationToken) {
        var startedTimestamp = Stopwatch.GetTimestamp();
        var outcome = "success";
        var intent = route.Intent.ToString().ToLowerInvariant();
        var scopeCount = Enum.GetValues<BusinessDataScope>()
            .Count(scope => scope is not BusinessDataScope.None and not BusinessDataScope.All
                && route.BusinessScopes.HasFlag(scope));
        using var activity = FactoryMindTelemetry.ActivitySource.StartActivity(
            "factorymind.rag.business",
            ActivityKind.Internal);
        activity?.SetTag("factorymind.chat.intent", intent);
        activity?.SetTag("factorymind.rag.business.scope_count", scopeCount);
        try {
            var records = await repository.RetrieveAsync(
                companyId,
                question,
                route.BusinessScopes,
                route.MachineStatus,
                route.ProductionOrderStatus,
                LimitPerScope,
                cancellationToken);
            FactoryMindTelemetry.BusinessRagCandidates.Record(
                records.Count,
                FactoryMindTelemetry.Tags(("intent", intent)));
            activity?.SetTag("factorymind.rag.business.candidate_count", records.Count);
            if (records.Count == 0) {
                FactoryMindTelemetry.BusinessRagEvidence.Record(
                    0,
                    FactoryMindTelemetry.Tags(("intent", intent)));
                activity?.SetTag("factorymind.rag.business.evidence_count", 0);
                return new BusinessContext(
                    Instructions + "No matching company business data was retrieved.",
                    []);
            }

            var prompt = new StringBuilder(Instructions);
            var evidence = new List<BusinessEvidenceResponse>(records.Count);

            foreach (var record in records) {
                var referenceNumber = evidence.Count + 1;
                var detail = record.Detail.Length <= MaximumDetailLength
                    ? record.Detail
                    : $"{record.Detail[..(MaximumDetailLength - 3)].TrimEnd()}...";
                var line = $"[B{referenceNumber}] {record.EntityType}: {record.Title}; {detail}\n";
                if (prompt.Length + line.Length > MaximumContextLength) {
                    break;
                }

                prompt.Append(line);
                evidence.Add(new BusinessEvidenceResponse(
                    referenceNumber,
                    record.EntityId,
                    record.EntityType,
                    record.Title,
                    detail));
            }

            FactoryMindTelemetry.BusinessRagEvidence.Record(
                evidence.Count,
                FactoryMindTelemetry.Tags(("intent", intent)));
            activity?.SetTag("factorymind.rag.business.evidence_count", evidence.Count);
            return new BusinessContext(prompt.ToString().TrimEnd(), evidence);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            outcome = "cancelled";
            throw;
        } catch {
            outcome = "error";
            activity?.SetStatus(ActivityStatusCode.Error);
            throw;
        } finally {
            var tags = FactoryMindTelemetry.Tags(
                ("intent", intent),
                ("outcome", outcome));
            FactoryMindTelemetry.BusinessRagRequests.Add(1, tags);
            FactoryMindTelemetry.BusinessRagDuration.Record(
                Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds,
                tags);
            FactoryMindTelemetry.BusinessRagScopeCount.Record(
                scopeCount,
                FactoryMindTelemetry.Tags(("intent", intent)));
            activity?.SetTag("factorymind.outcome", outcome);
        }
    }
}
