using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.ProductionOrders.GetProductionOrders;

public sealed record GetProductionOrdersQuery(
    string? Search,
    string? Status,
    string? Priority,
    string? DeliveryStatus,
    DateTime? DueFrom,
    DateTime? DueTo,
    Guid? ProductId,
    int Page,
    int PageSize,
    string SortBy,
    string SortDirection,
    bool PlanningOrder = false)
    : IRequest<Result<ProductionOrderPageResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
