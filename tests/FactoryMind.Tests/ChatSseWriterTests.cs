using System.Text;
using FactoryMind.Api.Endpoints;
using FactoryMind.Application.Features.Chat;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace FactoryMind.Tests;

public sealed class ChatSseWriterTests {
    [Fact]
    public async Task Stream_writes_conversation_tokens_citations_and_done_in_order() {
        var citation = new CitationResponse(
            1,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Safety manual",
            "safety.pdf",
            2,
            "Stop the machine.",
            0.94);
        var conversationId = Guid.NewGuid();
        var evidence = new BusinessEvidenceResponse(
            1,
            Guid.NewGuid(),
            "machine",
            "MC-01 - Cutter",
            "status=available");
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var writer = new ChatSseWriter(NullLogger<ChatSseWriter>.Instance);

        await writer.WriteAsync(
            context,
            new ChatStream(conversationId, Updates(citation, evidence)),
            CancellationToken.None);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body, Encoding.UTF8).ReadToEndAsync();
        Assert.Equal("text/event-stream; charset=utf-8", context.Response.ContentType);
        Assert.True(body.IndexOf("event: conversation", StringComparison.Ordinal)
            < body.IndexOf("event: token", StringComparison.Ordinal));
        Assert.True(body.IndexOf("event: token", StringComparison.Ordinal)
            < body.IndexOf("event: business-evidence", StringComparison.Ordinal));
        Assert.True(body.IndexOf("event: business-evidence", StringComparison.Ordinal)
            < body.IndexOf("event: citations", StringComparison.Ordinal));
        Assert.True(body.IndexOf("event: citations", StringComparison.Ordinal)
            < body.IndexOf("event: done", StringComparison.Ordinal));
        Assert.Contains(conversationId.ToString(), body);
        Assert.Contains("\"documentTitle\":\"Safety manual\"", body);
        Assert.Contains("\"entityType\":\"machine\"", body);
    }

    private static async IAsyncEnumerable<ChatStreamUpdate> Updates(
        CitationResponse citation,
        BusinessEvidenceResponse evidence) {
        await Task.Yield();
        yield return new ChatTokenUpdate("Stop");
        yield return new ChatTokenUpdate(" now [B1] [S1].");
        yield return new ChatBusinessEvidenceUpdate([evidence]);
        yield return new ChatCitationsUpdate([citation]);
    }
}
