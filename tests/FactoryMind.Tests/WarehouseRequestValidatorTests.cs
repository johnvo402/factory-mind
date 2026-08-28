using FactoryMind.Api.Endpoints;
using FluentValidation.TestHelper;

namespace FactoryMind.Tests;

public sealed class WarehouseRequestValidatorTests {
    [Fact]
    public async Task Warehouse_requires_code_and_name() {
        var validator = new WarehouseCreateRequestValidator();

        var result = await validator.TestValidateAsync(new WarehouseCreateRequest("", "", null));

        result.ShouldHaveValidationErrorFor(request => request.Code);
        result.ShouldHaveValidationErrorFor(request => request.Name);
    }
}
