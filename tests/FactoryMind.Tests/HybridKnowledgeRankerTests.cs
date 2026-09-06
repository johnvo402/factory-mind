using FactoryMind.Application.Common.Search;
using FactoryMind.Application.Features.Knowledge;

namespace FactoryMind.Tests;

public sealed class HybridKnowledgeRankerTests {
    [Fact]
    public void Rrf_promotes_a_candidate_returned_by_both_channels() {
        var both = Candidate("both", vectorRank: 3, lexicalRank: 3);
        var vectorOnly = Candidate("vector", vectorRank: 1);
        var lexicalOnly = Candidate("lexical", lexicalRank: 1);

        var results = HybridKnowledgeRanker.Rank(
            "unmatched query",
            [vectorOnly, lexicalOnly, both],
            3);

        Assert.Equal(both.ChunkId, results[0].ChunkId);
    }

    [Fact]
    public void Strong_semantic_candidate_survives_when_lexical_overlap_is_weak() {
        var semantic = Candidate(
            "isolated energy before service",
            vectorRank: 1,
            vectorScore: 0.97);
        var lexical = Candidate(
            "machine repair",
            vectorRank: 8,
            lexicalRank: 1,
            vectorScore: 0.4,
            lexicalScore: 0.8);

        var results = HybridKnowledgeRanker.Rank(
            "safe servicing procedure",
            [semantic, lexical],
            2);

        Assert.Contains(results, result => result.ChunkId == semantic.ChunkId);
    }

    [Fact]
    public void Exact_hyphenated_identifier_receives_a_deterministic_boost() {
        var exact = Candidate("Follow SOP-CNC-042 before spindle inspection.", lexicalRank: 2);
        var generic = Candidate("General spindle inspection guidance.", lexicalRank: 1);

        var results = HybridKnowledgeRanker.Rank("SOP-CNC-042 spindle", [generic, exact], 2);

        Assert.Equal(exact.ChunkId, results[0].ChunkId);
        Assert.Contains("sop-cnc-042", SearchTextNormalizer.Identifiers("SOP-CNC-042 spindle"));
    }

    [Fact]
    public void Vector_and_lexical_duplicates_are_unioned_by_chunk_id() {
        var id = Guid.NewGuid();
        var vector = Candidate("Emergency stop.", vectorRank: 1) with { ChunkId = id };
        var lexical = Candidate("Emergency stop.", lexicalRank: 1) with { ChunkId = id };

        var result = HybridKnowledgeRanker.Rank("emergency stop", [vector, lexical], 5);

        Assert.Single(result);
    }

    [Fact]
    public void Highly_overlapping_neighbor_is_removed() {
        var documentId = Guid.NewGuid();
        var first = Candidate(
            "lock out power before maintenance verify isolation wear protection",
            vectorRank: 1) with { DocumentId = documentId, ChunkSequence = 2 };
        var neighbor = Candidate(
            "power before maintenance verify isolation wear protection lock out",
            lexicalRank: 1) with { DocumentId = documentId, ChunkSequence = 3 };

        var result = HybridKnowledgeRanker.Rank("maintenance isolation", [first, neighbor], 5);

        Assert.Single(result);
    }

    [Fact]
    public void Final_score_is_bounded_and_is_not_presented_as_cosine_similarity() {
        var result = Assert.Single(HybridKnowledgeRanker.Rank(
            "SOP-CNC-042",
            [Candidate("SOP-CNC-042", vectorRank: 1, lexicalRank: 1)],
            1));

        Assert.InRange(result.Score, 0d, 1d);
    }

    [Fact]
    public void Lexical_normalization_preserves_diacritics_and_hyphenated_identifiers() {
        Assert.Equal(
            "hướng dẫn sop-cnc-042",
            SearchTextNormalizer.NormalizeLexical(" Hướng   dẫn: SOP-CNC-042? "));
    }

    private static KnowledgeSearchCandidate Candidate(
        string content,
        int? vectorRank = null,
        int? lexicalRank = null,
        double? vectorScore = null,
        double? lexicalScore = null) => new(
            Guid.NewGuid(),
            "Manufacturing safety",
            "safety.pdf",
            Guid.NewGuid(),
            0,
            1,
            content,
            vectorRank,
            lexicalRank,
            vectorScore,
            lexicalScore);
}
