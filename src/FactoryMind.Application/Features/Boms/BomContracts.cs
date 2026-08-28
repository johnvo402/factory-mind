using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;

namespace FactoryMind.Application.Features.Boms;

public static class BomConstraints {
    public const int QuantityPrecision = 18;
    public const int QuantityScale = 6;
    public const int ScrapPrecision = 5;
    public const int ScrapScale = 2;
    public const decimal MaximumScrapPercentage = 100m;
    public const int MaximumStatusLength = 20;
}

public sealed record BomItemDefinition(
    Guid MaterialId,
    decimal Quantity,
    decimal? ScrapPercentage);

public sealed record BomItemResponse(
    Guid Id,
    Guid MaterialId,
    string MaterialCode,
    string MaterialName,
    string Unit,
    decimal Quantity,
    decimal? ScrapPercentage,
    DateTime CreatedAt,
    DateTime UpdatedAt) {
    public static BomItemResponse From(BomItem item) => new(
        item.Id,
        item.MaterialId,
        item.Material?.Code ?? string.Empty,
        item.Material?.Name ?? string.Empty,
        item.Material?.Unit ?? string.Empty,
        item.Quantity,
        item.ScrapPercentage,
        item.CreatedAt,
        item.UpdatedAt);
}

public sealed record BomResponse(
    Guid Id,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    int Revision,
    decimal OutputQuantity,
    string Status,
    IReadOnlyList<BomItemResponse> Items,
    DateTime CreatedAt,
    DateTime UpdatedAt) {
    public static BomResponse From(BillOfMaterial bom) => new(
        bom.Id,
        bom.ProductId,
        bom.Product?.Code ?? string.Empty,
        bom.Product?.Name ?? string.Empty,
        bom.Revision,
        bom.OutputQuantity,
        bom.Status,
        bom.Items
            .OrderBy(item => item.Material?.Code, StringComparer.Ordinal)
            .Select(BomItemResponse.From)
            .ToList(),
        bom.CreatedAt,
        bom.UpdatedAt);
}

public sealed record MaterialRequirementItemResponse(
    Guid MaterialId,
    string MaterialCode,
    string MaterialName,
    string Unit,
    decimal QuantityPerBom,
    decimal? ScrapPercentage,
    decimal RequiredQuantity,
    decimal AvailableQuantity,
    decimal ShortageQuantity,
    bool IsSufficient);

public sealed record MaterialRequirementsResponse(
    Guid ProductId,
    string ProductCode,
    string ProductName,
    Guid BomId,
    int BomRevision,
    decimal RequestedQuantity,
    bool CanProduce,
    IReadOnlyList<MaterialRequirementItemResponse> Materials);

public interface IBomRepository {
    Task<IReadOnlyList<BillOfMaterial>> GetByProductAsync(
        Guid productId,
        Guid companyId,
        CancellationToken cancellationToken);

    Task<BillOfMaterial?> GetByIdAsync(
        Guid bomId,
        Guid productId,
        Guid companyId,
        CancellationToken cancellationToken);

    Task<BillOfMaterial?> GetActiveAsync(
        Guid productId,
        Guid companyId,
        CancellationToken cancellationToken);

    Task<int> GetNextRevisionAsync(
        Guid productId,
        Guid companyId,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, decimal>> GetAvailableQuantitiesAsync(
        Guid companyId,
        IReadOnlyCollection<Guid> materialIds,
        CancellationToken cancellationToken);

    void Add(BillOfMaterial bom);
    Task SaveChangesAsync(CancellationToken cancellationToken);

    Task ActivateAsync(
        BillOfMaterial bom,
        DateTime activatedAt,
        CancellationToken cancellationToken);
}

public static class BomErrors {
    public static readonly Error NotFound = new(
        "boms.not_found",
        "Bill of materials was not found.",
        404);

    public static readonly Error ActiveNotFound = new(
        "boms.active_not_found",
        "The product does not have an active bill of materials.",
        409);

    public static readonly Error MaterialNotFound = new(
        "boms.material_not_found",
        "Material was not found.",
        404);

    public static readonly Error OutputQuantityInvalid = new(
        "boms.output_quantity_invalid",
        "BOM output quantity must be greater than zero.",
        400);

    public static readonly Error ItemQuantityInvalid = new(
        "boms.item_quantity_invalid",
        "Every BOM item quantity must be greater than zero.",
        400);

    public static readonly Error ScrapPercentageInvalid = new(
        "boms.scrap_percentage_invalid",
        $"BOM item scrap percentage must be between 0 and {BomConstraints.MaximumScrapPercentage}.",
        400);

    public static readonly Error DuplicateMaterial = new(
        "boms.duplicate_material",
        "A material can appear only once in a bill of materials.",
        409);

    public static readonly Error DraftRequired = new(
        "boms.draft_required",
        "Only a draft bill of materials can be changed or activated.",
        409);

    public static readonly Error ItemsRequired = new(
        "boms.items_required",
        "A bill of materials must contain at least one item before activation.",
        409);

    public static readonly Error AlreadyArchived = new(
        "boms.already_archived",
        "The bill of materials is already archived.",
        409);

    public static readonly Error RequestedQuantityInvalid = new(
        "boms.requested_quantity_invalid",
        "Requested production quantity must be greater than zero.",
        400);
}

public static class BomSpecificationValidation {
    public static Error? Validate(decimal outputQuantity, IReadOnlyList<BomItemDefinition> items) {
        if (outputQuantity <= 0) {
            return BomErrors.OutputQuantityInvalid;
        }

        if (items.Any(item => item.Quantity <= 0)) {
            return BomErrors.ItemQuantityInvalid;
        }

        if (items.Any(item => item.ScrapPercentage is < 0 or > BomConstraints.MaximumScrapPercentage)) {
            return BomErrors.ScrapPercentageInvalid;
        }

        return items.Select(item => item.MaterialId).Distinct().Count() == items.Count
            ? null
            : BomErrors.DuplicateMaterial;
    }
}
