using FactoryMind.Domain.Chat;
using FactoryMind.Domain.Knowledge;
using FactoryMind.Domain.Manufacturing;

namespace FactoryMind.Domain.Identity;

public sealed class Company {
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<User> Users { get; set; } = [];
    public ICollection<Conversation> Conversations { get; set; } = [];
    public ICollection<KnowledgeDocument> Documents { get; set; } = [];
    public ICollection<Machine> Machines { get; set; } = [];
    public ICollection<Material> Materials { get; set; } = [];
    public ICollection<Product> Products { get; set; } = [];
    public ICollection<Inventory> Inventories { get; set; } = [];
    public ICollection<ProductionOrder> ProductionOrders { get; set; } = [];
}
