# FactoryMind Development Rules

## Mission

Build an MVP that can be deployed and sold.

Always prioritize simplicity.

---

## Architecture

Use Clean Architecture Lite.

Use CQRS with explicit commands and queries.

Expose HTTP endpoints through ASP.NET Core Minimal APIs.

Use the Repository Pattern: declare feature-specific repository interfaces in Application and implement them in Infrastructure.

Put genuinely reusable, framework-independent contracts and utilities in FactoryMind.Shared.

Never over engineer: CQRS does not require MediatR, an event bus, or separate read databases in the MVP.

---

## Tech Stack

Backend

- ASP.NET Core (.NET 9)
- PostgreSQL
- EF Core
- Redis
- Hangfire
- MinIO

Frontend

- Angular 20
- Angular Material
- Tailwind CSS
- Angular Signals

AI

- OpenAI Compatible API
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

- shared code in `FactoryMind.Shared` only when it is used by more than one project or feature

- feature-specific repository interfaces and infrastructure implementations

Avoid

- magic code

- unnecessary inheritance

- deep nesting

- business logic in Minimal API endpoint mappings

- commands that return query models or queries that change state

- feature-specific code placed in `FactoryMind.Shared`

- generic repositories that hide useful query intent

---

## AI

AI is only responsible for

- Chat

- Knowledge

- Business Data

- Hybrid RAG

Nothing else.

---

## Goal

Finish MVP.

Not perfect software.
