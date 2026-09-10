using Microsoft.Extensions.Hosting;

namespace FactoryMind.Infrastructure.Persistence;

public enum InfrastructureInitializationMode {
    None,
    Automatic,
    Migration
}

public static class InfrastructureInitializationModeResolver {
    public const string MigrationArgument = "--migrate";

    public static InfrastructureInitializationMode Resolve(
        IEnumerable<string> arguments,
        string environmentName) {
        if (arguments.Any(argument => string.Equals(
            argument,
            MigrationArgument,
            StringComparison.OrdinalIgnoreCase))) {
            return InfrastructureInitializationMode.Migration;
        }

        return string.Equals(
            environmentName,
            Environments.Production,
            StringComparison.OrdinalIgnoreCase)
            ? InfrastructureInitializationMode.None
            : InfrastructureInitializationMode.Automatic;
    }
}
