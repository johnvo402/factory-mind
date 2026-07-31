# 0007 - Shared contracts and feature repositories

- **Decision:** Put genuinely reusable, framework-independent code in `FactoryMind.Shared` and use feature-specific repository interfaces for persistence access.
- **Reason:** Shared contracts prevent duplication while repository abstractions keep CQRS handlers independent from EF Core.
- **Boundary:** `Shared` is not a dumping ground, and generic repositories are not used because they hide feature-specific query intent.
- **Date:** 2026-07-30
