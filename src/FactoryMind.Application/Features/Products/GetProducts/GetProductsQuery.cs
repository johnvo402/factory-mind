using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Products.GetProducts;

public sealed record GetProductsQuery(string? Search)
    : IRequest<Result<IReadOnlyList<ProductResponse>>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
