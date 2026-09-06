using System.Text;
using System.Text.RegularExpressions;

namespace FactoryMind.Application.Features.Knowledge;

public sealed partial class DocumentChunker {
    public const int TargetLength = 1_200;
    public const int MaximumLength = 1_400;
    public const int OverlapLength = 200;
    private const int MinimumBoundarySearchLength = 600;

    public IReadOnlyList<DocumentChunkDraft> Chunk(IReadOnlyList<DocumentPageText> pages) {
        var chunks = new List<DocumentChunkDraft>();

        foreach (var page in pages.OrderBy(page => page.PageNumber)) {
            var content = NormalizePage(page.Content);
            var start = 0;

            while (start < content.Length) {
                var end = FindPreferredEnd(content, start);
                var chunk = content[start..end].Trim();
                if (chunk.Length > 0) {
                    chunks.Add(new DocumentChunkDraft(chunks.Count, page.PageNumber, chunk));
                }

                if (end >= content.Length) {
                    break;
                }

                start = FindOverlapStart(content, start, end);
            }
        }

        return chunks;
    }

    private static string NormalizePage(string content) {
        var normalizedLines = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => InlineWhitespace().Replace(line.Trim(), " "))
            .ToList();
        var result = new StringBuilder(content.Length);
        var previousBlank = true;

        foreach (var line in normalizedLines) {
            if (line.Length == 0) {
                if (!previousBlank && result.Length > 0) {
                    result.Append("\n\n");
                }

                previousBlank = true;
                continue;
            }

            if (!previousBlank && result.Length > 0) {
                result.Append(' ');
            }

            result.Append(line);
            previousBlank = false;
        }

        return result.ToString().Trim();
    }

    private static int FindPreferredEnd(string content, int start) {
        if (content.Length - start <= MaximumLength) {
            return content.Length;
        }

        var target = Math.Min(start + TargetLength, content.Length);
        var minimum = Math.Min(start + MinimumBoundarySearchLength, target);

        var paragraph = content.LastIndexOf("\n\n", target, target - minimum + 1, StringComparison.Ordinal);
        if (paragraph >= minimum) {
            return paragraph;
        }

        for (var index = target - 1; index >= minimum; index--) {
            if (IsSentenceBoundary(content, index)) {
                return index + 1;
            }
        }

        var whitespace = content.LastIndexOfAny([' ', '\n'], target - 1, target - minimum);
        return whitespace >= minimum ? whitespace : Math.Min(start + MaximumLength, content.Length);
    }

    private static int FindOverlapStart(string content, int previousStart, int end) {
        var ideal = Math.Max(previousStart + 1, end - OverlapLength);
        var upperBound = Math.Min(end - 1, ideal + (OverlapLength / 2));

        for (var index = ideal; index <= upperBound; index++) {
            if (index > 0 && IsSentenceBoundary(content, index - 1)) {
                return SkipWhitespace(content, index);
            }

            if (index + 1 < content.Length && content[index] == '\n' && content[index + 1] == '\n') {
                return SkipWhitespace(content, index + 2);
            }
        }

        while (ideal < end && !char.IsWhiteSpace(content[ideal])) {
            ideal++;
        }

        return SkipWhitespace(content, ideal);
    }

    private static int SkipWhitespace(string content, int start) {
        while (start < content.Length && char.IsWhiteSpace(content[start])) {
            start++;
        }

        return start;
    }

    private static bool IsSentenceBoundary(string content, int index) =>
        index >= 0
        && index < content.Length
        && content[index] is '.' or '!' or '?'
        && (index + 1 == content.Length || char.IsWhiteSpace(content[index + 1]));

    [GeneratedRegex(@"[\t\f\v ]+", RegexOptions.CultureInvariant)]
    private static partial Regex InlineWhitespace();
}
