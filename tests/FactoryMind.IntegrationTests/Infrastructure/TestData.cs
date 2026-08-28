using FactoryMind.Application.Features.Auth;
using FactoryMind.Domain.Identity;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Infrastructure.Persistence;

namespace FactoryMind.IntegrationTests.Infrastructure;

public static class TestData {
    public const string Password = "FactoryMind@Test#2026";
    public const string CompanyAAdminEmail = "admin-a@factorymind.test";
    public const string CompanyAUserEmail = "user-a@factorymind.test";
    public const string CompanyBAdminEmail = "admin-b@factorymind.test";
    public const string CompanyBUserEmail = "user-b@factorymind.test";
    public const string CompanyAMachineCode = "A-MACHINE-001";
    public const string CompanyBMachineCode = "B-MACHINE-001";

    public static readonly Guid CompanyAId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid CompanyBId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid CompanyAMachineId = Guid.Parse("10000000-0000-0000-0000-000000000201");
    public static readonly Guid CompanyBMachineId = Guid.Parse("20000000-0000-0000-0000-000000000201");

    private static readonly DateTime CreatedAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static void Seed(FactoryMindDbContext dbContext, ICredentialHasher credentialHasher) {
        // Direct DbContext setup is intentionally confined here so every HTTP test starts
        // from the same two-tenant state without adding production-only seed behavior.
        var companyA = new Company { Id = CompanyAId, Name = "Company A", CreatedAt = CreatedAt };
        var companyB = new Company { Id = CompanyBId, Name = "Company B", CreatedAt = CreatedAt };
        var passwordHash = credentialHasher.HashPassword(Password);

        dbContext.AddRange(
            companyA,
            companyB,
            CreateUser(
                "10000000-0000-0000-0000-000000000101",
                CompanyAId,
                "Company A Admin",
                CompanyAAdminEmail,
                UserRoles.Admin,
                passwordHash),
            CreateUser(
                "10000000-0000-0000-0000-000000000102",
                CompanyAId,
                "Company A User",
                CompanyAUserEmail,
                UserRoles.User,
                passwordHash),
            CreateUser(
                "20000000-0000-0000-0000-000000000101",
                CompanyBId,
                "Company B Admin",
                CompanyBAdminEmail,
                UserRoles.Admin,
                passwordHash),
            CreateUser(
                "20000000-0000-0000-0000-000000000102",
                CompanyBId,
                "Company B User",
                CompanyBUserEmail,
                UserRoles.User,
                passwordHash),
            CreateMachine(CompanyAMachineId, CompanyAId, CompanyAMachineCode, "Company A Machine"),
            CreateMachine(CompanyBMachineId, CompanyBId, CompanyBMachineCode, "Company B Machine"));
    }

    private static User CreateUser(
        string id,
        Guid companyId,
        string name,
        string email,
        string role,
        string passwordHash) => new() {
            Id = Guid.Parse(id),
            CompanyId = companyId,
            Name = name,
            Email = email,
            Role = role,
            PasswordHash = passwordHash,
            CreatedAt = CreatedAt
        };

    private static Machine CreateMachine(
        Guid id,
        Guid companyId,
        string code,
        string name) => new() {
            Id = id,
            CompanyId = companyId,
            Code = code,
            Name = name,
            Status = MachineStatuses.Available,
            CreatedAt = CreatedAt,
            UpdatedAt = CreatedAt
        };
}
