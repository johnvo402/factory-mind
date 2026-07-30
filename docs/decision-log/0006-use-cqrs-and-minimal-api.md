# 0006 - Use CQRS and Minimal APIs

- **Decision:** Backend uses CQRS with explicit command/query handlers and exposes HTTP endpoints through ASP.NET Core Minimal APIs.
- **Reason:** This keeps each use case focused, makes read/write responsibilities explicit, and supports Clean Code and SOLID without adding unnecessary infrastructure.
- **Boundary:** CQRS does not introduce MediatR, an event bus, or separate read databases during the MVP.
- **Date:** 2026-07-30
