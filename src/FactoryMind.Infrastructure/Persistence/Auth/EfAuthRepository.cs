using FactoryMind.Application.Features.Auth;
using FactoryMind.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace FactoryMind.Infrastructure.Persistence.Auth;

public sealed class EfAuthRepository(FactoryMindDbContext dbContext) : IAuthRepository
{
    public Task<User?> GetActiveUserByEmailAsync(string email, CancellationToken cancellationToken) =>
        dbContext.Users.SingleOrDefaultAsync(user => user.Email == email && user.IsActive, cancellationToken);

    public Task<RefreshToken?> GetRefreshTokenWithUserAsync(string tokenHash, CancellationToken cancellationToken) =>
        dbContext.RefreshTokens.Include(token => token.User)
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

    public Task<RefreshToken?> GetRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken) =>
        dbContext.RefreshTokens.SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

    public void AddRefreshToken(RefreshToken refreshToken) => dbContext.RefreshTokens.Add(refreshToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
