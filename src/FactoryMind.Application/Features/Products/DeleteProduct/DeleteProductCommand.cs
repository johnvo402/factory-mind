using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Products.DeleteProduct;

public sealed record DeleteProductCommand(Guid ProductId)
    : IRequest<Result>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
