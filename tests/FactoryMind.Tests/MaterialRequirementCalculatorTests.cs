using FactoryMind.Application.Features.Boms;
using FactoryMind.Domain.Manufacturing;

namespace FactoryMind.Tests;

public sealed class MaterialRequirementCalculatorTests {
    [Fact]
    public void Calculator_applies_output_factor_scrap_shortage_and_can_produce() {
        var steel = Material("STEEL", "Steel", "kg");
        var fabric = Material("FABRIC", "Fabric", "m");
        var bom = Bom(1, [
            Item(steel, 2m, 5m),
            Item(fabric, 0.5m, null)
        ]);

        var result = new MaterialRequirementCalculator().Calculate(
            bom,
            100m,
            new Dictionary<Guid, decimal> {
                [steel.Id] = 250m,
                [fabric.Id] = 30m
            });

        var steelRequirement = result.Materials.Single(item => item.MaterialId == steel.Id);
        Assert.Equal(210m, steelRequirement.RequiredQuantity);
        Assert.Equal(0m, steelRequirement.ShortageQuantity);
        Assert.True(steelRequirement.IsSufficient);
        var fabricRequirement = result.Materials.Single(item => item.MaterialId == fabric.Id);
        Assert.Equal(50m, fabricRequirement.RequiredQuantity);
        Assert.Equal(20m, fabricRequirement.ShortageQuantity);
        Assert.False(fabricRequirement.IsSufficient);
        Assert.False(result.CanProduce);
    }

    [Fact]
    public void Calculator_scales_item_quantity_by_bom_output() {
        var steel = Material("STEEL", "Steel", "kg");
        var bom = Bom(10m, [Item(steel, 25m, null)]);

        var result = new MaterialRequirementCalculator().Calculate(
            bom,
            100m,
            new Dictionary<Guid, decimal> { [steel.Id] = 300m });

        var requirement = Assert.Single(result.Materials);
        Assert.Equal(250m, requirement.RequiredQuantity);
        Assert.Equal(300m, requirement.AvailableQuantity);
        Assert.True(result.CanProduce);
    }

    private static Material Material(string code, string name, string unit) => new() {
        Code = code,
        Name = name,
        Unit = unit
    };

    private static BomItem Item(Material material, decimal quantity, decimal? scrap) => new() {
        MaterialId = material.Id,
        Material = material,
        Quantity = quantity,
        ScrapPercentage = scrap
    };

    private static BillOfMaterial Bom(decimal outputQuantity, ICollection<BomItem> items) => new() {
        ProductId = Guid.NewGuid(),
        Product = new Product { Code = "PRD-001", Name = "Chair" },
        Revision = 2,
        OutputQuantity = outputQuantity,
        Status = BillOfMaterialStatuses.Active,
        Items = items
    };
}
