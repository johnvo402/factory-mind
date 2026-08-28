namespace FactoryMind.IntegrationTests.Infrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class IntegrationTestCollection : ICollectionFixture<PostgreSqlFixture> {
    public const string Name = "FactoryMind integration tests";
}
