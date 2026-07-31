namespace FactoryMind.Infrastructure.AI;

public sealed class GeminiSettings {
    public const string SectionName = "Gemini";

    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/";
    public string ApiKey { get; set; } = string.Empty;
    public string ChatModel { get; set; } = "gemini-3.5-flash-lite";
    public string EmbeddingModel { get; set; } = "gemini-embedding-2";
    public int MaximumOutputTokens { get; set; } = 2_048;
    public string SystemPrompt { get; set; } =
        "You are FactoryMind AI. Answer only manufacturing-related questions. "
        + "Do not invent company facts. If required data is unavailable, say that you do not know.";
}
