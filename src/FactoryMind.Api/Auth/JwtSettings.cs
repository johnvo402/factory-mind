namespace FactoryMind.Api.Auth;

public sealed class JwtSettings
{
    public const string SectionName = "Jwt";
    public string Issuer { get; init; } = "FactoryMind";
    public string Audience { get; init; } = "FactoryMind.Web";
    public string Key { get; init; } = "development-only-change-this-key-before-deployment-2026";
    public int AccessTokenMinutes { get; init; } = 30;
    public int RefreshTokenDays { get; init; } = 14;
}
