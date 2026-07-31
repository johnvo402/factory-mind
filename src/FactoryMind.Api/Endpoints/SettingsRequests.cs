using FactoryMind.Application.Features.Settings;
using FactoryMind.Domain.Identity;
using FluentValidation;

namespace FactoryMind.Api.Endpoints;

public sealed record UpdateCompanySettingsRequest(string Name);

public sealed class UpdateCompanySettingsRequestValidator : AbstractValidator<UpdateCompanySettingsRequest> {
    public UpdateCompanySettingsRequestValidator() {
        RuleFor(request => request.Name)
            .NotEmpty().WithMessage("Company name is required.")
            .MaximumLength(SettingsConstraints.MaximumCompanyNameLength)
            .WithMessage($"Company name must not exceed {SettingsConstraints.MaximumCompanyNameLength} characters.");
    }
}

public sealed record CreateUserRequest(string Name, string Email, string Password, string Role);

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest> {
    public CreateUserRequestValidator() {
        AddCommonRules(this);
        RuleFor(request => request.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(SettingsConstraints.MinimumPasswordLength)
            .WithMessage($"Password must contain at least {SettingsConstraints.MinimumPasswordLength} characters.");
    }

    private static void AddCommonRules(AbstractValidator<CreateUserRequest> validator) {
        validator.RuleFor(request => request.Name)
            .NotEmpty().WithMessage("User name is required.")
            .MaximumLength(SettingsConstraints.MaximumUserNameLength)
            .WithMessage($"User name must not exceed {SettingsConstraints.MaximumUserNameLength} characters.");
        validator.RuleFor(request => request.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email is invalid.")
            .MaximumLength(SettingsConstraints.MaximumEmailLength)
            .WithMessage($"Email must not exceed {SettingsConstraints.MaximumEmailLength} characters.");
        validator.RuleFor(request => request.Role)
            .Must(role => role is not null && UserRoles.All.Contains(role.Trim()))
            .WithMessage("Role must be Admin, Manager, or User.");
    }
}

public sealed record UpdateUserRequest(string Name, string Email, string Role, bool IsActive);

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest> {
    public UpdateUserRequestValidator() {
        RuleFor(request => request.Name)
            .NotEmpty().WithMessage("User name is required.")
            .MaximumLength(SettingsConstraints.MaximumUserNameLength)
            .WithMessage($"User name must not exceed {SettingsConstraints.MaximumUserNameLength} characters.");
        RuleFor(request => request.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email is invalid.")
            .MaximumLength(SettingsConstraints.MaximumEmailLength)
            .WithMessage($"Email must not exceed {SettingsConstraints.MaximumEmailLength} characters.");
        RuleFor(request => request.Role)
            .Must(role => role is not null && UserRoles.All.Contains(role.Trim()))
            .WithMessage("Role must be Admin, Manager, or User.");
    }
}
