using FactoryMind.Application.Features.ProductionOrders;
using FactoryMind.Domain.Manufacturing;
using FluentValidation;

namespace FactoryMind.Api.Endpoints;

public sealed record ProductionOrderRequest(
    string Number,
    Guid ProductId,
    decimal Quantity,
    string Status);

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
        RuleFor(request => request.Status)
            .Must(status => status is not null &&
                ProductionOrderStatuses.All.Contains(status.Trim().ToLowerInvariant()))
            .WithMessage("Production order status is invalid.");
    }
}
