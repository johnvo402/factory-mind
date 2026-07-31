using FactoryMind.Application.Features.Auth;
using FactoryMind.Application.Features.Auth.Login;
using FactoryMind.Application.Features.Auth.Refresh;
using FactoryMind.Domain.Identity;

namespace FactoryMind.Tests;

public sealed class AuthCommandHandlerTests {
    [Fact]
    public async Task Login_returns_a_session_and_persists_a_hashed_refresh_token() {
        var user = CreateActiveUser();
        var repository = new FakeAuthRepository { ActiveUser = user };
        var handler = CreateLoginHandler(repository);

        var result = await handler.Handle(new LoginCommand(" ADMIN@FACTORYMIND.LOCAL ", "Demo@123"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("access-token", result.Value.AccessToken);
        Assert.Equal("refresh-token", result.Value.RefreshToken);
        Assert.Equal("admin@factorymind.local", repository.RequestedEmail);
        var savedToken = Assert.Single(repository.AddedRefreshTokens);
        Assert.Equal("hash:refresh-token", savedToken.TokenHash);
        Assert.Equal(user.Id, savedToken.UserId);
        Assert.Equal(1, repository.SaveChangesCount);
    }

    [Fact]
    public async Task Login_with_invalid_password_returns_an_invalid_credentials_result() {
        var repository = new FakeAuthRepository { ActiveUser = CreateActiveUser() };
        var handler = new LoginCommandHandler(repository, new FakeCredentialHasher { PasswordIsValid = false }, CreateSessionIssuer(repository));

        var result = await handler.Handle(new LoginCommand("admin@factorymind.local", "wrong-password"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("auth.invalid_credentials", result.Error?.Code);
        Assert.Empty(repository.AddedRefreshTokens);
    }

    [Fact]
    public async Task Refresh_rotates_an_active_refresh_token() {
        var user = CreateActiveUser();
        var currentToken = new RefreshToken { User = user, UserId = user.Id, TokenHash = "hash:refresh-token", ExpiresAt = DateTime.UtcNow.AddDays(1) };
        var repository = new FakeAuthRepository { RefreshTokenWithUser = currentToken };
        var handler = new RefreshTokenCommandHandler(repository, new FakeCredentialHasher(), CreateSessionIssuer(repository));

        var result = await handler.Handle(new RefreshTokenCommand("refresh-token"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(currentToken.RevokedAt);
        Assert.Single(repository.AddedRefreshTokens);
        Assert.Equal(1, repository.SaveChangesCount);
    }

    private static LoginCommandHandler CreateLoginHandler(FakeAuthRepository repository) =>
        new(repository, new FakeCredentialHasher(), CreateSessionIssuer(repository));

    private static AuthSessionIssuer CreateSessionIssuer(FakeAuthRepository repository) =>
        new(repository, new FakeCredentialHasher(), new FakeTokenService());

    private static User CreateActiveUser() =>
        new() { Name = "Admin", Email = "admin@factorymind.local", Role = "Admin", CompanyId = Guid.NewGuid() };

    private sealed class FakeAuthRepository : IAuthRepository {
        public User? ActiveUser { get; init; }
        public RefreshToken? RefreshTokenWithUser { get; init; }
        public string? RequestedEmail { get; private set; }
        public List<RefreshToken> AddedRefreshTokens { get; } = [];
        public int SaveChangesCount { get; private set; }

        public Task<User?> GetActiveUserByEmailAsync(string email, CancellationToken cancellationToken) {
            RequestedEmail = email;
            return Task.FromResult(ActiveUser);
        }

        public Task<RefreshToken?> GetRefreshTokenWithUserAsync(string tokenHash, CancellationToken cancellationToken) =>
            Task.FromResult(RefreshTokenWithUser);

        public Task<RefreshToken?> GetRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken) =>
            Task.FromResult<RefreshToken?>(null);

        public void AddRefreshToken(RefreshToken refreshToken) => AddedRefreshTokens.Add(refreshToken);

        public Task SaveChangesAsync(CancellationToken cancellationToken) {
            SaveChangesCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCredentialHasher : ICredentialHasher {
        public bool PasswordIsValid { get; init; } = true;

        public bool VerifyPassword(string password, string passwordHash) => PasswordIsValid;
        public string HashPassword(string password) => $"password:{password}";
        public string HashToken(string token) => $"hash:{token}";
    }

    private sealed class FakeTokenService : ITokenService {
        public string CreateAccessToken(User user) => "access-token";
        public string CreateRefreshToken() => "refresh-token";
        public DateTime GetRefreshTokenExpiry() => new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }
}
