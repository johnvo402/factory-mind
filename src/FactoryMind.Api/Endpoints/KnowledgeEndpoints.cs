using FactoryMind.Api.Routing;
using FactoryMind.Application.Common.Authorization;
using FactoryMind.Application.Features.Knowledge.SearchKnowledge;
using Mediator;

namespace FactoryMind.Api.Endpoints;

public static class KnowledgeEndpoints {
    public static IEndpointRouteBuilder MapKnowledgeEndpoints(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup(ApiRoutes.Knowledge.Group)
            .RequireAuthorization(AuthorizationPolicies.Authenticated);

        group.MapPost(ApiRoutes.Knowledge.Search, async (
            SearchKnowledgeRequest request,
            ISender sender,
            CancellationToken cancellationToken) => {
            var query = new SearchKnowledgeQuery(request.Query, request.Limit);
            return (await sender.Send(query, cancellationToken)).ToHttpResult();
        }).WithRequestValidation<SearchKnowledgeRequest>();

        return endpoints;
    }
}
