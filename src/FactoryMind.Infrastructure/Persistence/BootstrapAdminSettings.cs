namespace FactoryMind.Infrastructure.Persistence;

public sealed class BootstrapAdminSettings {
    public const string SectionName = "BootstrapAdmin";

    public string CompanyName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
