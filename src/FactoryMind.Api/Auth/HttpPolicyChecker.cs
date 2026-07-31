using FactoryMind.Application.Common.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace FactoryMind.Api.Auth;

public sealed class HttpPolicyChecker(
    IHttpContextAccessor httpContextAccessor,
    IAuthorizationService authorizationService) : IPolicyChecker {
    public bool IsAuthenticated => httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public async Task<bool> IsAuthorizedAsync(string policy, CancellationToken cancellationToken) {
        var user = httpContextAccessor.HttpContext?.User;
        if (user is null) {
            return false;
        }

        var result = await authorizationService.AuthorizeAsync(user, policy);
        return result.Succeeded;
    }
}
