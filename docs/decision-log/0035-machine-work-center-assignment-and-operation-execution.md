# 0035 - Machine Work Center assignment and operation execution

## Status

Accepted.

## Context

Routing operations identify the Work Center capability or location required for an operation, but
they do not identify the physical Machine that performs it. Machine status was also editable as
ordinary master data, including `running`, so it could not safely represent runtime occupancy.

## Decision

- A Machine belongs to zero or one Work Center. `WorkCenterId` remains nullable so existing Machines
  remain readable without fabricated historical assignments. A supplied Work Center must be active
  and belong to the authenticated Company.
- Routing and RoutingOperation continue to describe the required Work Center only. The physical
  Machine is selected explicitly when a ProductionOrderOperation starts.
- Starting an operation snapshots nullable `MachineId`, `MachineCode`, and `MachineName` into the
  ProductionOrderOperation. The snapshot remains after completion and is not changed by later
  Machine master-data edits.
- `running` is system-managed. Administrative Machine create/update may choose `available`,
  `maintenance`, or `offline`, but cannot write `running`. Historical Machines already in Running
  remain readable and may be moved to an administrative state when no operation is actually using
  them.
- PostgreSQL transactionally claims an Available, same-tenant Machine in the operation's active Work
  Center and changes the Pending operation to InProgress. Any failed validation or transition rolls
  back both changes.
- Completing an assigned InProgress operation and releasing its Running Machine to Available occur
  in one PostgreSQL transaction. Inconsistent assigned Machine state causes a conflict and neither
  side is changed.
- A partial unique index permits a Machine to appear on at most one InProgress
  ProductionOrderOperation. The existing per-Production-Order InProgress uniqueness constraint
  remains.
- Legacy InProgress operations with no Machine assignment may complete without releasing a Machine.
  No Machine history is invented for legacy Pending, InProgress, or Completed operations.
- A Machine cannot be administratively changed while it is referenced by an InProgress operation.
  Once any ProductionOrderOperation references a Machine, restrictive history protection prevents
  hard deletion.
- Automatic selection, scheduling, capacity calendars, telemetry, OEE, downtime, maintenance
  planning, shifts, labor, quality, scrap, rework, and AI scheduling remain deferred.

## Date

2026-09-06
