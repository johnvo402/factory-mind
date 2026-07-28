using FactoryMind.Application.Auth;
using Microsoft.AspNetCore.Mvc;

namespace FactoryMind.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, cancellationToken);
        return result is null
            ? Unauthorized(ApiResponse<LoginResponse>.Failure("Email or password is incorrect."))
            : Ok(ApiResponse<LoginResponse>.Ok(result));
    }
}

public sealed record ApiResponse<T>(bool Success, string Message, T? Data)
{
    public static ApiResponse<T> Ok(T data) => new(true, "OK", data);
    public static ApiResponse<T> Failure(string message) => new(false, message, default);
}
