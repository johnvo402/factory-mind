using System.Diagnostics;
using FactoryMind.Application.Common.Search;
using FactoryMind.Application.Features.Knowledge;
using FactoryMind.Domain.Knowledge;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using FactoryMind.Shared.Observability;

namespace FactoryMind.Infrastructure.Persistence.Knowledge;

public sealed class EfKnowledgeSearchRepository(
    FactoryMindDbContext dbContext) : IKnowledgeSearchRepository {
    public async Task<IReadOnlyList<KnowledgeSearchCandidate>> RetrieveCandidatesAsync(
        Guid companyId,
        string embeddingModel,
        string normalizedQuery,
        float[] queryEmbedding,
        int vectorLimit,
        int lexicalLimit,
        CancellationToken cancellationToken) {
        var vectorCandidates = await RetrieveVectorCandidatesAsync(
            companyId,
            embeddingModel,
            queryEmbedding,
            vectorLimit,
            cancellationToken);
        var lexicalTerms = SearchTextNormalizer.LexicalTokens(normalizedQuery);
        var lexicalCandidates = lexicalTerms.Count == 0
            ? RecordSkippedLexicalSearch()
            : await RetrieveLexicalCandidatesAsync(
                companyId,
                string.Join(" | ", lexicalTerms),
                lexicalLimit,
                cancellationToken);

        return vectorCandidates.Concat(lexicalCandidates).ToList();
    }

    private async Task<IReadOnlyList<KnowledgeSearchCandidate>> RetrieveVectorCandidatesAsync(
        Guid companyId,
        string embeddingModel,
        float[] queryEmbedding,
        int limit,
        CancellationToken cancellationToken) {
        var startedTimestamp = Stopwatch.GetTimestamp();
        using var activity = FactoryMindTelemetry.ActivitySource.StartActivity(
            "factorymind.rag.vector_search",
            ActivityKind.Internal);
        activity?.SetTag("factorymind.rag.candidate_limit", limit);
        var vector = new Vector(queryEmbedding);
        var matches = await (
            from embedding in dbContext.DocumentEmbeddings.AsNoTracking()
            join chunk in dbContext.DocumentChunks.AsNoTracking()
                on embedding.DocumentChunkId equals chunk.Id
            join document in dbContext.Documents.AsNoTracking()
                on chunk.DocumentId equals document.Id
            where embedding.CompanyId == companyId
                && chunk.CompanyId == companyId
                && document.CompanyId == companyId
                && document.Status == DocumentStatuses.Ready
                && embedding.Model == embeddingModel
            select new {
                DocumentId = document.Id,
                DocumentTitle = document.Title,
                document.FileName,
                ChunkId = chunk.Id,
                ChunkSequence = chunk.Sequence,
                chunk.PageNumber,
                chunk.Content,
                Distance = embedding.Embedding.CosineDistance(vector)
            })
            .OrderBy(match => match.Distance)
            .ThenBy(match => match.ChunkId)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var results = matches.Select((match, index) => new KnowledgeSearchCandidate(
            match.DocumentId,
            match.DocumentTitle,
            match.FileName,
            match.ChunkId,
            match.ChunkSequence,
            match.PageNumber,
            match.Content,
            VectorRank: index + 1,
            VectorScore: Math.Round(1d - match.Distance, 6)))
            .ToList();
        activity?.SetTag("factorymind.rag.candidate_count", results.Count);
        FactoryMindTelemetry.RagVectorDuration.Record(
            Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds);
        FactoryMindTelemetry.RagVectorCandidates.Record(results.Count);
        return results;
    }

    private async Task<IReadOnlyList<KnowledgeSearchCandidate>> RetrieveLexicalCandidatesAsync(
        Guid companyId,
        string lexicalTsQuery,
        int limit,
        CancellationToken cancellationToken) {
        var startedTimestamp = Stopwatch.GetTimestamp();
        using var activity = FactoryMindTelemetry.ActivitySource.StartActivity(
            "factorymind.rag.lexical_search",
            ActivityKind.Internal);
        activity?.SetTag("factorymind.rag.candidate_limit", limit);
        var matches = await dbContext.Database.SqlQuery<LexicalMatch>($"""
            SELECT
                document."Id" AS "DocumentId",
                document."Title" AS "DocumentTitle",
                document."FileName",
                chunk."Id" AS "ChunkId",
                chunk."Sequence" AS "ChunkSequence",
                chunk."PageNumber",
                chunk."Content",
                ts_rank_cd(
                    to_tsvector('simple', COALESCE(chunk."Content", '')),
                    to_tsquery('simple', {lexicalTsQuery}))::double precision AS "Score"
            FROM document_chunks AS chunk
            INNER JOIN documents AS document ON chunk."DocumentId" = document."Id"
            WHERE chunk."CompanyId" = {companyId}
              AND document."CompanyId" = {companyId}
              AND document."Status" = {DocumentStatuses.Ready}
              AND to_tsvector('simple', COALESCE(chunk."Content", ''))
                  @@ to_tsquery('simple', {lexicalTsQuery})
            ORDER BY "Score" DESC, chunk."Id"
            LIMIT {limit}
            """)
            .ToListAsync(cancellationToken);

        var results = matches.Select((match, index) => new KnowledgeSearchCandidate(
            match.DocumentId,
            match.DocumentTitle,
            match.FileName,
            match.ChunkId,
            match.ChunkSequence,
            match.PageNumber,
            match.Content,
            LexicalRank: index + 1,
            LexicalScore: Math.Round(match.Score, 6)))
            .ToList();
        activity?.SetTag("factorymind.rag.candidate_count", results.Count);
        FactoryMindTelemetry.RagLexicalDuration.Record(
            Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds);
        FactoryMindTelemetry.RagLexicalCandidates.Record(results.Count);
        return results;
    }

    private static IReadOnlyList<KnowledgeSearchCandidate> RecordSkippedLexicalSearch() {
        using var activity = FactoryMindTelemetry.ActivitySource.StartActivity(
            "factorymind.rag.lexical_search",
            ActivityKind.Internal);
        activity?.SetTag("factorymind.rag.candidate_count", 0);
        FactoryMindTelemetry.RagLexicalDuration.Record(0);
        FactoryMindTelemetry.RagLexicalCandidates.Record(0);
        return [];
    }

    private sealed class LexicalMatch {
        public Guid DocumentId { get; init; }
        public string DocumentTitle { get; init; } = string.Empty;
        public string FileName { get; init; } = string.Empty;
        public Guid ChunkId { get; init; }
        public int ChunkSequence { get; init; }
        public int PageNumber { get; init; }
        public string Content { get; init; } = string.Empty;
        public double Score { get; init; }
    }
}
