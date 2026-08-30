# 0032 - Separate finished-goods inventory and atomic production completion

## Status

Accepted.

## Context

FactoryMind already records raw-material stock with `InventoryBalance` and an immutable
`InventoryTransaction` ledger keyed by `MaterialId`. Production Start consumes that stock and
writes traceable `ProductionConsume` entries. Products are a separate business concept, so adding
nullable `MaterialId`/`ProductId` columns to the raw-material tables would weaken their invariants
and make every inventory operation polymorphic.

Production Orders also need an explicit terminal transition that records where finished goods were
received. The output must not be duplicated by concurrent Complete requests or lost when different
orders complete into the same Product/Warehouse balance.

## Decision

- Keep the raw-material inventory schema and operations unchanged. Introduce
  `ProductInventoryBalance` and immutable `ProductInventoryTransaction` entities dedicated to
  finished goods.
- Keep one materialized Product balance for `(CompanyId, WarehouseId, ProductId)`. Its quantity is
  non-negative and uses the same numeric precision as `ProductionOrder.Quantity`.
- Store every finished-goods history quantity as a positive value. The only transaction type in this
  iteration is the strongly typed `ProductionOutput`, whose signed quantity is positive.
- Add the explicit `POST /api/production-orders/{id}/complete` command. It accepts only an active
  destination Warehouse. Product, Company, output quantity, status, user, and Production Order
  reference are derived on the server.
- Complete is valid only for a tenant-owned InProgress order with a locked BOM, a start timestamp, a
  positive quantity, a tenant-owned Product, and an active tenant-owned destination Warehouse.
- `IProductionExecutionRepository.TryCompleteAsync` owns one PostgreSQL transaction. It conditionally
  claims `InProgress -> Completed`, atomically upserts the Product balance with
  `Quantity = Quantity + output`, inserts one `ProductionOutput` ledger row, records `CompletedAt`,
  and commits. Any failure rolls the entire transaction back.
- The conditional state claim prevents the same order from completing twice. PostgreSQL
  `INSERT ... ON CONFLICT DO UPDATE` prevents lost updates when different orders concurrently output
  the same Product into the same Warehouse.
- Output quantity equals `ProductionOrder.Quantity`. Partial completion, yield variance, scrap,
  rework, and over/under-production are deferred until their business semantics are designed.
- Completing an order never writes or changes raw-material balances or transactions. Material was
  already consumed by Start.
- Expose tenant-scoped Product inventory balance and paged immutable history queries. No manual
  Product receipt, issue, adjustment, or transfer command is added.
- Product and Warehouse foreign keys use restrictive deletion. Product deletion is rejected once a
  Product is referenced by a BOM, Production Order, Product balance, or Product transaction.
  Warehouses continue to be deactivated rather than physically deleted.

## Compatibility and limitations

- Existing Production Orders migrate with `CompletedAt = null`. Historical completion timestamps
  are not invented.
- New Product inventory tables start empty. No finished-goods balance is fabricated for legacy
  Completed orders because the destination Warehouse is unknown.
- Completed orders are terminal and remain frozen. InProgress cancellation and material reversal
  remain unsupported.
- Finished goods can enter inventory only through Complete Production Order in this iteration.

## Date

2026-08-30
