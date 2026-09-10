using FactoryMind.Infrastructure.Persistence;

namespace FactoryMind.Tests;

public sealed class InfrastructureInitializationModeTests {
    [Fact]
    public void Normal_production_startup_does_not_initialize_the_database() {
        var mode = InfrastructureInitializationModeResolver.Resolve([], "Production");

        Assert.Equal(InfrastructureInitializationMode.None, mode);
    }

    [Fact]
    public void Migration_argument_always_selects_explicit_migration_mode() {
        var mode = InfrastructureInitializationModeResolver.Resolve(["--migrate"], "Production");

        Assert.Equal(InfrastructureInitializationMode.Migration, mode);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void Non_production_startup_keeps_automatic_initialization(string environmentName) {
        var mode = InfrastructureInitializationModeResolver.Resolve([], environmentName);

        Assert.Equal(InfrastructureInitializationMode.Automatic, mode);
    }
}
