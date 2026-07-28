using FactoryMind.Domain.Identity;
namespace FactoryMind.Application.Auth;
public sealed record LoginRequest(string Email, string Password);
public sealed record LoginResponse(string AccessToken, string RefreshToken, DateTime RefreshTokenExpiresAt, UserProfile User);
public sealed record UserProfile(Guid Id, string Name, string Email, string Role, Guid CompanyId);
public interface IAuthService { Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken); }
public interface IJwtTokenService { string CreateAccessToken(User user); string CreateRefreshToken(); DateTime GetRefreshTokenExpiry(); }
