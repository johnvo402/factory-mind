using System.Runtime.CompilerServices;
using FactoryMind.Api.Routing;
using FactoryMind.Application.Features.Chat;
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
            services.RemoveAll<IEmbeddingClient>();
            services.RemoveAll<IFileStorage>();
            services.RemoveAll<IDocumentProcessingQueue>();
            services.AddSingleton<IChatCompletionClient, TestChatCompletionClient>();
            services.AddSingleton<IEmbeddingClient, TestEmbeddingClient>();
            services.AddSingleton<IFileStorage, TestFileStorage>();
            services.AddSingleton<IDocumentProcessingQueue, TestDocumentProcessingQueue>();
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
