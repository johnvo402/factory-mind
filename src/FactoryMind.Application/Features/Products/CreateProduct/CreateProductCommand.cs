using FactoryMind.Application.Common.Authorization;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Products.CreateProduct;

public sealed record CreateProductCommand(string Code, string Name)
    : IRequest<Result<ProductResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}
