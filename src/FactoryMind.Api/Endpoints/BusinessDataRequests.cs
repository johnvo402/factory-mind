using FactoryMind.Application.Features.BusinessData;
using FluentValidation;

namespace FactoryMind.Api.Endpoints;

public sealed class BusinessDataSearchRequest {
    public string? Search { get; init; }
}

public sealed class BusinessDataSearchRequestValidator : AbstractValidator<BusinessDataSearchRequest> {
    public BusinessDataSearchRequestValidator() {
        RuleFor(request => request.Search)
            .MaximumLength(BusinessDataConstraints.MaximumSearchLength)
            .WithMessage($"Search must not exceed {BusinessDataConstraints.MaximumSearchLength} characters.");
    }
}
