using FluentValidation;

namespace FactoryMind.Application.Features.Auth.Logout;

public sealed class LogoutCommandValidator : AbstractValidator<LogoutCommand> {
    public LogoutCommandValidator() {
        RuleFor(command => command.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required.");
    }
}
