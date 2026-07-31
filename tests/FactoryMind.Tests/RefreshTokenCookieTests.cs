using FactoryMind.Api.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace FactoryMind.Tests;

public sealed class RefreshTokenCookieTests {
    [Fact]
    public void Development_cookie_is_http_only_strict_and_scoped_to_auth_routes() {
        var context = new DefaultHttpContext();
        var cookie = new RefreshTokenCookie(new FakeWebHostEnvironment(Environments.Development));

        cookie.Write(context.Response, "secret-token", DateTime.UtcNow.AddDays(14));

        var header = context.Response.Headers.SetCookie.ToString();
        Assert.Contains("factorymind.refresh_token=secret-token", header);
        Assert.Contains("path=/api/auth", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", header, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secure", header, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_cookie_is_secure_and_can_be_read_from_the_request() {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = "factorymind.refresh_token=rotating-token";
        var cookie = new RefreshTokenCookie(new FakeWebHostEnvironment(Environments.Production));

        cookie.Write(context.Response, "new-token", DateTime.UtcNow.AddDays(14));

        Assert.Equal("rotating-token", cookie.Read(context.Request));
        Assert.Contains("secure", context.Response.Headers.SetCookie.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeWebHostEnvironment(string environmentName) : IWebHostEnvironment {
        public string ApplicationName { get; set; } = "FactoryMind.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = environmentName;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
