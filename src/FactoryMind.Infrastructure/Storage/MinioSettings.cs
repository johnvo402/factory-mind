namespace FactoryMind.Infrastructure.Storage;

public sealed class MinioSettings {
    public const string SectionName = "Minio";

    public string Endpoint { get; init; } = "localhost:9000";
    public string AccessKey { get; init; } = "minioadmin";
    public string SecretKey { get; init; } = "minioadmin";
    public string Bucket { get; init; } = "factorymind";
    public bool UseSsl { get; init; }
}
