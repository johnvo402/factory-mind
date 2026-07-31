using FactoryMind.Application.Common.Authorization;
using Mediator;

namespace FactoryMind.Application.Common.Behaviors;

public sealed class AuthorizationBehavior<TMessage, TResponse>(IPolicyChecker policyChecker)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : notnull, IMessage {
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken) {
        if (message is not IAuthorizedRequest authorizedRequest) {
            return await next(message, cancellationToken);
        }

        if (!policyChecker.IsAuthenticated) {
            throw new AuthenticationRequiredException();
        }

        if (!await policyChecker.IsAuthorizedAsync(authorizedRequest.Policy, cancellationToken)) {
            throw new ForbiddenAccessException();
        }

        return await next(message, cancellationToken);
    }
}
