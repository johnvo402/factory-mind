using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Auth.Refresh;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<Result<AuthSession>>;
