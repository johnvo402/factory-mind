using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.Auth;
using FactoryMind.Application.Features.Settings;
using FactoryMind.Application.Features.Settings.CreateUser;
using FactoryMind.Application.Features.Settings.UpdateCompanySettings;
using FactoryMind.Application.Features.Settings.UpdateUser;
using FactoryMind.Domain.Identity;

namespace FactoryMind.Tests;

public sealed class SettingsHandlerTests {
    [Fact]
    public async Task Company_update_is_scoped_to_the_current_tenant() {
        var currentUser = new FakeCurrentUser();
        var company = new Company { Id = currentUser.CompanyId, Name = "Old" };
        var repository = new FakeSettingsRepository { Company = company };
        var handler = new UpdateCompanySettingsCommandHandler(repository, currentUser);

        var result = await handler.Handle(
            new UpdateCompanySettingsCommand("  Factory North  "),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Factory North", company.Name);
        Assert.Equal(currentUser.CompanyId, repository.RequestedCompanyId);
        Assert.Equal(1, repository.SaveChangesCount);
    }

    [Fact]
    public async Task Create_user_hashes_password_and_assigns_the_current_company() {
        var currentUser = new FakeCurrentUser();
        var repository = new FakeSettingsRepository();
        var hasher = new FakeCredentialHasher();
        var handler = new CreateUserCommandHandler(repository, hasher, currentUser);

        var result = await handler.Handle(
            new CreateUserCommand(" Operator ", " USER@EXAMPLE.COM ", "Secret123", UserRoles.User),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(currentUser.CompanyId, repository.AddedUser?.CompanyId);
        Assert.Equal("user@example.com", repository.AddedUser?.Email);
        Assert.Equal("hash:Secret123", repository.AddedUser?.PasswordHash);
        Assert.DoesNotContain("Secret123", result.Value?.ToString());
    }

    [Fact]
    public async Task Admin_cannot_demote_their_own_account() {
        var currentUser = new FakeCurrentUser();
        var user = new User {
            Id = currentUser.UserId,
            CompanyId = currentUser.CompanyId,
            Name = "Admin",
            Email = "admin@example.com",
            Role = UserRoles.Admin,
            IsActive = true
        };
        var repository = new FakeSettingsRepository();
        repository.Users.Add(user);
        var handler = new UpdateUserCommandHandler(repository, currentUser);

        var result = await handler.Handle(
            new UpdateUserCommand(
                user.Id,
                user.Name,
                user.Email,
                UserRoles.Manager,
                true),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("settings.self_admin_change_forbidden", result.Error?.Code);
        Assert.Equal(UserRoles.Admin, user.Role);
        Assert.Equal(0, repository.SaveChangesCount);
    }

    private sealed class FakeCurrentUser : ICurrentUser {
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid CompanyId { get; } = Guid.NewGuid();
        public string Role => UserRoles.Admin;
    }

    private sealed class FakeCredentialHasher : ICredentialHasher {
        public bool VerifyPassword(string password, string passwordHash) => false;
        public string HashPassword(string password) => $"hash:{password}";
        public string HashToken(string token) => token;
    }

    private sealed class FakeSettingsRepository : ISettingsRepository {
        public Company? Company { get; set; }
        public List<User> Users { get; } = [];
        public User? AddedUser { get; private set; }
        public Guid? RequestedCompanyId { get; private set; }
        public int SaveChangesCount { get; private set; }

        public Task<Company?> GetCompanyAsync(Guid companyId, CancellationToken cancellationToken) {
            RequestedCompanyId = companyId;
            return Task.FromResult(Company?.Id == companyId ? Company : null);
        }

        public Task<IReadOnlyList<User>> GetUsersAsync(
            Guid companyId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<User>>(Users.Where(user => user.CompanyId == companyId).ToList());

        public Task<User?> GetUserAsync(
            Guid userId,
            Guid companyId,
            CancellationToken cancellationToken) => Task.FromResult(
                Users.SingleOrDefault(user => user.Id == userId && user.CompanyId == companyId));

        public Task<bool> EmailExistsAsync(
            Guid companyId,
            string email,
            Guid? excludedUserId,
            CancellationToken cancellationToken) => Task.FromResult(Users.Any(user =>
                user.CompanyId == companyId
                && user.Email == email
                && (!excludedUserId.HasValue || user.Id != excludedUserId)));

        public void Add(User user) {
            AddedUser = user;
            Users.Add(user);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) {
            SaveChangesCount++;
            return Task.CompletedTask;
        }
    }
}
