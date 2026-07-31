# 0016 - Centralized API routes

- **Decision:** Define every Presentation route template in one `ApiRoutes` class instead of repeating string literals across endpoint mappings.
- **Naming:** Follow C# conventions with `ApiRoutes.Base` and nested groups such as `ApiRoutes.Auth.Login`; do not introduce an all-uppercase `ROUTE` type.
- **Compatibility:** Keep the existing `/api` base path. The infrastructure health endpoint remains `/health` but is also defined in `ApiRoutes`.
- **Boundary:** Route constants stay in `FactoryMind.Api` because they are HTTP Presentation details, not Application or Shared contracts.
- **Usage:** Endpoint files own handlers and metadata but must reference `ApiRoutes` for group paths, child paths, and route parameters.
- **Date:** 2026-07-31
