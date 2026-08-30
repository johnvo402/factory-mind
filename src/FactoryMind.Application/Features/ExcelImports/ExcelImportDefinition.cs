namespace FactoryMind.Application.Features.ExcelImports;

public static class ExcelImportDefinition {
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Fields =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) {
            [ExcelImportEntityTypes.Machine] = ["code", "name", "status"],
            [ExcelImportEntityTypes.Material] = ["code", "name", "unit"],
            [ExcelImportEntityTypes.Product] = ["code", "name"],
            [ExcelImportEntityTypes.Inventory] = ["materialCode", "warehouseCode", "quantity"],
            [ExcelImportEntityTypes.ProductionOrder] = ["number", "productCode", "quantity"]
        };

    public static IReadOnlyList<string>? GetRequiredFields(string entityType) =>
        Fields.GetValueOrDefault(entityType);

    public static IReadOnlyDictionary<string, string> SuggestMapping(
        IReadOnlyList<string> headers,
        IReadOnlyList<string> fields) {
        var normalizedHeaders = headers
            .GroupBy(Normalize)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var mapping = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in fields) {
            if (normalizedHeaders.TryGetValue(Normalize(field), out var header)) {
                mapping[field] = header;
            }
        }

        return mapping;
    }

    private static string Normalize(string value) => string.Concat(
        value.Where(char.IsLetterOrDigit)).ToLowerInvariant();
}
