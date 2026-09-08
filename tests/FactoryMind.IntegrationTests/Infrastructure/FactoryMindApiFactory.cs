using System.Runtime.CompilerServices;
using FactoryMind.Api.Routing;
using FactoryMind.Application.Features.Chat;
using FactoryMind.Application.Features.AiActions;
using FactoryMind.Application.Features.Knowledge;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FactoryMind.IntegrationTests.Infrastructure;

public sealed class FactoryMindApiFactory(string connectionString) : WebApplicationFactory<Program> {
    public async Task StartAsync() {
        using var client = CreateClient();
        using var response = await client.GetAsync(ApiRoutes.Health);
        response.EnsureSuccessStatusCode();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) {
        builder.UseSetting("ConnectionStrings:FactoryMind", connectionString);
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => {
            configuration.AddInMemoryCollection(new Dictionary<string, string?> {
                ["ConnectionStrings:FactoryMind"] = connectionString,
                ["BootstrapAdmin:CompanyName"] = "FactoryMind Integration Bootstrap",
                ["BootstrapAdmin:Name"] = "Integration Bootstrap Admin",
                ["BootstrapAdmin:Email"] = "bootstrap@factorymind.test",
                ["BootstrapAdmin:Password"] = "FactoryMind@Test#2026"
            });
        });
        builder.ConfigureTestServices(services => {
            services.RemoveAll<IChatCompletionClient>();
            services.RemoveAll<IAiToolPlanner>();
            services.RemoveAll<IAiActionPlanner>();
            services.RemoveAll<IEmbeddingClient>();
            services.RemoveAll<IFileStorage>();
            services.RemoveAll<IDocumentProcessingQueue>();
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<IChatCompletionClient, TestChatCompletionClient>();
            services.AddSingleton<IAiToolPlanner, ZeroToolPlanner>();
            services.AddSingleton<IAiActionPlanner, TestActionPlanner>();
            services.AddSingleton<IEmbeddingClient, TestEmbeddingClient>();
            services.AddSingleton<IFileStorage, TestFileStorage>();
            services.AddSingleton<IDocumentProcessingQueue, TestDocumentProcessingQueue>();
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(
                new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero)));
        });
    }

    private sealed class TestChatCompletionClient : IChatCompletionClient {
        public async IAsyncEnumerable<string> StreamAsync(
            IReadOnlyList<ChatPromptMessage> messages,
            [EnumeratorCancellation] CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return "Deterministic integration test response.";
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class ZeroToolPlanner : IAiToolPlanner {
        public Task<AiToolPlan> PlanAsync(
            IReadOnlyList<ChatPromptMessage> messages,
            IReadOnlyList<AiToolDefinition> tools,
            CancellationToken cancellationToken) =>
            Task.FromResult(new AiToolPlan([]));
    }

    private sealed class TestActionPlanner : IAiActionPlanner {
        public Task<AiActionPlan> PlanAsync(
            IReadOnlyList<ChatPromptMessage> messages,
            IReadOnlyList<AiActionDefinition> actions,
            CancellationToken cancellationToken) {
            var text = messages.LastOrDefault()?.Content ?? string.Empty;
            var match = System.Text.RegularExpressions.Regex.Match(
                text, @"\bPO-[A-Za-z0-9-]+\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!match.Success) return Task.FromResult(new AiActionPlan([]));
            using var document = System.Text.Json.JsonDocument.Parse(
                System.Text.Json.JsonSerializer.Serialize(new { number = match.Value }));
            return Task.FromResult(new AiActionPlan([
                new AiActionCall(AiActionProposalRegistry.ProposalName, document.RootElement.Clone())
            ]));
        }
    }

    private sealed class TestEmbeddingClient : IEmbeddingClient {
        public Task<EmbeddingBatch> CreateAsync(
            IReadOnlyList<string> inputs,
            EmbeddingPurpose purpose,
            CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<float[]> vectors = inputs
                .Select(_ => new float[DocumentEmbeddingConstraints.Dimensions])
                .ToList();
            return Task.FromResult(new EmbeddingBatch("integration-test-embedding", vectors));
        }
    }

    private sealed class TestFileStorage : IFileStorage {
        public Task UploadAsync(
            string objectKey,
            Stream content,
            long length,
            string contentType,
            CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task<Stream> DownloadAsync(string objectKey, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<Stream>(new MemoryStream());
        }
    }

    private sealed class TestDocumentProcessingQueue : IDocumentProcessingQueue {
        public void Enqueue(Guid documentId, Guid companyId) {
        }
    }
}
