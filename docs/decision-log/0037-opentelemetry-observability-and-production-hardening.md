# 0037 - OpenTelemetry observability and production hardening

## Status

Accepted.

## Context

FactoryMind already used `Microsoft.Extensions.Logging`, but operational diagnostics were limited to
basic Gemini transport messages and a static `/health` response. AI streaming calls were unbounded,
provider token usage was not observed, and RAG stages could not be correlated in a distributed trace.

## Decision

- Retain `Microsoft.Extensions.Logging`; production uses its built-in JSON console provider with
  TraceId, SpanId, ParentId, and request-scoped CorrelationId fields. Serilog is not introduced.
- Use OpenTelemetry with one ActivitySource and Meter named `FactoryMind`. ASP.NET Core, HttpClient,
  and runtime instrumentation are enabled, with vendor-neutral OTLP export optional through
  configuration. Disabled export requires no collector; enabled export requires a valid HTTP(S)
  endpoint and fails fast otherwise.
- Resource metadata contains configured `service.name`, assembly informational version, and
  deployment environment. Tenant, user, conversation, document, and manufacturing entity IDs are
  not Resource attributes or metric dimensions.
- `X-Correlation-ID` is bounded and restricted to safe printable identifier characters. It is an
  operator support identifier, not a TraceId. W3C TraceId remains independently generated and is
  preferred in safe ProblemDetails responses.
- Logs and telemetry never contain questions, prompts, conversation text, evidence/chunk content,
  vectors, credentials, authorization/cookie headers, or Gemini payload/response text. Metric tags
  use finite concepts such as operation, model, intent, purpose, outcome, and retry reason.
- Gemini chat metrics cover requests, duration, response headers, time to first generated chunk,
  stream duration, chunk count, retries, outcomes, and provider-reported input/output/total tokens.
  Embeddings record requests, duration, purpose, batch size, retries, outcomes, and usage metadata
  only when the provider supplies it. Token values are never estimated.
- Gemini pricing is not embedded in source and cost is not calculated in this milestone. A future
  cost feature would require explicit configured rates.
- Chat and embedding calls use linked whole-interaction timeouts of 120 and 30 seconds by default.
  Retrying remains limited to one retry for transport/transient server failures before a successful
  response stream begins. Quota responses are not retried. Caller cancellation is recorded as
  `cancelled`, configured expiry as `timeout`, and stream output is never retried after emission.
- Knowledge RAG traces embedding, vector search, lexical search, and deterministic ranking beneath
  the knowledge span. Metrics record bounded stage durations and candidate/result counts. Business
  RAG records intent, scope count, candidate/evidence counts, duration, and outcome without using
  the full scope flags as a metric tag. Document processing records outcome, duration, chunks, and
  embedding batch count.
- `/health/live` has no dependency checks. `/health/ready` and compatibility `/health` check
  PostgreSQL with a minimal safe response. MinIO is not a global readiness gate because only document
  operations require it; Gemini never gates readiness so core manufacturing remains available during
  AI outages. No active Gemini health request consumes quota.
- OpenTelemetry collectors, dashboards, alerts, telemetry database tables, AI tools, and write
  actions remain deferred.

## Date

2026-09-06
