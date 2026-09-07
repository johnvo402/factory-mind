# 0040 — Controlled AI release action

## Status

Accepted — 2026-09-07

## Decision

FactoryMind exposes one controlled AI write capability: `release_production_order`. Gemini can only
request `propose_release_production_order(number)`. That output is untrusted and cannot invoke a
command. A deterministic explicit-intent gate runs first, the server resolves the tenant and target,
checks the canonical Manager policy, validates current release prerequisites, and persists a pending
proposal containing server-derived snapshots. Questions, natural-language confirmations, unsupported
mutations, and requests naming multiple orders create no proposal.

Execution requires a direct authenticated `POST /api/ai/actions/{proposalId}/confirm`. The query is
tenant- and creator-scoped, Manager authorization runs again, and the server revalidates order number,
status, product, quantity, active BOM, active Routing, operations, and Work Centers. The existing
`ReleaseProductionOrderCommand` performs the mutation with snapshot expectations checked inside its
locked transaction. The model cannot provide company, user, internal IDs, BOM, Routing, role, or a
confirmation flag.

Proposals expire after 10 configurable minutes (valid range 1–60). Only the creator may read, confirm,
or cancel one. A user may hold at most 10 active pending proposals. Identical unexpired proposals in
the same conversation reuse the existing row. Lifecycle events are append-only: `proposed`,
`confirmed`, `execution_succeeded`, `execution_failed`, `cancelled`, `expired`, and `stale`.

Proposal ID is the idempotency boundary. Atomic status transitions prevent duplicate lifecycle events;
the production-order row lock and unique operation snapshots prevent duplicate release work. A retry
may reconcile an already Released order only when its locked BOM and Routing match the confirmed
snapshot. A BOM, Routing, quantity, product, number, or state change makes the proposal stale. A Work
Center becoming unavailable fails safely. Cancellation is pending-only and idempotent.

The proposal and audit records intentionally store Production Order IDs as historical snapshot values,
not foreign keys. This preserves audit history without making an otherwise deletable Planned order
permanently undeletable. Company, user, conversation, and source-message references use restrictive
foreign keys. No API updates or deletes audit events.

## Rationale and limits

Release was chosen because it has an existing bounded, authorized domain command and does not consume
inventory, start production or operations, assign/start machines, or complete output. Start remains
prohibited because it consumes materials and widens operational risk. The first version intentionally
requires same-user confirmation; multi-user approvals can be designed separately.

There is no auto-confirmation, scheduled execution, queue, confidence threshold, natural-language
approval, generic command/CRUD/SQL tool, or stored hidden reasoning. The synchronous trust boundary is:

```text
Gemini output → untrusted proposal request → strict schema → server tenant + authorization + DB validation
→ pending proposal → human UI confirmation → authorization again → staleness validation → canonical command
```
