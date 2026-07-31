namespace FactoryMind.Application.Features.BusinessData;

public static class BusinessDataConstraints {
    public const int MaximumSearchLength = 200;
}

internal static class BusinessDataNormalization {
    public static string Code(string code) => code.Trim().ToUpperInvariant();
    public static string Name(string name) => name.Trim();
    public static string? Search(string? search) => string.IsNullOrWhiteSpace(search)
        ? null
        : search.Trim();
}
