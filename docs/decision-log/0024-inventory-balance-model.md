# 0024 - Inventory balance model

## Status

Accepted.

## Context

The MVP needs searchable inventory data for warehouse users and structured context for later Hybrid RAG. The product documents define only material, warehouse, and quantity; they do not yet require receipts, issues, reservations, batches, or multiple warehouse master records.

## Decision

- Model Inventory as the current material balance in a warehouse, not as an immutable stock-movement ledger.
- Keep `Warehouse` as a required string with a maximum length of 100 characters.
- Store `Quantity` as `numeric(18,3)` and reject negative values at the API boundary.
- Enforce one inventory row for `(CompanyId, MaterialId, Warehouse)`.
- Resolve Material through the tenant-scoped material repository before create or update.
- Restrict deletion of a Material while Inventory references it.
- Protect Inventory endpoints and CQRS requests with the Manager policy.

## Consequences

This keeps the Sprint 4 CRUD and RAG source simple while preserving tenant isolation and database integrity. If stock movements, reservations, batches, or many warehouses become real requirements, introduce dedicated Warehouse and InventoryTransaction models in a later decision instead of expanding this balance row prematurely.
