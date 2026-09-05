namespace FactoryMind.Domain.Manufacturing;

public sealed class RoutingOperation {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoutingId { get; set; }
    public Routing? Routing { get; set; }
    public int Sequence { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid WorkCenterId { get; set; }
    public WorkCenter? WorkCenter { get; set; }
    public int SetupTimeMinutes { get; set; }
    public int RunTimeMinutes { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
