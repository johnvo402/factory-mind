namespace FactoryMind.Infrastructure.AI;

public sealed class OpenAiSettings {
    public const string SectionName = "OpenAi";

    public string BaseUrl { get; init; } = "https://api.openai.com/v1/";
    public string ApiKey { get; init; } = string.Empty;
    public string Model { get; init; } = "your-model-name";
    public double Temperature { get; init; } = 0.2;
    public string SystemPrompt { get; init; } =
        "You are FactoryMind AI. Answer only manufacturing-related questions. "
        + "Do not invent company facts. If required data is unavailable, say that you do not know.";
}
