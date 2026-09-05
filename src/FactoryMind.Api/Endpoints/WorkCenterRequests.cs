using FactoryMind.Application.Features.WorkCenters;
using FluentValidation;

namespace FactoryMind.Api.Endpoints;

public sealed record WorkCenterCreateRequest(string Code, string Name, string? Description);
public sealed record WorkCenterUpdateRequest(string Code, string Name, string? Description);

public sealed class WorkCenterCreateRequestValidator : AbstractValidator<WorkCenterCreateRequest> {
    public WorkCenterCreateRequestValidator() {
        RuleFor(request => request.Code).NotEmpty().MaximumLength(WorkCenterConstraints.MaximumCodeLength);
        RuleFor(request => request.Name).NotEmpty().MaximumLength(WorkCenterConstraints.MaximumNameLength);
        RuleFor(request => request.Description).MaximumLength(WorkCenterConstraints.MaximumDescriptionLength);
    }
}

public sealed class WorkCenterUpdateRequestValidator : AbstractValidator<WorkCenterUpdateRequest> {
    public WorkCenterUpdateRequestValidator() {
        RuleFor(request => request.Code).NotEmpty().MaximumLength(WorkCenterConstraints.MaximumCodeLength);
        RuleFor(request => request.Name).NotEmpty().MaximumLength(WorkCenterConstraints.MaximumNameLength);
        RuleFor(request => request.Description).MaximumLength(WorkCenterConstraints.MaximumDescriptionLength);
    }
}
