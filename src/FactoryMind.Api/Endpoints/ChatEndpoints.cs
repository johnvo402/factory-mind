using FactoryMind.Application.Common.Authorization;
using FactoryMind.Application.Features.Chat.CreateConversation;
using FactoryMind.Application.Features.Chat.GetConversations;
using FactoryMind.Application.Features.Chat.GetMessages;
using FactoryMind.Application.Features.Chat.SendMessage;
using FactoryMind.Api.Routing;
using Mediator;

namespace FactoryMind.Api.Endpoints;

public static class ChatEndpoints {
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup(ApiRoutes.Conversations.Group)
            .RequireAuthorization(AuthorizationPolicies.Authenticated);

        group.MapPost(ApiRoutes.Conversations.Root, async (
            CreateConversationCommand command,
            ISender sender,
            CancellationToken cancellationToken) => {
                return (await sender.Send(command, cancellationToken)).ToHttpResult();
            }).WithRequestValidation<CreateConversationCommand>();

        group.MapGet(ApiRoutes.Conversations.Root, async (ISender sender, CancellationToken cancellationToken) => {
            return (await sender.Send(new GetConversationsQuery(), cancellationToken)).ToHttpResult();
        });

        group.MapGet(ApiRoutes.Conversations.Messages, async (
            Guid conversationId,
            ISender sender,
            CancellationToken cancellationToken) => {
                var query = new GetMessagesQuery(conversationId);
                return (await sender.Send(query, cancellationToken)).ToHttpResult();
            });

        group.MapPost(ApiRoutes.Conversations.StreamMessage, async (
            Guid conversationId,
            SendMessageRequest request,
            ISender sender,
            ChatSseWriter streamWriter,
            HttpContext httpContext,
            CancellationToken cancellationToken) => {
                var command = new SendMessageCommand(conversationId, request.Content);
                var result = await sender.Send(command, cancellationToken);

                if (result.IsFailure) {
                    await result.ToHttpResult().ExecuteAsync(httpContext);
                    return;
                }

                await streamWriter.WriteAsync(httpContext, result.Value!, cancellationToken);
            }).WithRequestValidation<SendMessageRequest>();

        return endpoints;
    }
}
