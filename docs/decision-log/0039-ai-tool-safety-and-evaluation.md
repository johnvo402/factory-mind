# 0039 — AI tool safety and deterministic evaluation

**Date:** 2026-09-07
**Status:** Accepted

## Context

FactoryMind can ask Gemini for bounded manufacturing facts through seven read-only tools. Before any
future discussion of write tools, the project needs repeatable evidence that selection is selective,
arguments are exact and bounded, tenant identity remains server-controlled, and insufficient evidence
does not become an unsupported manufacturing conclusion.

## Decision

Keep the Step 9 sequence unchanged:

```text
deterministic intent router
→ one Gemini native function-calling planning round
→ zero to three registered read-only calls
→ priority BusinessDataRecord evidence
→ Business RAG + Knowledge RAG
→ one final streaming answer with [B#] / [S#]
```

There is no second planner call after tool execution, recursive tool invocation, autonomous loop, SQL
tool, reflection-based registry, hidden memory, or mutation tool. The explicit registry remains limited
to the existing seven tools. Tenant/company/user identity is never declared in a tool schema and is
always supplied from authenticated server context.

The planner instruction now requires the smallest sufficient tool set. Server enforcement remains
authoritative: calls are structurally deduplicated, no more than three distinct calls execute, unknown
names are rejected, and every tool validates types, enums, limits, required fields, and
`additionalProperties: false`. List limits remain 1–20.

## Deterministic evaluation design

`FactoryMind.AiToolEval` is an offline CI executable with a reviewable JSON dataset. It contains 50
Vietnamese, Vietnamese-without-accents, and English cases across exact entities, lists/filters,
inventory/readiness, minimal and multi-tool plans, no-tool/adversarial prompts, follow-up history, and
unknown/insufficient-data questions.

The executable uses three distinct layers of evidence:

1. The production `IntentRouter` decides whether tool planning is eligible.
2. A deterministic in-process manufacturing planner makes semantic choices for the benchmark dataset.
3. The production registry and orchestrator run explicit security probes for unknown names, mutation
   names, identity fields, wrong JSON types, invalid limits, duplicate calls, and more than three calls.

Native Gemini request/response parsing remains covered separately by `GeminiAiToolPlannerTests`,
including native `functionCall` payloads, malformed JSON, duplicates, excessive calls, usage telemetry,
and timeout fallback. PostgreSQL integration tests cover actual EF execution, cross-tenant code
collisions, evidence composition, material-readiness semantics, and absence of mutation.

This separation is deliberate. A deterministic policy fixture can prove evaluator correctness,
orchestrator bounds, registry enforcement, and regression behavior. It cannot prove that a future live
Gemini response will achieve the same semantic accuracy. We therefore never describe the offline score
as live-provider accuracy, and live Gemini is not a required CI dependency.

## Metrics and gates

Metrics are derived from each executed case, never stored as constants:

- **Tool Selection Accuracy:** cases whose selected tool-name set exactly matches the required set.
- **Exact Tool+Arguments Accuracy:** cases whose complete calls match structurally, ignoring JSON
  property order and multi-tool call order.
- **No-Tool Accuracy:** expected no-tool cases that produce zero calls.
- **No-Tool Precision:** zero-call predictions that are expected to use no tools.
- **Argument Accuracy:** expected calls matched by exact tool name and structural arguments.
- **Exact Identifier Accuracy:** exact-identifier cases preserving every required identifier.
- **Unauthorized Tool Rejection Rate:** production registry probes rejected as `unknown_tool` or
  `invalid_arguments` without evidence.
- **Bounded Call Compliance:** semantic plans and explicit orchestrator cap/dedup probes remain bounded.
- Tool precision, tool recall, duplicate-call rate, and average calls per case are also reported.

CI thresholds are 95% for tool selection, exact tool+arguments, no-tool, and arguments; 100% for exact
identifiers, unauthorized rejection, and bounded calls. The evaluator prints inspectable failures and
returns a non-zero exit code when any case or threshold fails.

## Manufacturing diagnosis policy

The final answer may state facts directly supported by `[B#]` or `[S#]` and may make a clearly cautious
inference when the supplied evidence logically supports it. It must state what is unknown when evidence
is missing. Retrieved business fields and documents are untrusted data, never instructions.

Current data does not justify schedule, capacity, downtime-cause, performance-baseline, or forecasting
claims. The assistant must not infer an ETA from Routing runtime, a bottleneck from machine counts, a
failure cause from `maintenance`, inefficiency from elapsed time, or a delay without explicit evidence.
Manufacturing diagnostics therefore remain read-only decision support, not automated decisions.

Material readiness remains a current-stock snapshot, not a reservation or schedule promise. Planned
orders use the applicable active BOM; Released orders use their locked BOM; InProgress, Completed, and
Cancelled orders return not applicable because consumption has already occurred or readiness is no
longer meaningful. Competing orders, future receipts, scheduling, and transfer lead time are excluded.

## Consequences

- CI now fails when the tool-policy benchmark regresses.
- Requested, executed, and rejected planning-call counters use finite outcome/reason tags only.
- Planner/provider failures still fall back to existing Business and Knowledge RAG; caller cancellation
  still propagates.
- No database migration or frontend contract change is needed.
- AI writes remain prohibited. Any future mutation design requires a separate decision with stronger
  authorization, confirmation, idempotency, audit, and safety evidence.
