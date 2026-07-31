using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;

namespace FactoryMind.Application.Features.ExcelImports;

public static class ExcelImportConstraints {
    public const long MaximumFileSize = 10 * 1024 * 1024;
    public const long MaximumRequestSize = MaximumFileSize + (1024 * 1024);
    public const int MaximumRows = 5_000;
    public const int MaximumColumns = 50;
    public const int PreviewRows = 10;
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}

public static class ExcelImportEntityTypes {
    public const string Machine = "machine";
    public const string Material = "material";
    public const string Product = "product";
    public const string Inventory = "inventory";
    public const string ProductionOrder = "production_order";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) {
        Machine,
        Material,
        Product,
        Inventory,
        ProductionOrder
    };
}

public sealed record ExcelWorkbookData(
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyDictionary<string, string>> Rows,
    int TotalRows);

public sealed record ExcelPreviewResponse(
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyDictionary<string, string>> Rows,
    int TotalRows,
    IReadOnlyList<string> RequiredFields,
    IReadOnlyDictionary<string, string> SuggestedMapping);

public sealed record ExcelRowError(int Row, string Field, string Message);

public sealed record ExcelImportResponse(
    int ImportedCount,
    IReadOnlyList<ExcelRowError> Errors);

public sealed record ExcelImportReferenceData(
    IReadOnlySet<string> ExistingKeys,
    IReadOnlyDictionary<string, Guid> RelatedIds);

public sealed record ExcelImportBatch(
    IReadOnlyList<Machine> Machines,
    IReadOnlyList<Material> Materials,
    IReadOnlyList<Product> Products,
    IReadOnlyList<Inventory> Inventories,
    IReadOnlyList<ProductionOrder> ProductionOrders) {
    public int Count => Machines.Count
        + Materials.Count
        + Products.Count
        + Inventories.Count
        + ProductionOrders.Count;
}

public interface IExcelWorkbookReader {
    Task<ExcelWorkbookData> ReadAsync(
        Stream content,
        int maximumRows,
        CancellationToken cancellationToken);
}

public interface IExcelImportRepository {
    Task<ExcelImportReferenceData> GetReferenceDataAsync(
        Guid companyId,
        string entityType,
        CancellationToken cancellationToken);

    void Add(ExcelImportBatch batch);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class ExcelWorkbookException : Exception {
    public ExcelWorkbookException(string message, Exception? innerException = null)
        : base(message, innerException) {
    }
}

public static class ExcelImportErrors {
    public static readonly Error InvalidWorkbook = new(
        "excel_import.invalid_workbook",
        "The Excel workbook is invalid or exceeds the allowed limits.",
        422);

    public static readonly Error InvalidEntityType = new(
        "excel_import.invalid_entity_type",
        "The Excel import entity type is not supported.",
        422);

    public static readonly Error InvalidMapping = new(
        "excel_import.invalid_mapping",
        "The Excel column mapping is incomplete or invalid.",
        422);
}
