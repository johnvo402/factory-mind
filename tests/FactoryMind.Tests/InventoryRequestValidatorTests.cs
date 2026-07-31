using FactoryMind.Api.Endpoints;
using FluentValidation.TestHelper;

namespace FactoryMind.Tests;

public sealed class InventoryRequestValidatorTests {
    private readonly InventoryRequestValidator _validator = new();

    [Fact]
    public async Task Rejects_missing_material_warehouse_and_negative_quantity() {
        var result = await _validator.TestValidateAsync(
            new InventoryRequest(Guid.Empty, string.Empty, -1m));

        result.ShouldHaveValidationErrorFor(request => request.MaterialId);
        result.ShouldHaveValidationErrorFor(request => request.Warehouse);
        result.ShouldHaveValidationErrorFor(request => request.Quantity);
    }

    [Fact]
    public async Task Accepts_zero_quantity_with_three_decimal_places() {
        var result = await _validator.TestValidateAsync(
            new InventoryRequest(Guid.NewGuid(), "Main Warehouse", 0.125m));

        result.ShouldNotHaveAnyValidationErrors();
    }
}
