namespace FactoryMind.Domain.Identity;
public sealed class Company { public Guid Id { get; set; } = Guid.NewGuid(); public string Name { get; set; } = string.Empty; public DateTime CreatedAt { get; set; } = DateTime.UtcNow; public ICollection<User> Users { get; set; } = []; }
