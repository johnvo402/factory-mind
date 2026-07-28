using FactoryMind.Domain.Identity;
using Microsoft.EntityFrameworkCore;
namespace FactoryMind.Infrastructure.Persistence;
public sealed class FactoryMindDbContext(DbContextOptions<FactoryMindDbContext> options) : DbContext(options)
{
    public DbSet<Company> Companies => Set<Company>(); public DbSet<User> Users => Set<User>(); public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Company>(entity => { entity.ToTable("companies"); entity.Property(x => x.Name).HasMaxLength(200).IsRequired(); });
        modelBuilder.Entity<User>(entity => { entity.ToTable("users"); entity.HasIndex(x => new { x.CompanyId, x.Email }).IsUnique(); entity.Property(x => x.Email).HasMaxLength(320).IsRequired(); entity.Property(x => x.PasswordHash).IsRequired(); entity.HasOne(x => x.Company).WithMany(x => x.Users).HasForeignKey(x => x.CompanyId); });
        modelBuilder.Entity<RefreshToken>(entity => { entity.ToTable("refresh_tokens"); entity.HasIndex(x => x.TokenHash).IsUnique(); entity.Property(x => x.TokenHash).HasMaxLength(128).IsRequired(); entity.HasOne(x => x.User).WithMany(x => x.RefreshTokens).HasForeignKey(x => x.UserId); });
    }
}
