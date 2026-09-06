# 0038 - Bounded read-only manufacturing AI tools

Date: 2026-09-07

## Decision

FactoryMind uses a two-phase AI flow for live manufacturing questions. A separate, non-streaming
Gemini `generateContent` request may select provider-native `functionCall` parts from an explicit
read-only registry. The server validates every name and argument, injects the authenticated
`CompanyId`, executes at most three distinct calls in one planning round, and converts results to
existing `BusinessDataRecord` evidence. Business and Knowledge RAG then build the final bounded
context, and the existing `IChatCompletionClient.StreamAsync` request produces the SSE answer with
no tool definitions.

## Boundaries

- Tool planning is eligible only for deterministic Business intent or explicit manufacturing Hybrid
  intent. Knowledge-only and ambiguous `Hybrid + All` fallback questions skip planning.
- Planning happens once. Tool results are never returned for another planning round, so there is no
  recursive agent, ReAct loop, autonomous chaining, or planner memory.
- `MaximumToolCallsPerRequest = 3` is enforced by the server after canonical argument deduplication.
  Excess calls are dropped and measured.
- Only seven intentionally registered domain tools are callable. No reflection discovery, generic
  query/repository tool, SQL, dynamic filter, write action, or external API is exposed.
- Tool schemas use `additionalProperties: false`; authoritative server validation rejects unknown
  properties, missing or wrongly typed values, invalid enums, overlong identifiers, and limits
  outside 1-20. Unknown tool names and malformed calls never execute.
- Tenant, company, user, role, permission, connection, and authorization values are absent from every
  tool schema. `CompanyId` comes only from authenticated server context and is included in each EF
  Core query predicate. A foreign-tenant collision returns `not_found`, never `forbidden`.
- Queries use typed EF Core, `AsNoTracking`, bounded projections/result sets, and batched related-data
  reads. Tools do not call `SaveChanges` and cannot mutate manufacturing state.

## Evidence and fallback

Targeted tool records precede Business RAG records. `(EntityType, EntityId)` deduplication keeps the
richer targeted record and then assigns contiguous `[B#]` labels within the existing context/detail
budgets. The existing citation filter persists and exposes only labels used by the final answer in
`ChatBusinessEvidence`; no migration or `[T#]` citation family is introduced. Knowledge RAG continues
to provide `[S#]`, allowing a final answer to combine live execution state with an SOP.

Planner timeout, transport, malformed-response, unknown-tool, and invalid-argument failures degrade
to the existing RAG path. Caller cancellation is propagated through planning, tool execution, RAG,
and final streaming. Planner prose is ignored and never shown or persisted; only structured
`functionCall` parts can trigger execution.

## Observability and privacy

Planning and execution emit `factorymind.ai.tool_plan` and `factorymind.ai.tool.execute` activities,
provider request/token telemetry, and low-cardinality plan/call/duration/rejection metrics. Registered
tool name, finite outcome, finite rejection reason, count, and duration are safe. Questions, prompts,
raw arguments, results, identifiers, tenant/user IDs, and quantities tied to identifiers are never
metric tags or tool logs. Operational telemetry remains in OpenTelemetry; no telemetry tables exist.

## Consequences

The final stream and frontend SSE contract remain unchanged, while live database evidence is more
precise than broad retrieval alone. The model still cannot start or complete orders/operations,
assign machines, change status, move inventory, activate revisions, create records, or delete data.
Write tools remain prohibited.
