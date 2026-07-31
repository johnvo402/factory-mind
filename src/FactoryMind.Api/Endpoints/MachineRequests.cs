using FactoryMind.Application.Features.Machines;
using FactoryMind.Domain.Manufacturing;
using FluentValidation;

namespace FactoryMind.Api.Endpoints;

public sealed record MachineRequest(string Code, string Name, string Status);

public sealed class MachineRequestValidator : AbstractValidator<MachineRequest> {
    public MachineRequestValidator() {
        RuleFor(request => request.Code)
            .NotEmpty().WithMessage("Machine code is required.")
            .MaximumLength(MachineConstraints.MaximumCodeLength)
            .WithMessage($"Machine code must not exceed {MachineConstraints.MaximumCodeLength} characters.");
        RuleFor(request => request.Name)
            .NotEmpty().WithMessage("Machine name is required.")
            .MaximumLength(MachineConstraints.MaximumNameLength)
            .WithMessage($"Machine name must not exceed {MachineConstraints.MaximumNameLength} characters.");
        RuleFor(request => request.Status)
            .NotEmpty().WithMessage("Machine status is required.")
            .Must(status => status is not null && MachineStatuses.All.Contains(status.Trim()))
            .WithMessage("Machine status must be available, running, maintenance, or offline.");
    }
}
