using FactoryMind.Application.Features.Warehouses;
using FluentValidation;

namespace FactoryMind.Api.Endpoints;

public sealed record WarehouseCreateRequest(string Code, string Name, string? Description);

public sealed record WarehouseUpdateRequest(
    string Code,
    string Name,
    string? Description,
    bool IsActive);

public sealed class WarehouseCreateRequestValidator : AbstractValidator<WarehouseCreateRequest> {
    public WarehouseCreateRequestValidator() {
        RuleFor(request => request.Code)
            .NotEmpty()
            .MaximumLength(WarehouseConstraints.MaximumCodeLength);
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(WarehouseConstraints.MaximumNameLength);
        RuleFor(request => request.Description)
            .MaximumLength(WarehouseConstraints.MaximumDescriptionLength);
    }
}

public sealed class WarehouseUpdateRequestValidator : AbstractValidator<WarehouseUpdateRequest> {
    public WarehouseUpdateRequestValidator() {
        RuleFor(request => request.Code)
            .NotEmpty()
            .MaximumLength(WarehouseConstraints.MaximumCodeLength);
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(WarehouseConstraints.MaximumNameLength);
        RuleFor(request => request.Description)
            .MaximumLength(WarehouseConstraints.MaximumDescriptionLength);
    }
}
