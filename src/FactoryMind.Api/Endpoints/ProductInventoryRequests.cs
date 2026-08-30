using FactoryMind.Application.Features.ProductInventories;
using FactoryMind.Domain.Manufacturing;
using FluentValidation;

namespace FactoryMind.Api.Endpoints;

public sealed class ProductInventoryBalanceQueryRequest {
    public Guid? WarehouseId { get; init; }
    public Guid? ProductId { get; init; }
    public string? Search { get; init; }
}

public sealed class ProductInventoryBalanceQueryRequestValidator
    : AbstractValidator<ProductInventoryBalanceQueryRequest> {
    public ProductInventoryBalanceQueryRequestValidator() {
        RuleFor(request => request.Search)
            .MaximumLength(ProductInventoryConstraints.MaximumSearchLength);
    }
}

public sealed class ProductInventoryTransactionQueryRequest {
    public Guid? WarehouseId { get; init; }
    public Guid? ProductId { get; init; }
    public ProductInventoryTransactionType? TransactionType { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public int? Page { get; init; }
    public int? PageSize { get; init; }
}

public sealed class ProductInventoryTransactionQueryRequestValidator
    : AbstractValidator<ProductInventoryTransactionQueryRequest> {
    public ProductInventoryTransactionQueryRequestValidator() {
        RuleFor(request => request.Page!.Value)
            .GreaterThan(0)
            .When(request => request.Page.HasValue);
        RuleFor(request => request.PageSize!.Value)
            .InclusiveBetween(1, ProductInventoryConstraints.MaximumPageSize)
            .When(request => request.PageSize.HasValue);
        RuleFor(request => request.To)
            .GreaterThanOrEqualTo(request => request.From!.Value)
            .When(request => request.From.HasValue && request.To.HasValue)
            .WithMessage("To must be greater than or equal to From.");
    }
}
