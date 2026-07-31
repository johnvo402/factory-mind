using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Products.UpdateProduct;

public sealed record UpdateProductCommand(Guid ProductId, string Code, string Name)
    : IRequest<Result<ProductResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
