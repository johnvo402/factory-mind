using System.Text;

namespace FactoryMind.Application.Features.Chat.Rag;

public sealed class BusinessContextBuilder(
    IBusinessContextRepository repository) : IBusinessContextBuilder {
    public const int LimitPerScope = 5;
    public const int MaximumContextLength = 6_000;
    public const int MaximumDetailLength = 500;

    private const string Instructions =
        "Use the live company business data below as the source of truth. "
        + "Cite every supported business claim with its label such as [B1]. "
        + "Do not invent missing values.\n\n";

    public async Task<BusinessContext> BuildAsync(
        Guid companyId,
        IntentRoute route,
        CancellationToken cancellationToken) {
        var records = await repository.RetrieveAsync(
            companyId,
            route.BusinessScopes,
            route.MachineStatus,
            route.ProductionOrderStatus,
            LimitPerScope,
            cancellationToken);
        if (records.Count == 0) {
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

        return new BusinessContext(prompt.ToString().TrimEnd(), evidence);
    }
}
