using FactoryMind.Application.Features.Boms;
using FactoryMind.Application.Features.ProductionOrders;
using FactoryMind.Domain.Manufacturing;
using FluentValidation;

namespace FactoryMind.Api.Endpoints;

public sealed record SchedulePreviewRequest(
    int? HorizonDays,
    string? Priority,
    Guid? OrderId,
    Guid? WorkCenterId);

public sealed class SchedulePreviewRequestValidator : AbstractValidator<SchedulePreviewRequest> {
    public SchedulePreviewRequestValidator() {
        RuleFor(request => request.HorizonDays)
            .InclusiveBetween(1, 180).When(request => request.HorizonDays.HasValue);
        RuleFor(request => request.Priority)
            .Must(priority => priority is null || ProductionOrderPriorities.All.Contains(priority))
            .WithMessage("Priority is invalid.");
    }
}

public sealed record ProductionOrderRequest(
    string Number,
    Guid ProductId,
    decimal Quantity,
    DateTime? DueDate = null,
    string Priority = ProductionOrderPriorities.Normal);

public sealed class ProductionOrderQueryRequest {
    public string? Search { get; init; }
    public string? Status { get; init; }
    public string? Priority { get; init; }
    public string? DeliveryStatus { get; init; }
    public DateTime? DueFrom { get; init; }
    public DateTime? DueTo { get; init; }
    public Guid? ProductId { get; init; }
    public int? Page { get; init; }
    public int? PageSize { get; init; }
    public string? SortBy { get; init; }
    public string? SortDirection { get; init; }
}

public sealed record ProductionMaterialAllocationRequest(
    Guid MaterialId,
    Guid WarehouseId,
    decimal Quantity);

public sealed record StartProductionOrderRequest(
    IReadOnlyList<ProductionMaterialAllocationRequest> Allocations);

public sealed record CompleteProductionOrderRequest(Guid WarehouseId);

public sealed record StartProductionOrderOperationRequest(Guid MachineId);

public sealed class ProductionOrderRequestValidator : AbstractValidator<ProductionOrderRequest> {
    public ProductionOrderRequestValidator() {
        RuleFor(request => request.Number)
            .NotEmpty().WithMessage("Production order number is required.")
            .MaximumLength(ProductionOrderConstraints.MaximumNumberLength)
            .WithMessage(
                $"Production order number must not exceed {ProductionOrderConstraints.MaximumNumberLength} characters.");
        RuleFor(request => request.ProductId)
            .NotEmpty().WithMessage("Product is required.");
        RuleFor(request => request.Quantity)
            .GreaterThan(0).WithMessage("Quantity must be greater than zero.")
            .PrecisionScale(
                ProductionOrderConstraints.QuantityPrecision,
                ProductionOrderConstraints.QuantityScale,
                ignoreTrailingZeros: true)
            .WithMessage(
                $"Quantity must have at most {ProductionOrderConstraints.QuantityPrecision} digits and " +
                $"{ProductionOrderConstraints.QuantityScale} decimal places.");
        RuleFor(request => request.Priority)
            .Must(priority => priority is not null && ProductionOrderPriorities.All.Contains(priority))
            .WithMessage("Priority must be low, normal, high, or urgent.");
    }
}

public sealed class ProductionOrderQueryRequestValidator : AbstractValidator<ProductionOrderQueryRequest> {
    public ProductionOrderQueryRequestValidator() {
        RuleFor(request => request.Search)
            .MaximumLength(200).WithMessage("Search must not exceed 200 characters.");
        RuleFor(request => request.Status)
            .Must(status => status is null || ProductionOrderStatuses.All.Contains(status))
            .WithMessage("Status is invalid.");
        RuleFor(request => request.Priority)
            .Must(priority => priority is null || ProductionOrderPriorities.All.Contains(priority))
            .WithMessage("Priority is invalid.");
        RuleFor(request => request.DeliveryStatus)
            .Must(status => status is null || ProductionOrderDeliveryStatuses.All.Contains(status))
            .WithMessage("Delivery status is invalid.");
        RuleFor(request => request.Page)
            .GreaterThanOrEqualTo(1).When(request => request.Page.HasValue)
            .WithMessage("Page must be at least 1.");
        RuleFor(request => request.PageSize)
            .InclusiveBetween(1, ProductionOrderConstraints.MaximumPageSize)
            .When(request => request.PageSize.HasValue)
            .WithMessage($"Page size must be between 1 and {ProductionOrderConstraints.MaximumPageSize}.");
        RuleFor(request => request.SortBy)
            .Must(value => value is null || ProductionOrderSortFields.All.Contains(value))
            .WithMessage("Sort field is invalid.");
        RuleFor(request => request.SortDirection)
            .Must(value => value is null || ProductionOrderSortDirections.All.Contains(value))
            .WithMessage("Sort direction is invalid.");
        RuleFor(request => request)
            .Must(request => !request.DueFrom.HasValue || !request.DueTo.HasValue || request.DueFrom <= request.DueTo)
            .WithMessage("Due from must be before or equal to due to.");
    }
}

public sealed class ProductionMaterialAllocationRequestValidator
    : AbstractValidator<ProductionMaterialAllocationRequest> {
    public ProductionMaterialAllocationRequestValidator() {
        RuleFor(request => request.MaterialId)
            .NotEmpty().WithMessage("Material is required.");
        RuleFor(request => request.WarehouseId)
            .NotEmpty().WithMessage("Warehouse is required.");
        RuleFor(request => request.Quantity)
            .GreaterThan(0).WithMessage("Allocation quantity must be greater than zero.")
            .PrecisionScale(
                BomConstraints.QuantityPrecision,
                BomConstraints.QuantityScale,
                ignoreTrailingZeros: true)
            .WithMessage(
                $"Allocation quantity must have at most {BomConstraints.QuantityPrecision} digits and " +
                $"{BomConstraints.QuantityScale} decimal places.");
    }
}

public sealed class StartProductionOrderRequestValidator : AbstractValidator<StartProductionOrderRequest> {
    public StartProductionOrderRequestValidator() {
        RuleFor(request => request.Allocations)
            .NotNull().WithMessage("Material allocations are required.")
            .NotEmpty().WithMessage("At least one material allocation is required.");
        RuleForEach(request => request.Allocations)
            .SetValidator(new ProductionMaterialAllocationRequestValidator());
    }
}

public sealed class CompleteProductionOrderRequestValidator : AbstractValidator<CompleteProductionOrderRequest> {
    public CompleteProductionOrderRequestValidator() {
        RuleFor(request => request.WarehouseId)
            .NotEmpty().WithMessage("Destination warehouse is required.");
    }
}

public sealed class StartProductionOrderOperationRequestValidator
    : AbstractValidator<StartProductionOrderOperationRequest> {
    public StartProductionOrderOperationRequestValidator() {
        RuleFor(request => request.MachineId)
            .NotEmpty().WithMessage("Machine is required.");
    }
}
