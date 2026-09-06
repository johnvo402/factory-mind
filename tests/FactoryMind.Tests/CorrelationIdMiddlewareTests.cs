using System.Diagnostics;
using FactoryMind.Api.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace FactoryMind.Tests;

public sealed class CorrelationIdMiddlewareTests {
    [Fact]
    public async Task Middleware_creates_structured_scope_with_correlation_trace_and_span_ids() {
        var logger = new ScopeCaptureLogger<CorrelationIdMiddleware>();
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "factory-test-123";
        using var activity = new Activity("request").Start();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask, logger);

        await middleware.InvokeAsync(context);

        Assert.Equal("factory-test-123", logger.Scope["CorrelationId"]);
        Assert.Equal(activity.TraceId.ToString(), logger.Scope["TraceId"]);
        Assert.Equal(activity.SpanId.ToString(), logger.Scope["SpanId"]);
        Assert.Equal(
            "factory-test-123",
            context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString());
    }

    private sealed class ScopeCaptureLogger<T> : ILogger<T> {
        public IReadOnlyDictionary<string, object> Scope { get; private set; } =
            new Dictionary<string, object>();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull {
            Scope = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(state);
            return NoopDisposable.Instance;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) {
        }

        private sealed class NoopDisposable : IDisposable {
            public static NoopDisposable Instance { get; } = new();

            public void Dispose() {
            }
        }
    }
}
