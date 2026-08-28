using FactoryMind.Application.Features.Inventories;
using FactoryMind.Domain.Manufacturing;
using FluentValidation;

namespace FactoryMind.Api.Endpoints;

public sealed class InventoryBalanceQueryRequest {
    public Guid? WarehouseId { get; init; }
    public Guid? MaterialId { get; init; }
    public string? Search { get; init; }
}

public sealed class InventoryBalanceQueryRequestValidator : AbstractValidator<InventoryBalanceQueryRequest> {
    public InventoryBalanceQueryRequestValidator() {
        RuleFor(request => request.Search)
            .MaximumLength(InventoryConstraints.MaximumSearchLength);
    }
}

public sealed class InventoryTransactionQueryRequest {
    public Guid? WarehouseId { get; init; }
    public Guid? MaterialId { get; init; }
    public InventoryTransactionType? TransactionType { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public int? Page { get; init; }
    public int? PageSize { get; init; }
}

public sealed class InventoryTransactionQueryRequestValidator : AbstractValidator<InventoryTransactionQueryRequest> {
    public InventoryTransactionQueryRequestValidator() {
        RuleFor(request => request.Page!.Value)
            .GreaterThan(0)
            .When(request => request.Page.HasValue);
        RuleFor(request => request.PageSize!.Value)
            .InclusiveBetween(1, InventoryConstraints.MaximumPageSize)
            .When(request => request.PageSize.HasValue);
        RuleFor(request => request.To)
            .GreaterThanOrEqualTo(request => request.From!.Value)
            .When(request => request.From.HasValue && request.To.HasValue)
            .WithMessage("To must be greater than or equal to From.");
    }
}

public sealed record InventoryMovementRequest(
    Guid WarehouseId,
    Guid MaterialId,
    decimal Quantity,
    string? Note,
    string? ReferenceType,
    Guid? ReferenceId);

public sealed class InventoryMovementRequestValidator : AbstractValidator<InventoryMovementRequest> {
    public InventoryMovementRequestValidator() {
        RuleFor(request => request.WarehouseId).NotEmpty();
        RuleFor(request => request.MaterialId).NotEmpty();
        RuleFor(request => request.Quantity)
            .GreaterThan(0)
            .PrecisionScale(
                InventoryConstraints.QuantityPrecision,
                InventoryConstraints.QuantityScale,
                ignoreTrailingZeros: true);
        RuleFor(request => request.Note).MaximumLength(InventoryConstraints.MaximumNoteLength);
        RuleFor(request => request.ReferenceType)
            .MaximumLength(InventoryConstraints.MaximumReferenceTypeLength);
    }
}

public sealed record InventoryAdjustmentRequest(
    Guid WarehouseId,
    Guid MaterialId,
    InventoryAdjustmentDirection Direction,
    decimal Quantity,
    string Note,
    string? ReferenceType,
    Guid? ReferenceId);

public sealed class InventoryAdjustmentRequestValidator : AbstractValidator<InventoryAdjustmentRequest> {
    public InventoryAdjustmentRequestValidator() {
        RuleFor(request => request.WarehouseId).NotEmpty();
        RuleFor(request => request.MaterialId).NotEmpty();
        RuleFor(request => request.Direction).IsInEnum();
        RuleFor(request => request.Quantity)
            .GreaterThan(0)
            .PrecisionScale(
                InventoryConstraints.QuantityPrecision,
                InventoryConstraints.QuantityScale,
                ignoreTrailingZeros: true);
        RuleFor(request => request.Note)
            .NotEmpty()
            .MaximumLength(InventoryConstraints.MaximumNoteLength);
        RuleFor(request => request.ReferenceType)
            .MaximumLength(InventoryConstraints.MaximumReferenceTypeLength);
    }
}

public sealed record InventoryTransferRequest(
    Guid SourceWarehouseId,
    Guid DestinationWarehouseId,
    Guid MaterialId,
    decimal Quantity,
    string? Note,
    string? ReferenceType);

public sealed class InventoryTransferRequestValidator : AbstractValidator<InventoryTransferRequest> {
    public InventoryTransferRequestValidator() {
        RuleFor(request => request.SourceWarehouseId).NotEmpty();
        RuleFor(request => request.DestinationWarehouseId)
            .NotEmpty()
            .NotEqual(request => request.SourceWarehouseId)
            .WithMessage("Destination warehouse must differ from source warehouse.");
        RuleFor(request => request.MaterialId).NotEmpty();
        RuleFor(request => request.Quantity)
            .GreaterThan(0)
            .PrecisionScale(
                InventoryConstraints.QuantityPrecision,
                InventoryConstraints.QuantityScale,
                ignoreTrailingZeros: true);
        RuleFor(request => request.Note).MaximumLength(InventoryConstraints.MaximumNoteLength);
        RuleFor(request => request.ReferenceType)
            .MaximumLength(InventoryConstraints.MaximumReferenceTypeLength);
    }
}
