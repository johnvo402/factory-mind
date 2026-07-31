# 0022 - Machine business-data vertical slice

- **Decision:** Sprint 4 starts with a complete Machine vertical slice before adding the remaining business-data entities.
- **Tenant boundary:** Every Machine query and mutation is scoped by the authenticated `CompanyId`.
- **Identity:** Machine codes are trimmed, normalized to uppercase, and unique within a company.
- **Status:** The MVP supports `available`, `running`, `maintenance`, and `offline`.
- **Authorization:** The existing `Manager` policy permits Admin and Manager roles to read and mutate business data.
- **Architecture:** Application owns CQRS handlers and `IMachineRepository`; Infrastructure owns EF Core; Presentation owns Minimal API binding and FluentValidation.
- **Date:** 2026-08-01
