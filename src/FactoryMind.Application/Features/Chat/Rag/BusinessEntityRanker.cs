using FactoryMind.Application.Common.Search;

namespace FactoryMind.Application.Features.Chat.Rag;

public static class BusinessEntityRanker {
    public static int Score(string question, string codeOrNumber, string name) {
        var query = SearchTextNormalizer.Normalize(question);
        var code = SearchTextNormalizer.Normalize(codeOrNumber);
        var normalizedName = SearchTextNormalizer.Normalize(name);
        var identifiers = SearchTextNormalizer.Identifiers(question);
        if (code.Length > 0 && identifiers.Contains(code)) {
            return 1_000;
        }

        if (code.Length > 0 && ContainsTerm(query, code)) {
            return 900;
        }

        if (normalizedName.Length > 0 && query.Contains(normalizedName, StringComparison.Ordinal)) {
            return 700;
        }

        var queryTokens = SearchTextNormalizer.Tokens(query);
        var codeTokens = SearchTextNormalizer.Tokens(code);
        if (codeTokens.Count > 0 && codeTokens.All(queryTokens.Contains)) {
            return 500;
        }

        var nameTokens = SearchTextNormalizer.Tokens(normalizedName);
        var matchingNameTokens = nameTokens.Count(queryTokens.Contains);
        return matchingNameTokens == 0
            ? 0
            : 100 + (int)Math.Round(300d * matchingNameTokens / nameTokens.Count);
    }

    private static bool ContainsTerm(string text, string term) {
        var index = text.IndexOf(term, StringComparison.Ordinal);
        while (index >= 0) {
            var before = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
            var end = index + term.Length;
            var after = end == text.Length || !char.IsLetterOrDigit(text[end]);
            if (before && after) {
                return true;
            }

            index = text.IndexOf(term, index + 1, StringComparison.Ordinal);
        }

        return false;
    }
}
