namespace FactoryMind.Api.Observability;

public sealed class ObservabilitySettings {
    public const string SectionName = "Observability";

    public string ServiceName { get; set; } = "factorymind-api";
    public OtlpSettings Otlp { get; set; } = new();
}

public sealed class OtlpSettings {
    public bool Enabled { get; set; }
    public string Endpoint { get; set; } = string.Empty;
}
