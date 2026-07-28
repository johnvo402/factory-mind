# FactoryMind Development Rules

## Mission

Build an MVP that can be deployed and sold.

Always prioritize simplicity.

---

## Architecture

Use Clean Architecture Lite.

Never over engineer.

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
- SOLID (reasonable)
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

- introduce CQRS

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

Avoid

- magic code

- unnecessary inheritance

- deep nesting

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