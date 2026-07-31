using System.Security.Claims;
using FactoryMind.Application.Common.Authorization;
using FactoryMind.Application.Common.Identity;

namespace FactoryMind.Api.Auth;

public sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser {
    private ClaimsPrincipal Principal {
        get {
            var principal = httpContextAccessor.HttpContext?.User;
            return principal?.Identity?.IsAuthenticated == true
                ? principal
                : throw new AuthenticationRequiredException();
        }
    }

    public Guid UserId => ReadGuidClaim(ClaimTypes.NameIdentifier, "sub");

    public Guid CompanyId => ReadGuidClaim("companyId");

    public string Role => Principal.FindFirstValue(ClaimTypes.Role)
        ?? throw new AuthenticationRequiredException();

    private Guid ReadGuidClaim(params string[] claimTypes) {
        foreach (var claimType in claimTypes) {
            if (Guid.TryParse(Principal.FindFirstValue(claimType), out var value)) {
                return value;
            }
        }

        throw new AuthenticationRequiredException();
    }
}
