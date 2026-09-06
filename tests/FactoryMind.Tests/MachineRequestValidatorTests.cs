using FactoryMind.Api.Endpoints;
using FactoryMind.Domain.Manufacturing;
using FluentValidation.TestHelper;

namespace FactoryMind.Tests;

public sealed class MachineRequestValidatorTests {
    [Fact]
    public async Task Machine_request_requires_code_name_and_supported_status() {
        var validator = new MachineRequestValidator();

        var result = await validator.TestValidateAsync(new MachineRequest("", "", "unknown"));

        result.ShouldHaveValidationErrorFor(request => request.Code);
        result.ShouldHaveValidationErrorFor(request => request.Name);
        result.ShouldHaveValidationErrorFor(request => request.Status)
            .WithErrorMessage(
                "Machine status must be available, maintenance, or offline. Running is system-managed.");
    }

    [Fact]
    public async Task Machine_request_accepts_status_case_insensitively() {
        var validator = new MachineRequestValidator();

        var result = await validator.TestValidateAsync(
            new MachineRequest("M-001", "Injection molding", MachineStatuses.Available.ToUpperInvariant()));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Machine_request_rejects_manual_running_status() {
        var validator = new MachineRequestValidator();

        var result = await validator.TestValidateAsync(
            new MachineRequest("M-001", "Injection molding", MachineStatuses.Running));

        result.ShouldHaveValidationErrorFor(request => request.Status);
    }
}
