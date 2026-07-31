# 0025 - Production Order lifecycle

## Status

Accepted.

## Context

The MVP requires CRUD and search for production orders, but the product documents currently define only number, product, quantity, and status. Scheduling, routing, machine assignment, bill of materials, and material consumption are not yet validated requirements.

## Decision

- Normalize `ProductionOrder.Number` to uppercase and make it unique per company.
- Resolve Product through the tenant-scoped product repository before create or update.
- Store quantity as `numeric(18,3)` and require a value greater than zero.
- Use the explicit MVP lifecycle `planned`, `in_progress`, `completed`, and `cancelled`.
- Restrict Product deletion while a production order references it.
- Protect endpoints and CQRS requests with the Manager policy.

## Consequences

The slice supports the documented workflow and structured RAG context without pretending to be a full MRP engine. Scheduling, routing, assignments, bill of materials, and inventory consumption must be added through later decisions when their use cases are defined.
