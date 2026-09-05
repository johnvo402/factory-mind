using FactoryMind.Application.Features.Routings;
using FluentValidation;

namespace FactoryMind.Api.Endpoints;

public sealed record RoutingOperationRequest(
    int Sequence,
    string Name,
    Guid WorkCenterId,
    int SetupTimeMinutes,
    int RunTimeMinutes,
    string? Description);

public sealed record RoutingRequest(IReadOnlyList<RoutingOperationRequest> Operations);

public sealed class RoutingOperationRequestValidator : AbstractValidator<RoutingOperationRequest> {
    public RoutingOperationRequestValidator() {
        RuleFor(request => request.Sequence).GreaterThan(0);
        RuleFor(request => request.Name).NotEmpty().MaximumLength(RoutingConstraints.MaximumOperationNameLength);
        RuleFor(request => request.WorkCenterId).NotEmpty();
        RuleFor(request => request.SetupTimeMinutes).GreaterThanOrEqualTo(0);
        RuleFor(request => request.RunTimeMinutes).GreaterThanOrEqualTo(0);
        RuleFor(request => request.Description).MaximumLength(RoutingConstraints.MaximumOperationDescriptionLength);
    }
}

public sealed class RoutingRequestValidator : AbstractValidator<RoutingRequest> {
    public RoutingRequestValidator() {
        RuleFor(request => request.Operations).NotNull();
        RuleForEach(request => request.Operations).SetValidator(new RoutingOperationRequestValidator());
    }
}
