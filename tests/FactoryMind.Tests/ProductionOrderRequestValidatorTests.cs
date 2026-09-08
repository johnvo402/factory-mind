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

    [Fact]
    public async Task Start_operation_requires_an_explicit_machine() {
        var validator = new StartProductionOrderOperationRequestValidator();

        var result = await validator.TestValidateAsync(
            new StartProductionOrderOperationRequest(Guid.Empty));

        result.ShouldHaveValidationErrorFor(request => request.MachineId)
            .WithErrorMessage("Machine is required.");
    }

    [Theory]
    [InlineData(ProductionOrderPriorities.Low)]
    [InlineData(ProductionOrderPriorities.Normal)]
    [InlineData(ProductionOrderPriorities.High)]
    [InlineData(ProductionOrderPriorities.Urgent)]
    public async Task Accepts_each_typed_priority(string priority) {
        var result = await _validator.TestValidateAsync(new ProductionOrderRequest(
            "PO-001", Guid.NewGuid(), 1m, DateTime.UtcNow.AddDays(-1), priority));

        result.ShouldNotHaveValidationErrorFor(request => request.Priority);
    }

    [Fact]
    public async Task Rejects_invalid_priority_without_rejecting_overdue_due_date() {
        var result = await _validator.TestValidateAsync(new ProductionOrderRequest(
            "PO-001", Guid.NewGuid(), 1m, DateTime.UtcNow.AddYears(-1), "critical"));

        result.ShouldHaveValidationErrorFor(request => request.Priority);
        result.ShouldNotHaveValidationErrorFor(request => request.DueDate);
    }
}
