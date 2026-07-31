using FactoryMind.Api.Routing;

namespace FactoryMind.Api.Auth;

public sealed class RefreshTokenCookie(IWebHostEnvironment environment) {
    public const string Name = "factorymind.refresh_token";

    public string? Read(HttpRequest request) => request.Cookies[Name];

    public void Write(HttpResponse response, string refreshToken, DateTime expiresAt) {
        response.Cookies.Append(Name, refreshToken, CreateOptions(expiresAt));
    }

    public void Delete(HttpResponse response) {
        response.Cookies.Delete(Name, CreateOptions());
    }

    private CookieOptions CreateOptions(DateTime? expiresAt = null) => new() {
        HttpOnly = true,
        Secure = !environment.IsDevelopment(),
        SameSite = SameSiteMode.Strict,
        Path = ApiRoutes.Auth.Group,
        Expires = expiresAt is null ? null : new DateTimeOffset(expiresAt.Value),
        IsEssential = true
    };
}
