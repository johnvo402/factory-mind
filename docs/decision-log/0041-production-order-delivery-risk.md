# 0041 — Production Order delivery risk

## Status

Accepted — 2026-09-08

## Decision

Production Orders carry user-managed planning metadata: nullable `DueDate` and a required Priority
from `low`, `normal`, `high`, or `urgent`. Existing rows keep no fabricated deadline and default to
`normal`. Priority is independent of delivery risk and never changes automatically.

DueDate is stored as UTC `DateTime` (`timestamp with time zone`). The current UX treats it as a
business delivery day and normalizes a selected date to 23:59:59.999 UTC. This preserves the existing
API/EF timestamp conventions and makes the immutable `CompletedAt <= DueDate` comparison explicit.
`DateOnly` was not chosen because it would introduce a second temporal contract across the current
domain, Npgsql API serialization and Angular client. If exact local delivery hours become a
requirement, timezone ownership must be designed separately.

One server calculator classifies `no_due_date`, `on_track`, `due_soon`, `overdue`,
`completed_on_time`, `completed_late`, and `cancelled`. `DaysUntilDue` is calendar-date subtraction:
today 0, tomorrow 1, yesterday -1. Active orders use current UTC; completed orders use immutable
CompletedAt; cancelled orders are not overdue. Due Soon is configured by `Planning__DueSoonDays`,
validated from 1 to 30, with default 3.

## Consequences and boundary

The same server semantics drive API responses, query predicates, dashboard aggregates, Business RAG
and AI evidence. Angular only renders those facts. Due date is information and does not block Release,
Start or Complete.

This is deadline classification, not scheduling or prediction. FactoryMind does not derive ETA from
routing minutes, model capacity, shifts, queues, machine calendars or future bottlenecks; it does not
reschedule, auto-prioritize or add any AI mutation. Those capabilities require a later capacity and
scheduling model.
