using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Auth.Login;

public sealed record LoginCommand(string Email, string Password) : IRequest<Result<AuthSession>>;
