using FactoryMind.Domain.Identity;

namespace FactoryMind.Domain.Manufacturing;

public sealed class BillOfMaterial {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public int Revision { get; set; }
    public decimal OutputQuantity { get; set; }
    public string Status { get; set; } = BillOfMaterialStatuses.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<BomItem> Items { get; set; } = [];
}

public static class BillOfMaterialStatuses {
    public const string Draft = "draft";
    public const string Active = "active";
    public const string Archived = "archived";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) {
        Draft,
        Active,
        Archived
    };
}
