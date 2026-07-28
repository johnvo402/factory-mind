using System.Security.Cryptography;
using FactoryMind.Application.Auth;
using FactoryMind.Domain.Identity;
using Microsoft.EntityFrameworkCore;
namespace FactoryMind.Infrastructure.Persistence;
public sealed class AuthService(FactoryMindDbContext dbContext, IJwtTokenService tokenService) : IAuthService
{
    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Email == request.Email.Trim().ToLowerInvariant() && x.IsActive, cancellationToken);
        if (user is null || !PasswordHash.Verify(request.Password, user.PasswordHash)) return null;
        var refreshToken = tokenService.CreateRefreshToken();
        var expiresAt = tokenService.GetRefreshTokenExpiry();
        dbContext.RefreshTokens.Add(new RefreshToken { UserId = user.Id, TokenHash = PasswordHash.HashToken(refreshToken), ExpiresAt = expiresAt });
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(tokenService.CreateAccessToken(user), refreshToken, expiresAt, new(user.Id, user.Name, user.Email, user.Role, user.CompanyId));
    }
}
internal static class PasswordHash
{
    public static bool Verify(string password, string encoded) { var parts = encoded.Split(':'); return parts.Length == 3 && CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(parts[2]), Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(parts[1]), 210_000, HashAlgorithmName.SHA512, 32)); }
    public static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
}
