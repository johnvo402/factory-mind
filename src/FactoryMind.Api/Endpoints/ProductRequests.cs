using FactoryMind.Application.Features.Products;
using FluentValidation;

namespace FactoryMind.Api.Endpoints;

public sealed record ProductRequest(string Code, string Name);

public sealed class ProductRequestValidator : AbstractValidator<ProductRequest> {
    public ProductRequestValidator() {
        RuleFor(request => request.Code)
            .NotEmpty().WithMessage("Product code is required.")
            .MaximumLength(ProductConstraints.MaximumCodeLength)
            .WithMessage($"Product code must not exceed {ProductConstraints.MaximumCodeLength} characters.");
        RuleFor(request => request.Name)
            .NotEmpty().WithMessage("Product name is required.")
            .MaximumLength(ProductConstraints.MaximumNameLength)
            .WithMessage($"Product name must not exceed {ProductConstraints.MaximumNameLength} characters.");
    }
}
