using System.Diagnostics;
using FactoryMind.Shared.AI;
using FactoryMind.Application.Common.Search;
using FactoryMind.Shared.Observability;

namespace FactoryMind.Application.Features.Knowledge;

public sealed class KnowledgeRetriever(
    IEmbeddingClient embeddingClient,
    IKnowledgeSearchRepository searchRepository) {
    public async Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(
        Guid companyId,
        string query,
        int limit,
        CancellationToken cancellationToken) {
        var startedTimestamp = Stopwatch.GetTimestamp();
        var outcome = "success";
        using var activity = FactoryMindTelemetry.ActivitySource.StartActivity(
            "factorymind.rag.knowledge",
            ActivityKind.Internal);
        activity?.SetTag("factorymind.rag.result_limit", limit);
        try {
            var embeddingQuery = string.Join(' ', query.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            var embedding = await embeddingClient.CreateAsync(
                [embeddingQuery],
                EmbeddingPurpose.Query,
                cancellationToken);
            if (embedding.Vectors.Count != 1
                || embedding.Vectors[0].Length != DocumentEmbeddingConstraints.Dimensions) {
                throw new AiProviderException("AI service returned an invalid embedding response.");
            }

            activity?.SetTag("factorymind.rag.embedding_model", embedding.Model);
            var normalizedQuery = SearchTextNormalizer.NormalizeLexical(query);
            var candidates = await searchRepository.RetrieveCandidatesAsync(
                companyId,
                embedding.Model,
                normalizedQuery,
                embedding.Vectors[0],
                KnowledgeSearchConstraints.VectorCandidateLimit,
                KnowledgeSearchConstraints.LexicalCandidateLimit,
                cancellationToken);
            var rankStartedTimestamp = Stopwatch.GetTimestamp();
            using var rankActivity = FactoryMindTelemetry.ActivitySource.StartActivity(
                "factorymind.rag.rank",
                ActivityKind.Internal);
            rankActivity?.SetTag("factorymind.rag.candidate_count", candidates.Count);
            var results = HybridKnowledgeRanker.Rank(query, candidates, limit);
            var rankDuration = Stopwatch.GetElapsedTime(rankStartedTimestamp).TotalMilliseconds;
            rankActivity?.SetTag("factorymind.rag.result_count", results.Count);
            FactoryMindTelemetry.RagRankDuration.Record(rankDuration);
            FactoryMindTelemetry.RagResults.Record(results.Count);
            activity?.SetTag("factorymind.rag.result_count", results.Count);
            return results;
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            outcome = "cancelled";
            throw;
        } catch {
            outcome = "error";
            activity?.SetStatus(ActivityStatusCode.Error);
            throw;
        } finally {
            var tags = FactoryMindTelemetry.Tags(("outcome", outcome));
            FactoryMindTelemetry.RagRequests.Add(1, tags);
            FactoryMindTelemetry.RagDuration.Record(
                Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds,
                tags);
            activity?.SetTag("factorymind.outcome", outcome);
        }
    }
}
