# FactoryMind Development Rules

## Mission

Build an MVP that can be deployed and sold.

Always prioritize simplicity.

---

## Architecture

Use Clean Architecture Lite.

Use CQRS with explicit commands and queries.

Use the source-generated `Mediator` library to dispatch commands and queries to their handlers.

Expose HTTP endpoints through ASP.NET Core Minimal APIs in the Presentation layer.

Use the Repository Pattern: declare feature-specific repository interfaces in Application and implement them in Infrastructure.

Put genuinely reusable, framework-independent contracts and utilities in FactoryMind.Shared.

Handlers return `Result` or `Result<T>` from FactoryMind.Shared; Presentation maps results to HTTP responses.

Protect use cases with named ASP.NET Core authorization policies and the Mediator `AuthorizationBehavior`.

Validate Minimal API request models with FluentValidation endpoint filters before dispatching them.

Return failures as RFC 7807 Problem Details with one clear message in `detail`; do not localize the same response into multiple languages.

Each service-owning layer exposes its registrations through a `DependencyInjection.cs` extension. `Program.cs` composes these extensions and must not register Application or Infrastructure implementation details directly.

Domain and Shared remain free of dependency-injection framework references because they do not own runtime services.

Never over engineer: do not add an event bus, separate read databases, generic repositories, or a Unit of Work abstraction to the MVP.

---

## Tech Stack

Backend

- ASP.NET Core (.NET 9)
- PostgreSQL
- EF Core
- Redis (deferred until a measured cache or session use case exists)
- Hangfire
- MinIO

Frontend

- Angular 20
- Angular Material
- Tailwind CSS
- Angular Signals

AI

- Google Gemini API
- `gemini-3.5-flash-lite` for chat
- `gemini-embedding-2` for embeddings
- pgvector
- RAG

---

## Development Principles

Always follow

- KISS
- YAGNI
- DRY
- SOLID
- Clean Code
- Ship Fast
- MVP First

---

## Before Writing Code

Always

1. Read README.md
2. Read related docs
3. Check current sprint
4. Think
5. Then code

---

## Never

Never

- add new feature without discussion

- add new module

- use new framework without reason

- optimize too early

- create unnecessary abstraction

- introduce Microservices

- introduce Event Bus

- introduce Generic Repository everywhere

---

## Documentation

Whenever architecture changes

Update documentation first.

Never let code and documentation become inconsistent.

---

## Code Quality

Prefer

- readable code

- explicit naming

- small methods

- small classes

- focused command/query handlers

- one responsibility per type

- dependency inversion at architectural boundaries

- K&R brace style: opening braces stay on the same line as the declaration or condition

- shared code in `FactoryMind.Shared` only when it is used by more than one project or feature

- feature-specific repository interfaces and infrastructure implementations

Avoid

- magic code

- unnecessary inheritance

- deep nesting

- business logic in Minimal API endpoint mappings

- validation logic duplicated inside endpoint mappings

- ad hoc authorization checks inside handlers

- commands that return query models or queries that change state

- feature-specific code placed in `FactoryMind.Shared`

- generic repositories that hide useful query intent

- LHS/Allman brace formatting from the reference project

---

## AI

AI is only responsible for

- Chat

- Knowledge

- Business Data

- Hybrid RAG

Nothing else.

Provider rules:

- Use the native Google Gemini API; do not add OpenAI-compatible clients or configuration.
- Keep `GEMINI_API_KEY` in user secrets, environment variables, or deployment secrets only.
- Never commit, log, expose, or return an AI API key through an endpoint.
- Keep free-tier requests bounded and surface quota exhaustion without unbounded retries.

---

## Goal

Finish MVP.

Not perfect software.
