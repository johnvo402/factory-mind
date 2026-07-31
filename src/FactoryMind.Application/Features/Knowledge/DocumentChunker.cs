namespace FactoryMind.Application.Features.Knowledge;

public sealed class DocumentChunker {
    public const int TargetLength = 1200;
    public const int OverlapLength = 200;

    public IReadOnlyList<DocumentChunkDraft> Chunk(IReadOnlyList<DocumentPageText> pages) {
        var chunks = new List<DocumentChunkDraft>();

        foreach (var page in pages.OrderBy(page => page.PageNumber)) {
            var content = NormalizeWhitespace(page.Content);
            var start = 0;

            while (start < content.Length) {
                var end = FindEnd(content, start);
                var chunk = content[start..end].Trim();
                if (chunk.Length > 0) {
                    chunks.Add(new DocumentChunkDraft(chunks.Count, page.PageNumber, chunk));
                }

                if (end >= content.Length) {
                    break;
                }

                start = FindNextStart(content, start, end);
            }
        }

        return chunks;
    }

    private static string NormalizeWhitespace(string content) =>
        string.Join(' ', content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static int FindEnd(string content, int start) {
        var candidate = Math.Min(start + TargetLength, content.Length);
        if (candidate == content.Length) {
            return candidate;
        }

        var boundary = content.LastIndexOf(' ', candidate - 1, candidate - start);
        return boundary > start ? boundary : candidate;
    }

    private static int FindNextStart(string content, int previousStart, int end) {
        var start = Math.Max(previousStart + 1, end - OverlapLength);
        while (start < end && !char.IsWhiteSpace(content[start])) {
            start++;
        }

        while (start < content.Length && char.IsWhiteSpace(content[start])) {
            start++;
        }

        return start;
    }
}
