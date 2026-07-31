using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FactoryMind.Application.Features.Auth;
using FactoryMind.Domain.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FactoryMind.Api.Auth;

public sealed class JwtTokenService(IOptions<JwtSettings> options) : ITokenService {
    private readonly JwtSettings _settings = options.Value;

    public string CreateAccessToken(User user) {
        Claim[] claims = [
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role),
            new("companyId", user.CompanyId.ToString())
        ];
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            _settings.Issuer,
            _settings.Audience,
            claims,
            expires: DateTime.UtcNow.AddMinutes(_settings.AccessTokenMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string CreateRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public DateTime GetRefreshTokenExpiry() => DateTime.UtcNow.AddDays(_settings.RefreshTokenDays);
}
