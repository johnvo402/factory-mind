using FactoryMind.Api.Endpoints;
using FluentValidation.TestHelper;

namespace FactoryMind.Tests;

public sealed class BusinessDataRequestValidatorTests {
    [Fact]
    public async Task Material_request_requires_code_name_and_unit() {
        var validator = new MaterialRequestValidator();

        var result = await validator.TestValidateAsync(new MaterialRequest("", "", ""));

        result.ShouldHaveValidationErrorFor(request => request.Code);
        result.ShouldHaveValidationErrorFor(request => request.Name);
        result.ShouldHaveValidationErrorFor(request => request.Unit);
    }

    [Fact]
    public async Task Product_request_requires_code_and_name() {
        var validator = new ProductRequestValidator();

        var result = await validator.TestValidateAsync(new ProductRequest("", ""));

        result.ShouldHaveValidationErrorFor(request => request.Code);
        result.ShouldHaveValidationErrorFor(request => request.Name);
    }
}
