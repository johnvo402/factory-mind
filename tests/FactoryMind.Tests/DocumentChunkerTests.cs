using FactoryMind.Application.Features.Knowledge;

namespace FactoryMind.Tests;

public sealed class DocumentChunkerTests {
    private readonly DocumentChunker _chunker = new();

    [Fact]
    public void Small_page_becomes_one_normalized_chunk() {
        var chunks = _chunker.Chunk([new DocumentPageText(3, "  Emergency\t stop   SOP-CNC-042.  ")]);

        var chunk = Assert.Single(chunks);
        Assert.Equal(0, chunk.Sequence);
        Assert.Equal(3, chunk.PageNumber);
        Assert.Equal("Emergency stop SOP-CNC-042.", chunk.Content);
    }

    [Fact]
    public void Paragraph_boundary_is_preferred_before_target() {
        var first = string.Join(' ', Enumerable.Repeat("First paragraph sentence.", 32));
        var second = string.Join(' ', Enumerable.Repeat("Second paragraph instruction.", 32));

        var chunks = _chunker.Chunk([new DocumentPageText(1, $"{first}\n\n{second}")]);

        Assert.True(chunks.Count >= 2);
        Assert.Equal(first, chunks[0].Content);
    }

    [Fact]
    public void Sentence_boundary_is_preferred_for_long_paragraph() {
        var content = string.Join(' ', Enumerable.Range(0, 100)
            .Select(index => $"Sentence {index} contains deterministic safety guidance."));

        var chunks = _chunker.Chunk([new DocumentPageText(1, content)]);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks.Take(chunks.Count - 1), chunk => Assert.EndsWith(".", chunk.Content));
    }

    [Fact]
    public void Long_sentence_uses_bounded_whitespace_fallback() {
        var content = string.Join(' ', Enumerable.Range(0, 500).Select(index => $"token{index}"));

        var chunks = _chunker.Chunk([new DocumentPageText(1, content)]);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, chunk => Assert.True(chunk.Content.Length <= DocumentChunker.MaximumLength));
    }

    [Fact]
    public void Chunks_never_cross_page_boundaries_and_have_stable_indexes() {
        var pages = new[] {
            new DocumentPageText(2, string.Join(' ', Enumerable.Repeat("PAGE-TWO.", 200))),
            new DocumentPageText(1, string.Join(' ', Enumerable.Repeat("PAGE-ONE.", 200)))
        };

        var firstRun = _chunker.Chunk(pages);
        var secondRun = _chunker.Chunk(pages);

        Assert.Equal(firstRun, secondRun);
        Assert.Equal(Enumerable.Range(0, firstRun.Count), firstRun.Select(chunk => chunk.Sequence));
        Assert.All(firstRun.Where(chunk => chunk.PageNumber == 1),
            chunk => Assert.DoesNotContain("PAGE-TWO", chunk.Content));
        Assert.All(firstRun.Where(chunk => chunk.PageNumber == 2),
            chunk => Assert.DoesNotContain("PAGE-ONE", chunk.Content));
        Assert.True(firstRun.TakeWhile(chunk => chunk.PageNumber == 1).Any());
    }

    [Fact]
    public void Consecutive_chunks_retain_useful_overlap() {
        var content = string.Join(' ', Enumerable.Range(0, 350)
            .Select(index => $"word{index}"));

        var chunks = _chunker.Chunk([new DocumentPageText(1, content)]);

        Assert.True(chunks.Count >= 2);
        var firstTokens = chunks[0].Content.Split(' ').ToHashSet();
        var secondTokens = chunks[1].Content.Split(' ').ToHashSet();
        Assert.True(firstTokens.Intersect(secondTokens).Count() >= 10);
    }

    [Fact]
    public void Technical_identifiers_are_not_split_when_a_word_boundary_exists() {
        var prefix = string.Join(' ', Enumerable.Repeat("safety", 190));
        var chunks = _chunker.Chunk([
            new DocumentPageText(1, $"{prefix} SOP-CNC-042 emergency-stop verification.")
        ]);

        Assert.Contains(chunks, chunk => chunk.Content.Contains("SOP-CNC-042", StringComparison.Ordinal));
        Assert.DoesNotContain(chunks, chunk => chunk.Content.EndsWith("SOP-CNC", StringComparison.Ordinal));
    }
}
