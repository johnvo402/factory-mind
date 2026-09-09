# 0042 — Work Center capacity scheduling preview

## Status

Accepted — 2026-09-08

## Decision

FactoryMind models explicit `ParallelCapacity` per Work Center. It is planning capacity and is not
derived from the number or live status of Machines. Preview lanes 1..N are temporary calculation
slots, never future Machine assignments and never persisted.

Company owns one validated IANA `TimeZoneId`, defaulting existing data to `UTC`. Weekly Work Center
shifts are local wall-clock intervals; full-day days off override them. A central calendar service
expands those facts into ordered UTC working intervals and fails controlled on invalid timezone/local
time. No configured shifts means unavailable capacity, not an implicit 24/7 calendar.

The scheduler is deterministic and read-only. It seeds current in-progress operations, then orders
remaining PO work by overdue, due soon, other due and no due; within each group it uses
urgent/high/normal/low, DueDate, Number and Id. Operation sequence is preserved. Same-center work
cannot exceed lanes; different centers can overlap. Work pauses outside working intervals.

Planned orders read the current active Routing and are visibly provisional. Released and InProgress
orders read only their locked `ProductionOrderOperation` snapshots; a later active Routing revision
cannot change them. Missing or invalid inputs become typed unscheduled reasons instead of fallback
assumptions.

All schedule surfaces load the full bounded tenant workload (`Planned`, `Released`, `InProgress`) and
calculate one canonical preview before applying `priority`, `orderId`, or `workCenterId`. These are
view filters, not scenario inputs: they select response rows without removing competing work.
Filtered summaries describe the returned order/Work Center subset, while each returned capacity fact
is unchanged from the canonical global calculation. The same rule applies to both planning AI tools;
global workload limits are enforced before selecting their exact target.

## Duration and projections

Step 12B interprets `SetupTimeMinutes + RunTimeMinutes` as the complete standard duration of one
operation. It does not multiply by Production Order Quantity because the existing domain has no
per-unit/batch runtime semantics. In-progress remaining duration subtracts elapsed calendar working
minutes, not wall time.

Projected completion exists only when all remaining operations fit the strict horizon. Comparing it
to DueDate yields `projected_on_time` or `projected_late`; absent due/completion yields `unknown`.
These are planning projections based on current assumptions, not guarantees and not replacements for
Step 12A delivery status.

An operation may schedule only when its predecessor has a known projected or actual end. The first
operation that fails keeps its typed root reason; downstream work is `blocked_by_predecessor`. A
current-capacity conflict with a valid projected end may still provide the precedence timestamp, but
the order remains unscheduled because the conflict warning is retained. An active locked order whose
operations are all Completed uses the latest valid actual `CompletedAt`; missing completion timestamps
remain unknown and never create fake future work.

## Consequences and boundary

The API and AI may explain schedule preview, planned capacity load and projected lateness. They do
not persist a schedule, drag or reschedule work, change priority, assign Machines, reserve inventory,
optimize globally or promise completion. Those are separate future control-plane decisions.
