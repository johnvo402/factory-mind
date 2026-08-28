using FactoryMind.Application.Features.Boms;
using FluentValidation;

namespace FactoryMind.Api.Endpoints;

public sealed record BomItemRequest(
    Guid MaterialId,
    decimal Quantity,
    decimal? ScrapPercentage);

public sealed record BomRequest(
    decimal OutputQuantity,
    IReadOnlyList<BomItemRequest> Items);

public sealed record MaterialRequirementRequest(decimal Quantity);

public sealed class BomItemRequestValidator : AbstractValidator<BomItemRequest> {
    public BomItemRequestValidator() {
        RuleFor(request => request.MaterialId)
            .NotEmpty().WithMessage("Material is required.");
        RuleFor(request => request.Quantity)
            .GreaterThan(0).WithMessage("BOM item quantity must be greater than zero.")
            .PrecisionScale(BomConstraints.QuantityPrecision, BomConstraints.QuantityScale, true)
            .WithMessage(
                $"BOM item quantity must have at most {BomConstraints.QuantityPrecision} digits and " +
                $"{BomConstraints.QuantityScale} decimal places.");
        RuleFor(request => request.ScrapPercentage)
            .InclusiveBetween(0m, BomConstraints.MaximumScrapPercentage)
            .When(request => request.ScrapPercentage.HasValue)
            .WithMessage(
                $"Scrap percentage must be between 0 and {BomConstraints.MaximumScrapPercentage}.")
            .PrecisionScale(BomConstraints.ScrapPrecision, BomConstraints.ScrapScale, true)
            .When(request => request.ScrapPercentage.HasValue)
            .WithMessage(
                $"Scrap percentage must have at most {BomConstraints.ScrapPrecision} digits and " +
                $"{BomConstraints.ScrapScale} decimal places.");
    }
}

public sealed class BomRequestValidator : AbstractValidator<BomRequest> {
    public BomRequestValidator() {
        RuleFor(request => request.OutputQuantity)
            .GreaterThan(0).WithMessage("BOM output quantity must be greater than zero.")
            .PrecisionScale(BomConstraints.QuantityPrecision, BomConstraints.QuantityScale, true)
            .WithMessage(
                $"BOM output quantity must have at most {BomConstraints.QuantityPrecision} digits and " +
                $"{BomConstraints.QuantityScale} decimal places.");
        RuleFor(request => request.Items)
            .NotNull().WithMessage("BOM items are required.")
            .Must(items => items is null || items.Select(item => item.MaterialId).Distinct().Count() == items.Count)
            .WithMessage("A material can appear only once in a bill of materials.");
        RuleForEach(request => request.Items).SetValidator(new BomItemRequestValidator());
    }
}

public sealed class MaterialRequirementRequestValidator : AbstractValidator<MaterialRequirementRequest> {
    public MaterialRequirementRequestValidator() {
        RuleFor(request => request.Quantity)
            .GreaterThan(0).WithMessage("Requested production quantity must be greater than zero.")
            .PrecisionScale(BomConstraints.QuantityPrecision, BomConstraints.QuantityScale, true)
            .WithMessage(
                $"Requested quantity must have at most {BomConstraints.QuantityPrecision} digits and " +
                $"{BomConstraints.QuantityScale} decimal places.");
    }
}
