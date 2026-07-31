using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace FactoryMind.Tests;

public sealed class PresentationDependencyInjectionTests {
    [Fact]
    public void Production_registration_rejects_development_JWT_key() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Jwt:Issuer"] = "FactoryMind",
                ["Jwt:Audience"] = "FactoryMind.Web",
                ["Jwt:Key"] = "development-only-change-this-key-before-deployment-2026"
            })
            .Build();
        var services = new ServiceCollection();
        var environment = new TestHostEnvironment { EnvironmentName = Environments.Production };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            FactoryMind.Api.DependencyInjection.AddPresentation(services, configuration, environment));

        Assert.Equal("A strong production JWT key is required.", exception.Message);
    }

    private sealed class TestHostEnvironment : IHostEnvironment {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "FactoryMind.Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
