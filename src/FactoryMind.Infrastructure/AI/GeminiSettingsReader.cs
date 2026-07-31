using FactoryMind.Application.Features.Knowledge;
using FactoryMind.Application.Features.Settings;
using Microsoft.Extensions.Options;

namespace FactoryMind.Infrastructure.AI;

public sealed class GeminiSettingsReader(
    IOptions<GeminiSettings> options) : IAiSettingsReader {
    public AiSettingsResponse Get() {
        var settings = options.Value;
        return new AiSettingsResponse(
            "Google Gemini",
            settings.ChatModel,
            settings.EmbeddingModel,
            DocumentEmbeddingConstraints.Dimensions,
            settings.MaximumOutputTokens,
            !string.IsNullOrWhiteSpace(settings.ApiKey));
    }
}
