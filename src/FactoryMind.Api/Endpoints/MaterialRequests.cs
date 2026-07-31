using FactoryMind.Application.Features.Materials;
using FluentValidation;

namespace FactoryMind.Api.Endpoints;

public sealed record MaterialRequest(string Code, string Name, string Unit);

public sealed class MaterialRequestValidator : AbstractValidator<MaterialRequest> {
    public MaterialRequestValidator() {
        RuleFor(request => request.Code)
            .NotEmpty().WithMessage("Material code is required.")
            .MaximumLength(MaterialConstraints.MaximumCodeLength)
            .WithMessage($"Material code must not exceed {MaterialConstraints.MaximumCodeLength} characters.");
        RuleFor(request => request.Name)
            .NotEmpty().WithMessage("Material name is required.")
            .MaximumLength(MaterialConstraints.MaximumNameLength)
            .WithMessage($"Material name must not exceed {MaterialConstraints.MaximumNameLength} characters.");
        RuleFor(request => request.Unit)
            .NotEmpty().WithMessage("Material unit is required.")
            .MaximumLength(MaterialConstraints.MaximumUnitLength)
            .WithMessage($"Material unit must not exceed {MaterialConstraints.MaximumUnitLength} characters.");
    }
}
