using FactoryMind.Application.Features.Inventories;
using FluentValidation;

namespace FactoryMind.Api.Endpoints;

public sealed record InventoryRequest(Guid MaterialId, string Warehouse, decimal Quantity);

public sealed class InventoryRequestValidator : AbstractValidator<InventoryRequest> {
    public InventoryRequestValidator() {
        RuleFor(request => request.MaterialId)
            .NotEmpty().WithMessage("Material is required.");
        RuleFor(request => request.Warehouse)
            .NotEmpty().WithMessage("Warehouse is required.")
            .MaximumLength(InventoryConstraints.MaximumWarehouseLength)
            .WithMessage($"Warehouse must not exceed {InventoryConstraints.MaximumWarehouseLength} characters.");
        RuleFor(request => request.Quantity)
            .GreaterThanOrEqualTo(0).WithMessage("Quantity must be greater than or equal to zero.")
            .PrecisionScale(
                InventoryConstraints.QuantityPrecision,
                InventoryConstraints.QuantityScale,
                ignoreTrailingZeros: true)
            .WithMessage(
                $"Quantity must have at most {InventoryConstraints.QuantityPrecision} digits and " +
                $"{InventoryConstraints.QuantityScale} decimal places.");
    }
}
