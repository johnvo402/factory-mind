# 0031 - Introduce production release and atomic material consumption

## Status

Accepted.

## Context

Production Orders currently allow their status to be edited as ordinary planning data and material previews always follow the Product's active BOM. That is unsafe once execution begins: a later BOM activation can change historical requirements, and consuming several materials through independent inventory operations can leave partial stock changes.

## Decision

- New Production Orders always start as `planned`. Generic create, update, and Excel-import planning flows do not accept status. Only Planned orders may change number, Product, or quantity, and only Planned orders may be physically deleted.
- Add the explicit execution lifecycle `planned -> released -> in_progress`, with cancellation allowed only from Planned or Released. Keep legacy `completed` rows readable, but do not expose a Complete command or permit direct transitions to Completed.
- Release atomically selects the Product's current active BOM, stores its exact `BillOfMaterialId` on the order, sets `released`, and records `ReleasedAt`. A restrictive foreign key retains referenced BOM history. Active and Archived BOMs remain immutable; only Draft BOMs may be edited.
- Release locks the manufacturing definition but does not reserve stock or write inventory transactions. Planned previews use the current active BOM; Released and InProgress previews use the locked revision. Legacy non-Planned orders without a locked revision remain readable but cannot claim an accurate execution preview.
- Start accepts explicit positive Material/Warehouse allocations. The server recalculates required quantities from the locked BOM, including output factor, scrap, and six-decimal rounding. Submitted totals must exactly match every and only required Material. Materials and active Warehouses are resolved inside the authenticated tenant.
- `IProductionExecutionRepository` is the focused Application/Infrastructure boundary for lifecycle state claims and whole-order consumption. The PostgreSQL implementation conditionally changes a Released row to InProgress, conditionally decrements every balance in deterministic order, inserts positive `ProductionConsume` ledger quantities referencing the Production Order, records `StartedAt`, and commits once. Any failure rolls back the entire operation.
- The conditional Released-to-InProgress update is the concurrency gate. Concurrent starts serialize on the Production Order row; exactly one can claim the Released state, so stock is consumed once. Conditional balance decrements prevent negative inventory.
- Inventory balance and transaction quantity scale is widened from three to six decimal places so execution can persist the same precision produced by the BOM requirement calculator.
- InProgress cancellation is deferred because consumed material would require an explicit reversal operation. Completion and finished-goods output are deferred until a Product inventory model exists.

## Compatibility and limitations

- Existing rows migrate with nullable `BillOfMaterialId`, `ReleasedAt`, `StartedAt`, and `CancelledAt`. No historical BOM is invented.
- Existing Completed records remain listable and frozen. Legacy Released/InProgress/Completed records without a locked BOM cannot start or use a historical material preview.
- Released is not a reservation. Stock is revalidated when Start executes and may be consumed by another operation before then.
- This slice does not add material reservations, output receipts, finished-goods balances, completion, or consumption reversal.

## Date

2026-08-30
