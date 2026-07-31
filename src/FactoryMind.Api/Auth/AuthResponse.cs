using FactoryMind.Application.Features.Auth;

namespace FactoryMind.Api.Auth;

public sealed record AuthResponse(
    string AccessToken,
    UserProfile User);
