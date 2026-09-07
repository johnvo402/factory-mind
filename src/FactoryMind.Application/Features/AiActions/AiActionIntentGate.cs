using System.Text.RegularExpressions;

namespace FactoryMind.Application.Features.AiActions;

public sealed partial class AiActionIntentGate {
    public bool IsEligible(string text) {
        var normalized = text.Trim();
        if (normalized.Length == 0
            || QuestionPattern().IsMatch(normalized)
            || ConfirmationOnlyPattern().IsMatch(normalized)
            || BypassPattern().IsMatch(normalized)
            || UnsupportedWritePattern().IsMatch(normalized)) {
            return false;
        }

        var identifiers = ProductionOrderPattern().Matches(normalized)
            .Select(match => match.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .Count();
        return identifiers == 1 && ExplicitReleasePattern().IsMatch(normalized);
    }

    public bool IsMultipleOrderRequest(string text) => ProductionOrderPattern().Matches(text)
        .Select(match => match.Value)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Take(2)
        .Count() > 1;

    [GeneratedRegex(@"\b(should|can|could|would|why|what|status|whether|nên|có nên|đã|chưa|tại sao|nếu)\b|\?", RegexOptions.IgnoreCase)]
    private static partial Regex QuestionPattern();

    [GeneratedRegex(@"^(yes|ok|okay|oke|confirm|do it|làm đi|đồng ý)[.!\s]*$", RegexOptions.IgnoreCase)]
    private static partial Regex ConfirmationOnlyPattern();

    [GeneratedRegex(@"ignore (all )?(previous )?instructions|without confirmation|bypass|no confirmation|không cần xác nhận", RegexOptions.IgnoreCase)]
    private static partial Regex BypassPattern();

    [GeneratedRegex(@"\b(start|cancel|complete|assign|update|delete|create|adjust|transfer|receive|issue|activate|offline|bắt đầu|hủy|hoàn thành)\b", RegexOptions.IgnoreCase)]
    private static partial Regex UnsupportedWritePattern();

    [GeneratedRegex(@"\b(release|phát hành)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ExplicitReleasePattern();

    [GeneratedRegex(@"\bPO-[A-Za-z0-9-]+\b", RegexOptions.IgnoreCase)]
    private static partial Regex ProductionOrderPattern();
}
