using FactoryMind.Api.Routing;
using FactoryMind.Application.Common.Authorization;
using FactoryMind.Application.Features.ProductInventories.GetProductInventoryBalances;
using FactoryMind.Application.Features.ProductInventories.GetProductInventoryTransactions;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace FactoryMind.Api.Endpoints;

public static class ProductInventoryEndpoints {
    public static IEndpointRouteBuilder MapProductInventoryEndpoints(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup(ApiRoutes.ProductInventories.Group)
            .RequireAuthorization(AuthorizationPolicies.Manager);

        group.MapGet(ApiRoutes.ProductInventories.Root, async (
            [AsParameters] ProductInventoryBalanceQueryRequest request,
            ISender sender,
            CancellationToken cancellationToken) => (await sender.Send(
                new GetProductInventoryBalancesQuery(
                    request.WarehouseId,
                    request.ProductId,
                    request.Search),
                cancellationToken)).ToHttpResult())
            .WithRequestValidation<ProductInventoryBalanceQueryRequest>();

        group.MapGet(ApiRoutes.ProductInventories.Transactions, async (
            [AsParameters] ProductInventoryTransactionQueryRequest request,
            ISender sender,
            CancellationToken cancellationToken) => (await sender.Send(
                new GetProductInventoryTransactionsQuery(
                    request.WarehouseId,
                    request.ProductId,
                    request.TransactionType,
                    request.From,
                    request.To,
                    request.Page ?? 1,
                    request.PageSize ?? 50),
                cancellationToken)).ToHttpResult())
            .WithRequestValidation<ProductInventoryTransactionQueryRequest>();

        return endpoints;
    }
}
