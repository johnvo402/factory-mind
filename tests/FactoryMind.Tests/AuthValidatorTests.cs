using FactoryMind.Application.Features.Auth.Login;
using FluentValidation.TestHelper;

namespace FactoryMind.Tests;

public sealed class AuthValidatorTests {
    [Fact]
    public async Task Login_requires_a_valid_email_and_password() {
        var validator = new LoginCommandValidator();

        var result = await validator.TestValidateAsync(new LoginCommand("not-an-email", ""));

        result.ShouldHaveValidationErrorFor(command => command.Email)
            .WithErrorMessage("Email is invalid.");
        result.ShouldHaveValidationErrorFor(command => command.Password)
            .WithErrorMessage("Password is required.");
    }
}
