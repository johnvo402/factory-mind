using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace FactoryMind.Application.Common.Search;

public static partial class SearchTextNormalizer {
    public static string NormalizeLexical(string value) {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormC)) {
            builder.Append(char.IsLetterOrDigit(character)
                || char.IsWhiteSpace(character)
                || character == '-'
                    ? character
                    : ' ');
        }

        return string.Join(' ', builder
            .ToString()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    public static string Normalize(string value) {
        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed) {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) {
                continue;
            }

            var normalizedCharacter = character == 'đ' ? 'd' : character;
            builder.Append(char.IsLetterOrDigit(normalizedCharacter)
                || char.IsWhiteSpace(normalizedCharacter)
                || normalizedCharacter == '-'
                    ? normalizedCharacter
                    : ' ');
        }

        return string.Join(' ', builder
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    public static IReadOnlySet<string> Tokens(string value) => Normalize(value)
        .Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries)
        .ToHashSet(StringComparer.Ordinal);

    public static IReadOnlyList<string> LexicalTokens(string value) => NormalizeLexical(value)
        .Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries)
        .Distinct(StringComparer.Ordinal)
        .ToList();

    public static IReadOnlySet<string> Identifiers(string value) => IdentifierPattern()
        .Matches(Normalize(value))
        .Select(match => match.Value)
        .ToHashSet(StringComparer.Ordinal);

    [GeneratedRegex(@"(?<![a-z0-9])[a-z0-9]+(?:-[a-z0-9]+)+(?![a-z0-9])", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierPattern();
}
