using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FactoryMind.Application.Features.Chat;
using FactoryMind.Application.Features.Chat.Rag;
using FactoryMind.Application.Features.Knowledge;

var datasetPath = Path.Combine(AppContext.BaseDirectory, "rag-eval-cases.json");
var cases = JsonSerializer.Deserialize<List<EvalCase>>(
    await File.ReadAllTextAsync(datasetPath),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
    ?? throw new InvalidOperationException("RAG evaluation dataset is empty.");
if (cases.Count < 20) {
    throw new InvalidOperationException("RAG evaluation requires at least 20 cases.");
}

var router = new IntentRouter();
var intentHits = 0;
var scopeHits = 0;
var knowledgeCases = 0;
var vectorRecallHits = 0;
var hybridRecallHits = 0;
var vectorReciprocalRank = 0d;
var hybridReciprocalRank = 0d;
var identifierCases = 0;
var identifierHits = 0;
var businessCases = 0;
var businessHits = 0;
var failures = new List<string>();

foreach (var evalCase in cases) {
    var route = router.Route(evalCase.Query);
    var expectedIntent = Enum.Parse<ChatIntent>(evalCase.ExpectedIntent);
    var expectedScopes = evalCase.ExpectedScopes
        .Select(value => Enum.Parse<BusinessDataScope>(value))
        .Aggregate(BusinessDataScope.None, (current, scope) => current | scope);
    if (route.Intent == expectedIntent) {
        intentHits++;
    } else {
        failures.Add($"{evalCase.Id}: intent {route.Intent}, expected {expectedIntent}");
    }

    if (route.BusinessScopes == expectedScopes) {
        scopeHits++;
    } else {
        failures.Add($"{evalCase.Id}: scopes {route.BusinessScopes}, expected {expectedScopes}");
    }

    if (evalCase.KnowledgeCandidates.Count > 0) {
        knowledgeCases++;
        var candidates = evalCase.KnowledgeCandidates
            .Select((candidate, index) => new KnowledgeSearchCandidate(
                StableGuid($"{evalCase.Id}:document:{candidate.FileName}"),
                candidate.Title,
                candidate.FileName,
                StableGuid($"{evalCase.Id}:chunk:{index}"),
                index,
                candidate.PageNumber,
                candidate.Content,
                candidate.VectorRank,
                candidate.LexicalRank,
                candidate.VectorScore,
                candidate.LexicalScore))
            .ToList();
        var vector = HybridKnowledgeRanker.RankVectorOnly(candidates, 5);
        var hybrid = HybridKnowledgeRanker.Rank(evalCase.Query, candidates, 5);
        var vectorRank = ExpectedRank(vector, evalCase);
        var hybridRank = ExpectedRank(hybrid, evalCase);
        if (vectorRank.HasValue) {
            vectorRecallHits++;
            vectorReciprocalRank += 1d / vectorRank.Value;
        }

        if (hybridRank.HasValue) {
            hybridRecallHits++;
            hybridReciprocalRank += 1d / hybridRank.Value;
        } else {
            failures.Add($"{evalCase.Id}: hybrid retrieval missed expected evidence");
        }

        if (evalCase.ExactIdentifier) {
            identifierCases++;
            if (hybridRank.HasValue) {
                identifierHits++;
            } else {
                failures.Add($"{evalCase.Id}: exact identifier missed");
            }
        }
    }

    if (evalCase.BusinessCandidates.Count > 0) {
        businessCases++;
        var winner = evalCase.BusinessCandidates
            .OrderByDescending(candidate =>
                BusinessEntityRanker.Score(evalCase.Query, candidate.Code, candidate.Name))
            .ThenBy(candidate => candidate.Code, StringComparer.OrdinalIgnoreCase)
            .First();
        if (string.Equals(winner.Code, evalCase.ExpectedEntity, StringComparison.OrdinalIgnoreCase)) {
            businessHits++;
        } else {
            failures.Add($"{evalCase.Id}: entity {winner.Code}, expected {evalCase.ExpectedEntity}");
        }
    }
}

var metrics = new {
    DatasetSize = cases.Count,
    IntentAccuracy = intentHits / (double)cases.Count,
    BusinessScopeAccuracy = scopeHits / (double)cases.Count,
    VectorRecallAt5 = vectorRecallHits / (double)knowledgeCases,
    HybridRecallAt5 = hybridRecallHits / (double)knowledgeCases,
    VectorMrr = vectorReciprocalRank / knowledgeCases,
    HybridMrr = hybridReciprocalRank / knowledgeCases,
    ExactIdentifierHitRateAt5 = identifierHits / (double)identifierCases,
    BusinessExactEntityHitRate = businessHits / (double)businessCases
};

Console.WriteLine($"RAG evaluation cases: {metrics.DatasetSize}");
Console.WriteLine($"Intent Accuracy: {metrics.IntentAccuracy:P2}");
Console.WriteLine($"Business Scope Accuracy: {metrics.BusinessScopeAccuracy:P2}");
Console.WriteLine($"Vector Recall@5: {metrics.VectorRecallAt5:P2}");
Console.WriteLine($"Hybrid Recall@5: {metrics.HybridRecallAt5:P2}");
Console.WriteLine($"Vector MRR: {metrics.VectorMrr:F4}");
Console.WriteLine($"Hybrid MRR: {metrics.HybridMrr:F4}");
Console.WriteLine($"Exact Identifier HitRate@5: {metrics.ExactIdentifierHitRateAt5:P2}");
Console.WriteLine($"Business Exact Entity HitRate: {metrics.BusinessExactEntityHitRate:P2}");

var thresholdsMet = metrics.IntentAccuracy >= 0.90
    && metrics.BusinessScopeAccuracy >= 0.90
    && metrics.HybridRecallAt5 >= 0.85
    && metrics.HybridMrr >= 0.75
    && metrics.ExactIdentifierHitRateAt5 >= 1.00
    && metrics.BusinessExactEntityHitRate >= 0.90
    && metrics.HybridRecallAt5 >= metrics.VectorRecallAt5
    && metrics.HybridMrr >= metrics.VectorMrr;
if (!thresholdsMet || failures.Count > 0) {
    Console.Error.WriteLine("RAG evaluation failed:");
    foreach (var failure in failures) {
        Console.Error.WriteLine($"- {failure}");
    }

    return 1;
}

Console.WriteLine("RAG evaluation thresholds passed.");
return 0;

static int? ExpectedRank(IReadOnlyList<KnowledgeSearchResult> results, EvalCase evalCase) {
    for (var index = 0; index < results.Count; index++) {
        var result = results[index];
        if (string.Equals(result.FileName, evalCase.ExpectedDocument, StringComparison.OrdinalIgnoreCase)
            && evalCase.ExpectedChunkContains.All(expected =>
                result.Content.Contains(expected, StringComparison.OrdinalIgnoreCase))) {
            return index + 1;
        }
    }

    return null;
}

static Guid StableGuid(string value) {
    var bytes = MD5.HashData(Encoding.UTF8.GetBytes(value));
    return new Guid(bytes);
}

internal sealed record EvalCase(
    string Id,
    string Query,
    string ExpectedIntent,
    IReadOnlyList<string> ExpectedScopes,
    string? ExpectedDocument,
    IReadOnlyList<string> ExpectedChunkContains,
    bool ExactIdentifier,
    IReadOnlyList<KnowledgeCandidate> KnowledgeCandidates,
    string? ExpectedEntity,
    IReadOnlyList<BusinessCandidate> BusinessCandidates);

internal sealed record KnowledgeCandidate(
    string Title,
    string FileName,
    int PageNumber,
    string Content,
    int? VectorRank,
    int? LexicalRank,
    double? VectorScore,
    double? LexicalScore);

internal sealed record BusinessCandidate(string Code, string Name);
