using FactoryMind.Api.Routing;
using FactoryMind.Application.Common.Authorization;
using FactoryMind.Application.Features.Inventories.AdjustInventory;
using FactoryMind.Application.Features.Inventories.GetInventoryBalances;
using FactoryMind.Application.Features.Inventories.GetInventoryTransactions;
using FactoryMind.Application.Features.Inventories.IssueInventory;
using FactoryMind.Application.Features.Inventories.ReceiveInventory;
using FactoryMind.Application.Features.Inventories.TransferInventory;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace FactoryMind.Api.Endpoints;

public static class InventoryEndpoints {
    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup(ApiRoutes.Inventories.Group)
            .RequireAuthorization(AuthorizationPolicies.Manager);

        group.MapGet(ApiRoutes.Inventories.Root, async (
            [AsParameters] InventoryBalanceQueryRequest request,
            ISender sender,
            CancellationToken cancellationToken) => (await sender.Send(
                new GetInventoryBalancesQuery(request.WarehouseId, request.MaterialId, request.Search),
                cancellationToken)).ToHttpResult())
            .WithRequestValidation<InventoryBalanceQueryRequest>();

        group.MapGet(ApiRoutes.Inventories.Transactions, async (
            [AsParameters] InventoryTransactionQueryRequest request,
            ISender sender,
            CancellationToken cancellationToken) => (await sender.Send(
                new GetInventoryTransactionsQuery(
                    request.WarehouseId,
                    request.MaterialId,
                    request.TransactionType,
                    request.From,
                    request.To,
                    request.Page ?? 1,
                    request.PageSize ?? 50),
                cancellationToken)).ToHttpResult())
            .WithRequestValidation<InventoryTransactionQueryRequest>();

        group.MapPost(ApiRoutes.Inventories.Receive, async (
            [FromBody] InventoryMovementRequest request,
            ISender sender,
            CancellationToken cancellationToken) => (await sender.Send(
                new ReceiveInventoryCommand(
                    request.WarehouseId,
                    request.MaterialId,
                    request.Quantity,
                    request.Note,
                    request.ReferenceType,
                    request.ReferenceId),
                cancellationToken)).ToHttpResult())
            .WithRequestValidation<InventoryMovementRequest>();

        group.MapPost(ApiRoutes.Inventories.Issue, async (
            [FromBody] InventoryMovementRequest request,
            ISender sender,
            CancellationToken cancellationToken) => (await sender.Send(
                new IssueInventoryCommand(
                    request.WarehouseId,
                    request.MaterialId,
                    request.Quantity,
                    request.Note,
                    request.ReferenceType,
                    request.ReferenceId),
                cancellationToken)).ToHttpResult())
            .WithRequestValidation<InventoryMovementRequest>();

        group.MapPost(ApiRoutes.Inventories.Adjust, async (
            [FromBody] InventoryAdjustmentRequest request,
            ISender sender,
            CancellationToken cancellationToken) => (await sender.Send(
                new AdjustInventoryCommand(
                    request.WarehouseId,
                    request.MaterialId,
                    request.Direction,
                    request.Quantity,
                    request.Note,
                    request.ReferenceType,
                    request.ReferenceId),
                cancellationToken)).ToHttpResult())
            .WithRequestValidation<InventoryAdjustmentRequest>();

        group.MapPost(ApiRoutes.Inventories.Transfer, async (
            [FromBody] InventoryTransferRequest request,
            ISender sender,
            CancellationToken cancellationToken) => (await sender.Send(
                new TransferInventoryCommand(
                    request.SourceWarehouseId,
                    request.DestinationWarehouseId,
                    request.MaterialId,
                    request.Quantity,
                    request.Note,
                    request.ReferenceType),
                cancellationToken)).ToHttpResult())
            .WithRequestValidation<InventoryTransferRequest>();

        return endpoints;
    }
}
