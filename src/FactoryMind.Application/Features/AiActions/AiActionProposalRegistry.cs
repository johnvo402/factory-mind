using System.Text.Json;

namespace FactoryMind.Application.Features.AiActions;

public sealed class AiActionProposalRegistry : IAiActionProposalRegistry {
    public const string ProposalName = "propose_release_production_order";
    private static readonly JsonElement Parameters = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": { "number": { "type": "string" } },
          "required": ["number"],
          "additionalProperties": false
        }
        """).RootElement.Clone();

    public IReadOnlyList<AiActionDefinition> Definitions { get; } = [new(
        ProposalName,
        "Propose releasing exactly one planned production order. This never executes the release.",
        Parameters)];

    public bool TryReadProductionOrderNumber(AiActionCall call, out string number) {
        number = string.Empty;
        if (call.Name != ProposalName || call.Arguments.ValueKind != JsonValueKind.Object) {
            return false;
        }

        var properties = call.Arguments.EnumerateObject().ToList();
        if (properties.Count != 1
            || properties[0].Name != "number"
            || properties[0].Value.ValueKind != JsonValueKind.String) {
            return false;
        }

        number = properties[0].Value.GetString()?.Trim() ?? string.Empty;
        return number.Length is > 0 and <= 50;
    }
}
