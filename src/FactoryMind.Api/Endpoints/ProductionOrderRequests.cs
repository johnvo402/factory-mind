using FactoryMind.Application.Features.Boms;
using FactoryMind.Application.Features.ProductionOrders;
using FluentValidation;

namespace FactoryMind.Api.Endpoints;

public sealed record ProductionOrderRequest(
    string Number,
    Guid ProductId,
    decimal Quantity);

public sealed record ProductionMaterialAllocationRequest(
    Guid MaterialId,
    Guid WarehouseId,
    decimal Quantity);

public sealed record StartProductionOrderRequest(
    IReadOnlyList<ProductionMaterialAllocationRequest> Allocations);

public sealed record CompleteProductionOrderRequest(Guid WarehouseId);

public sealed class ProductionOrderRequestValidator : AbstractValidator<ProductionOrderRequest> {
    public ProductionOrderRequestValidator() {
        RuleFor(request => request.Number)
            .NotEmpty().WithMessage("Production order number is required.")
            .MaximumLength(ProductionOrderConstraints.MaximumNumberLength)
            .WithMessage(
                $"Production order number must not exceed {ProductionOrderConstraints.MaximumNumberLength} characters.");
        RuleFor(request => request.ProductId)
            .NotEmpty().WithMessage("Product is required.");
        RuleFor(request => request.Quantity)
            .GreaterThan(0).WithMessage("Quantity must be greater than zero.")
            .PrecisionScale(
                ProductionOrderConstraints.QuantityPrecision,
                ProductionOrderConstraints.QuantityScale,
                ignoreTrailingZeros: true)
            .WithMessage(
                $"Quantity must have at most {ProductionOrderConstraints.QuantityPrecision} digits and " +
                $"{ProductionOrderConstraints.QuantityScale} decimal places.");
    }
}

public sealed class ProductionMaterialAllocationRequestValidator
    : AbstractValidator<ProductionMaterialAllocationRequest> {
    public ProductionMaterialAllocationRequestValidator() {
        RuleFor(request => request.MaterialId)
            .NotEmpty().WithMessage("Material is required.");
        RuleFor(request => request.WarehouseId)
            .NotEmpty().WithMessage("Warehouse is required.");
        RuleFor(request => request.Quantity)
            .GreaterThan(0).WithMessage("Allocation quantity must be greater than zero.")
            .PrecisionScale(
                BomConstraints.QuantityPrecision,
                BomConstraints.QuantityScale,
                ignoreTrailingZeros: true)
            .WithMessage(
                $"Allocation quantity must have at most {BomConstraints.QuantityPrecision} digits and " +
                $"{BomConstraints.QuantityScale} decimal places.");
    }
}

public sealed class StartProductionOrderRequestValidator : AbstractValidator<StartProductionOrderRequest> {
    public StartProductionOrderRequestValidator() {
        RuleFor(request => request.Allocations)
            .NotNull().WithMessage("Material allocations are required.")
            .NotEmpty().WithMessage("At least one material allocation is required.");
        RuleForEach(request => request.Allocations)
            .SetValidator(new ProductionMaterialAllocationRequestValidator());
    }
}

public sealed class CompleteProductionOrderRequestValidator : AbstractValidator<CompleteProductionOrderRequest> {
    public CompleteProductionOrderRequestValidator() {
        RuleFor(request => request.WarehouseId)
            .NotEmpty().WithMessage("Destination warehouse is required.");
    }
}
