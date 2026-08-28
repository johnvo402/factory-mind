# 0030 - Introduce versioned Bill of Materials and material requirements planning

## Status

Accepted.

## Context

Products and Production Orders currently describe what should be manufactured, while material inventory describes what is on hand. FactoryMind needs a tenant-scoped definition of what each Product is made from and a read-only preview that compares the materials required for a requested quantity with current inventory.

## Decision

- Add `BillOfMaterial` revisions owned by a Company and Product. Revisions are assigned monotonically per Product and use the explicit lifecycle `draft`, `active`, and `archived`.
- Allow at most one active revision per `(CompanyId, ProductId)`. Activation archives the previous active revision and activates the selected Draft in one repository transaction; a filtered unique database index remains the final concurrency guard.
- Keep `OutputQuantity` on each BOM. An item quantity describes the amount required for that output quantity, not necessarily for one Product.
- Store BOM quantities as decimal values. Require positive output/item quantities, one occurrence of each Material per BOM, and an optional scrap percentage between 0 and 100.
- Resolve Product and every Material through the authenticated user's Company. HTTP bodies never accept `CompanyId`; cross-tenant references follow the existing not-found convention.
- Update a Draft BOM together with its complete item list. Do not expose physical BOM deletion. Product and Material foreign keys are restrictive so version history cannot disappear through master-data deletion.
- Calculate each requirement as `item quantity * requested quantity / output quantity * (1 + scrap percentage / 100)` and round the planning result to six decimal places.
- Calculate availability as the sum of all `InventoryBalance` rows for the Material in the current Company, across that Company's warehouses. Shortage is `max(required - available, 0)` and `CanProduce` is true only when every item is sufficient.
- Keep requirement calculation read-only. It creates no reservations, balance changes, or `InventoryTransaction` rows.
- Product previews use the Product's current active BOM. Production Order previews use the order quantity and the Product's current active BOM in this iteration.
- Extend the bounded business-data read model so Product context can include the active BOM revision and its material components. Do not add AI tools or function calling.

## Consequences

The feature answers what a Product is made from and whether current stock is sufficient without pretending to schedule or execute production. A historical Production Order can still observe a different live BOM after a revision is activated. Before production execution or inventory consumption is introduced, the order must snapshot or reference the exact BOM revision used so its historical material definition cannot change silently.

## Date

2026-08-28
