using FactoryMind.Application.Common.Search;

namespace FactoryMind.Application.Features.Knowledge;

public static class HybridKnowledgeRanker {
    private const double BothChannelsBoost = 0.002;
    private const double ContentPhraseBoost = 0.006;
    private const double MetadataPhraseBoost = 0.004;
    private const double ContentIdentifierBoost = 0.006;
    private const double MetadataIdentifierBoost = 0.006;
    private const double MaximumTokenCoverageBoost = 0.003;
    private const double MaximumFusionScore =
        2d / (KnowledgeSearchConstraints.ReciprocalRankConstant + 1);

    public static IReadOnlyList<KnowledgeSearchResult> Rank(
        string query,
        IReadOnlyList<KnowledgeSearchCandidate> candidates,
        int limit) {
        var normalizedQuery = SearchTextNormalizer.Normalize(query);
        var queryTokens = SearchTextNormalizer.Tokens(query);
        var identifiers = SearchTextNormalizer.Identifiers(query);
        var ranked = candidates
            .GroupBy(candidate => candidate.ChunkId)
            .Select(group => Merge(group))
            .Select(candidate => new RankedCandidate(candidate, Score(
                candidate,
                normalizedQuery,
                queryTokens,
                identifiers)))
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Candidate.VectorRank ?? int.MaxValue)
            .ThenBy(item => item.Candidate.LexicalRank ?? int.MaxValue)
            .ThenBy(item => item.Candidate.DocumentId)
            .ThenBy(item => item.Candidate.ChunkSequence)
            .ToList();

        var selected = new List<RankedCandidate>(limit);
        foreach (var candidate in ranked) {
            if (selected.Any(existing => IsNearDuplicate(existing.Candidate, candidate.Candidate))) {
                continue;
            }

            selected.Add(candidate);
            if (selected.Count == limit) {
                break;
            }
        }

        return selected.Select(item => new KnowledgeSearchResult(
            item.Candidate.DocumentId,
            item.Candidate.DocumentTitle,
            item.Candidate.FileName,
            item.Candidate.ChunkId,
            item.Candidate.PageNumber,
            item.Candidate.Content,
            Math.Round(Math.Clamp(item.Score / MaximumFusionScore, 0d, 1d), 6)))
            .ToList();
    }

    public static IReadOnlyList<KnowledgeSearchResult> RankVectorOnly(
        IReadOnlyList<KnowledgeSearchCandidate> candidates,
        int limit) => candidates
        .Where(candidate => candidate.VectorRank.HasValue)
        .GroupBy(candidate => candidate.ChunkId)
        .Select(Merge)
        .OrderBy(candidate => candidate.VectorRank)
        .ThenBy(candidate => candidate.DocumentId)
        .ThenBy(candidate => candidate.ChunkSequence)
        .Take(limit)
        .Select(candidate => new KnowledgeSearchResult(
            candidate.DocumentId,
            candidate.DocumentTitle,
            candidate.FileName,
            candidate.ChunkId,
            candidate.PageNumber,
            candidate.Content,
            Math.Round(candidate.VectorScore ?? 0d, 6)))
        .ToList();

    private static double Score(
        KnowledgeSearchCandidate candidate,
        string normalizedQuery,
        IReadOnlySet<string> queryTokens,
        IReadOnlySet<string> identifiers) {
        var score = Reciprocal(candidate.VectorRank) + Reciprocal(candidate.LexicalRank);
        if (candidate.VectorRank.HasValue && candidate.LexicalRank.HasValue) {
            score += BothChannelsBoost;
        }

        var content = SearchTextNormalizer.Normalize(candidate.Content);
        var title = SearchTextNormalizer.Normalize(candidate.DocumentTitle);
        var fileName = SearchTextNormalizer.Normalize(candidate.FileName);
        if (normalizedQuery.Length > 0 && content.Contains(normalizedQuery, StringComparison.Ordinal)) {
            score += ContentPhraseBoost;
        }

        if (normalizedQuery.Length > 0 && title.Contains(normalizedQuery, StringComparison.Ordinal)) {
            score += MetadataPhraseBoost;
        }

        if (normalizedQuery.Length > 0 && fileName.Contains(normalizedQuery, StringComparison.Ordinal)) {
            score += MetadataPhraseBoost;
        }

        if (identifiers.Any(identifier => ContainsIdentifier(content, identifier))) {
            score += ContentIdentifierBoost;
        }

        if (identifiers.Any(identifier =>
            ContainsIdentifier(title, identifier) || ContainsIdentifier(fileName, identifier))) {
            score += MetadataIdentifierBoost;
        }

        if (queryTokens.Count > 0) {
            var candidateTokens = SearchTextNormalizer.Tokens($"{candidate.DocumentTitle} {candidate.FileName} {candidate.Content}");
            var coverage = queryTokens.Count(token => candidateTokens.Contains(token)) / (double)queryTokens.Count;
            score += MaximumTokenCoverageBoost * coverage;
        }

        if (candidate.VectorScore is { } vectorScore && double.IsFinite(vectorScore)) {
            score += 0.0015 * Math.Clamp(vectorScore, 0d, 1d);
        }

        if (candidate.LexicalScore is { } lexicalScore
            && lexicalScore > 0d
            && double.IsFinite(lexicalScore)) {
            score += 0.0015 * (lexicalScore / (1d + lexicalScore));
        }

        return score;
    }

    private static double Reciprocal(int? rank) => rank.HasValue
        ? 1d / (KnowledgeSearchConstraints.ReciprocalRankConstant + rank.Value)
        : 0d;

    private static bool ContainsIdentifier(string text, string identifier) {
        var index = text.IndexOf(identifier, StringComparison.Ordinal);
        while (index >= 0) {
            var before = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
            var end = index + identifier.Length;
            var after = end == text.Length || !char.IsLetterOrDigit(text[end]);
            if (before && after) {
                return true;
            }

            index = text.IndexOf(identifier, index + 1, StringComparison.Ordinal);
        }

        return false;
    }

    private static KnowledgeSearchCandidate Merge(IEnumerable<KnowledgeSearchCandidate> values) {
        var candidates = values.ToList();
        var first = candidates[0];
        return first with {
            VectorRank = candidates.Where(item => item.VectorRank.HasValue).Min(item => item.VectorRank),
            LexicalRank = candidates.Where(item => item.LexicalRank.HasValue).Min(item => item.LexicalRank),
            VectorScore = candidates.Where(item => item.VectorScore.HasValue).Max(item => item.VectorScore),
            LexicalScore = candidates.Where(item => item.LexicalScore.HasValue).Max(item => item.LexicalScore)
        };
    }

    private static bool IsNearDuplicate(KnowledgeSearchCandidate left, KnowledgeSearchCandidate right) {
        if (left.DocumentId != right.DocumentId
            || Math.Abs(left.ChunkSequence - right.ChunkSequence) > 1) {
            return false;
        }

        var leftTokens = SearchTextNormalizer.Tokens(left.Content);
        var rightTokens = SearchTextNormalizer.Tokens(right.Content);
        if (leftTokens.Count == 0 || rightTokens.Count == 0) {
            return string.Equals(left.Content, right.Content, StringComparison.Ordinal);
        }

        var common = leftTokens.Count(rightTokens.Contains);
        return common / (double)Math.Min(leftTokens.Count, rightTokens.Count) >= 0.85;
    }

    private sealed record RankedCandidate(KnowledgeSearchCandidate Candidate, double Score);
}
