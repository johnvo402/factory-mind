using FactoryMind.Api.Endpoints;

namespace FactoryMind.Tests;

public sealed class BomRequestValidatorTests {
    [Fact]
    public async Task Validator_rejects_duplicate_material_and_invalid_values() {
        var materialId = Guid.NewGuid();
        var request = new BomRequest(0, [
            new BomItemRequest(materialId, 0, -1),
            new BomItemRequest(materialId, 1, 101)
        ]);

        var result = await new BomRequestValidator().ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(BomRequest.OutputQuantity));
        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(BomRequest.Items));
        Assert.Contains(result.Errors, failure => failure.PropertyName.EndsWith("Quantity", StringComparison.Ordinal));
        Assert.Contains(result.Errors, failure => failure.PropertyName.EndsWith("ScrapPercentage", StringComparison.Ordinal));
    }
}
