# 0034 - Versioned routing, work centers, and production operation execution

## Status

Accepted.

## Context

The BOM defines required materials, but the execution lifecycle previously moved directly from whole-order material consumption at Start to finished-goods output at Complete. It did not record the manufacturing steps, their order, where they run, or their actual execution state.

## Decision

- Add tenant-owned `WorkCenter` master data with company-unique codes, active/inactive state, and deactivation instead of physical deletion.
- Add Product-owned `Routing` revisions with `draft`, `active`, and `archived` states. Revisions increase monotonically per Product, only Draft revisions are mutable, and a PostgreSQL filtered unique index enforces at most one Active Routing per Product and Company.
- Add ordered `RoutingOperation` rows. Each operation has one positive Sequence, a name, one same-tenant Work Center, non-negative setup/run minutes, and optional description. Operations are strictly sequential.
- Routing activation validates a non-empty operation list and locks every referenced active Work Center inside the activation transaction. The previous Active Routing is archived and the Draft becomes Active atomically.
- Production Order Release now requires both an Active BOM and an Active Routing. It locks their exact IDs, snapshots every routing operation into `ProductionOrderOperation`, and changes the order to Released in one transaction.
- `ProductionOrderOperation` snapshots Sequence, name, Work Center identity/code/name, setup/run time, and description. Execution therefore does not read mutable Routing configuration.
- Operation lifecycle is `pending -> in_progress -> completed` only. Conditional PostgreSQL updates enforce Sequence and state, while a filtered unique index permits at most one InProgress operation per Production Order.
- Production Order Start continues to consume all raw materials exactly once. Material consumption is not moved to operations.
- Production Order Complete conditionally claims the order only when no operation remains Pending or InProgress, then keeps the existing atomic finished-goods balance upsert and ProductionOutput transaction.

## Compatibility and limitations

- `ProductionOrder.RoutingId` remains nullable. Existing Released, InProgress, Completed, and Cancelled rows remain readable without fabricated Routing references or operation snapshots. Only Releases performed after this migration require an Active Routing.
- Historical Manufacturing foreign keys are restrictive. Active and Archived Routing revisions are immutable through the API.
- Machine assignment, calendars, finite-capacity or advanced scheduling, Gantt charts, parallel/branching routes, alternate Work Centers, OEE, downtime, maintenance, scrap, rework, quality, yield, partial quantities, lots/serials, costing, labor, shifts, and AI planning are explicitly deferred.

## Date

2026-09-05
