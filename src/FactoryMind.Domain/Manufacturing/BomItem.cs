namespace FactoryMind.Domain.Manufacturing;

public sealed class BomItem {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BillOfMaterialId { get; set; }
    public BillOfMaterial? BillOfMaterial { get; set; }
    public Guid MaterialId { get; set; }
    public Material? Material { get; set; }
    public decimal Quantity { get; set; }
    public decimal? ScrapPercentage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
