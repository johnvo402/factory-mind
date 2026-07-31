using FactoryMind.Api.Endpoints;
using FactoryMind.Domain.Manufacturing;
using FluentValidation.TestHelper;

namespace FactoryMind.Tests;

public sealed class ProductionOrderRequestValidatorTests {
    private readonly ProductionOrderRequestValidator _validator = new();

    [Fact]
    public async Task Rejects_missing_fields_non_positive_quantity_and_unknown_status() {
        var result = await _validator.TestValidateAsync(
            new ProductionOrderRequest(string.Empty, Guid.Empty, 0m, "unknown"));

        result.ShouldHaveValidationErrorFor(request => request.Number);
        result.ShouldHaveValidationErrorFor(request => request.ProductId);
        result.ShouldHaveValidationErrorFor(request => request.Quantity);
        result.ShouldHaveValidationErrorFor(request => request.Status);
    }

    [Fact]
    public async Task Accepts_known_status_case_insensitively() {
        var result = await _validator.TestValidateAsync(new ProductionOrderRequest(
            "PO-001",
            Guid.NewGuid(),
            500.125m,
            ProductionOrderStatuses.InProgress.ToUpperInvariant()));

        result.ShouldNotHaveAnyValidationErrors();
    }
}
