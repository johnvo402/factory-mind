using FactoryMind.Application.Common.Authorization;
using FactoryMind.Application.Features.AiActions;
using FactoryMind.Api.Routing;
using Mediator;

namespace FactoryMind.Api.Endpoints;

public static class AiActionEndpoints {
    public static IEndpointRouteBuilder MapAiActionEndpoints(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup(ApiRoutes.AiActions.Group)
            .RequireAuthorization(AuthorizationPolicies.Authenticated);

        group.MapGet(ApiRoutes.AiActions.ById, async (
            Guid proposalId,
            ISender sender,
            CancellationToken cancellationToken) =>
            (await sender.Send(new GetAiActionProposalQuery(proposalId), cancellationToken)).ToHttpResult());

        group.MapPost(ApiRoutes.AiActions.Confirm, async (
            Guid proposalId,
            ISender sender,
            CancellationToken cancellationToken) =>
            (await sender.Send(new ConfirmAiActionProposalCommand(proposalId), cancellationToken)).ToHttpResult());

        group.MapPost(ApiRoutes.AiActions.Cancel, async (
            Guid proposalId,
            ISender sender,
            CancellationToken cancellationToken) =>
            (await sender.Send(new CancelAiActionProposalCommand(proposalId), cancellationToken)).ToHttpResult());

        return endpoints;
    }
}
