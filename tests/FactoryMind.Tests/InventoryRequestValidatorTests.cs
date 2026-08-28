using FactoryMind.Api.Endpoints;
using FactoryMind.Domain.Manufacturing;
using FluentValidation.TestHelper;

namespace FactoryMind.Tests;

public sealed class InventoryRequestValidatorTests {
    [Fact]
    public async Task Movement_rejects_missing_ids_and_non_positive_quantity() {
        var validator = new InventoryMovementRequestValidator();

        var result = await validator.TestValidateAsync(
            new InventoryMovementRequest(Guid.Empty, Guid.Empty, 0m, null, null, null));

        result.ShouldHaveValidationErrorFor(request => request.WarehouseId);
        result.ShouldHaveValidationErrorFor(request => request.MaterialId);
        result.ShouldHaveValidationErrorFor(request => request.Quantity);
    }

    [Fact]
    public async Task Adjustment_requires_a_reason() {
        var validator = new InventoryAdjustmentRequestValidator();

        var result = await validator.TestValidateAsync(new InventoryAdjustmentRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            InventoryAdjustmentDirection.Increase,
            1m,
            string.Empty,
            null,
            null));

        result.ShouldHaveValidationErrorFor(request => request.Note);
    }

    [Fact]
    public async Task Transfer_rejects_same_warehouse() {
        var warehouseId = Guid.NewGuid();
        var validator = new InventoryTransferRequestValidator();

        var result = await validator.TestValidateAsync(new InventoryTransferRequest(
            warehouseId, warehouseId, Guid.NewGuid(), 1m, null, null));

        result.ShouldHaveValidationErrorFor(request => request.DestinationWarehouseId);
    }
}
