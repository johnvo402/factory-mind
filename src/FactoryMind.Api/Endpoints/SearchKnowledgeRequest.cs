using FactoryMind.Application.Features.Knowledge;
using FluentValidation;

namespace FactoryMind.Api.Endpoints;

public sealed class SearchKnowledgeRequest {
    public string Query { get; init; } = string.Empty;
    public int Limit { get; init; } = KnowledgeSearchConstraints.DefaultLimit;
}

public sealed class SearchKnowledgeRequestValidator : AbstractValidator<SearchKnowledgeRequest> {
    public SearchKnowledgeRequestValidator() {
        RuleFor(request => request.Query)
            .NotEmpty().WithMessage("Search query is required.")
            .MaximumLength(KnowledgeSearchConstraints.MaximumQueryLength)
            .WithMessage("Search query must not exceed 2000 characters.");
        RuleFor(request => request.Limit)
            .InclusiveBetween(1, KnowledgeSearchConstraints.MaximumLimit)
            .WithMessage("Search limit must be between 1 and 20.");
    }
}
