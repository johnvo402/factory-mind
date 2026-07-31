# 0011 - Layer-owned dependency injection

- **Decision:** Application and Infrastructure each expose a `DependencyInjection.cs` extension that registers the services implemented or orchestrated by that layer.
- **Decision:** Presentation exposes its HTTP-specific registrations through `FactoryMind.Api/DependencyInjection.cs`; `Program.cs` only composes the layer extensions and configures the request pipeline.
- **Reason:** Registration ownership follows implementation ownership, keeps the composition root readable, and prevents `Program.cs` from knowing repository, validator, behavior, or provider details.
- **Boundary:** Domain and Shared contain no runtime services, so they remain dependency-injection-free instead of adding no-op registrations or framework dependencies.
- **Date:** 2026-07-31
