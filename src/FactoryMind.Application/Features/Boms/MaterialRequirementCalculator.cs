using FactoryMind.Domain.Manufacturing;

namespace FactoryMind.Application.Features.Boms;

public sealed class MaterialRequirementCalculator {
    public MaterialRequirementsResponse Calculate(
        BillOfMaterial bom,
        decimal requestedQuantity,
        IReadOnlyDictionary<Guid, decimal> availableQuantities) {
        var productionFactor = requestedQuantity / bom.OutputQuantity;
        var materials = bom.Items
            .OrderBy(item => item.Material?.Code, StringComparer.Ordinal)
            .Select(item => CalculateItem(item, productionFactor, availableQuantities))
            .ToList();

        return new MaterialRequirementsResponse(
            bom.ProductId,
            bom.Product?.Code ?? string.Empty,
            bom.Product?.Name ?? string.Empty,
            bom.Id,
            bom.Revision,
            requestedQuantity,
            materials.All(material => material.IsSufficient),
            materials);
    }

    private static MaterialRequirementItemResponse CalculateItem(
        BomItem item,
        decimal productionFactor,
        IReadOnlyDictionary<Guid, decimal> availableQuantities) {
        var scrapMultiplier = 1m + (item.ScrapPercentage ?? 0m) / 100m;
        var requiredQuantity = RoundQuantity(item.Quantity * productionFactor * scrapMultiplier);
        var availableQuantity = availableQuantities.GetValueOrDefault(item.MaterialId);
        var shortageQuantity = Math.Max(RoundQuantity(requiredQuantity - availableQuantity), 0m);

        return new MaterialRequirementItemResponse(
            item.MaterialId,
            item.Material?.Code ?? string.Empty,
            item.Material?.Name ?? string.Empty,
            item.Material?.Unit ?? string.Empty,
            item.Quantity,
            item.ScrapPercentage,
            requiredQuantity,
            availableQuantity,
            shortageQuantity,
            availableQuantity >= requiredQuantity);
    }

    public static decimal RoundQuantity(decimal value) => decimal.Round(
        value,
        BomConstraints.QuantityScale,
        MidpointRounding.AwayFromZero);
}
