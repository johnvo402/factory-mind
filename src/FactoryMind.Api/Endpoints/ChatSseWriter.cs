using System.Text.Json;
using FactoryMind.Application.Features.Chat;

namespace FactoryMind.Api.Endpoints;

public sealed class ChatSseWriter(ILogger<ChatSseWriter> logger) {
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task WriteAsync(
        HttpContext httpContext,
        ChatStream chatStream,
        CancellationToken cancellationToken) {
        await using var enumerator = chatStream.Updates.GetAsyncEnumerator(cancellationToken);
        var hasFirstUpdate = await enumerator.MoveNextAsync();

        httpContext.Response.StatusCode = StatusCodes.Status200OK;
        httpContext.Response.ContentType = "text/event-stream; charset=utf-8";
        httpContext.Response.Headers.CacheControl = "no-cache";
        httpContext.Response.Headers.Append("X-Accel-Buffering", "no");

        await WriteEventAsync(
            httpContext.Response,
            "conversation",
            new { conversationId = chatStream.ConversationId },
            cancellationToken);

        try {
            if (hasFirstUpdate) {
                await WriteUpdateAsync(httpContext.Response, enumerator.Current, cancellationToken);
            }

            while (await enumerator.MoveNextAsync()) {
                await WriteUpdateAsync(httpContext.Response, enumerator.Current, cancellationToken);
            }

            await WriteEventAsync(httpContext.Response, "done", new { }, cancellationToken);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            logger.LogDebug("Chat stream {ConversationId} was cancelled by the client", chatStream.ConversationId);
        } catch (Exception exception) {
            logger.LogError(exception, "Chat stream {ConversationId} failed after streaming started", chatStream.ConversationId);
            await TryWriteErrorAsync(httpContext.Response, cancellationToken);
        }
    }

    private static Task WriteUpdateAsync(
        HttpResponse response,
        ChatStreamUpdate update,
        CancellationToken cancellationToken) {
        return update switch {
            ChatTokenUpdate token => WriteEventAsync(
                response,
                "token",
                new { token.Content },
                cancellationToken),
            ChatCitationsUpdate citations => WriteEventAsync(
                response,
                "citations",
                new { citations.Citations },
                cancellationToken),
            ChatBusinessEvidenceUpdate evidence => WriteEventAsync(
                response,
                "business-evidence",
                new { evidence.BusinessEvidence },
                cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported chat stream update {update.GetType().Name}.")
        };
    }

    private static async Task WriteEventAsync<T>(
        HttpResponse response,
        string eventName,
        T data,
        CancellationToken cancellationToken) {
        var json = JsonSerializer.Serialize(data, JsonOptions);
        await response.WriteAsync($"event: {eventName}\ndata: {json}\n\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }

    private async Task TryWriteErrorAsync(HttpResponse response, CancellationToken cancellationToken) {
        if (cancellationToken.IsCancellationRequested) {
            return;
        }

        try {
            await WriteEventAsync(
                response,
                "error",
                new { message = "AI service is temporarily unavailable." },
                cancellationToken);
        } catch (Exception exception) when (exception is IOException or OperationCanceledException) {
            logger.LogDebug(exception, "Could not write the chat stream error event");
        }
    }
}
