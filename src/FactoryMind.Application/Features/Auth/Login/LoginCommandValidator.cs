using FluentValidation;

namespace FactoryMind.Application.Features.Auth.Login;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand> {
    public LoginCommandValidator() {
        RuleFor(command => command.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email is invalid.");

        RuleFor(command => command.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}
