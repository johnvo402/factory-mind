using FactoryMind.Api.Endpoints;
using FactoryMind.Domain.Manufacturing;
using FluentValidation.TestHelper;

namespace FactoryMind.Tests;

public sealed class ProductionOrderRequestValidatorTests {
    private readonly ProductionOrderRequestValidator _validator = new();

    [Fact]
    public async Task Rejects_missing_fields_and_non_positive_quantity() {
        var result = await _validator.TestValidateAsync(
            new ProductionOrderRequest(string.Empty, Guid.Empty, 0m));

        result.ShouldHaveValidationErrorFor(request => request.Number);
        result.ShouldHaveValidationErrorFor(request => request.ProductId);
        result.ShouldHaveValidationErrorFor(request => request.Quantity);
    }

    [Fact]
    public async Task Accepts_valid_planning_data_without_status() {
        var result = await _validator.TestValidateAsync(new ProductionOrderRequest(
            "PO-001",
            Guid.NewGuid(),
            500.125m));

        result.ShouldNotHaveAnyValidationErrors();
    }
}
