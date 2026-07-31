using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Auth.Logout;

public sealed record LogoutCommand(string RefreshToken) : IRequest<Result>;
