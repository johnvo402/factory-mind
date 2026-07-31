# 0008 - Use Mediator, Result and Presentation layer

- **Decision:** CQRS requests are dispatched through source-generated `Mediator`; handlers return `Result` or `Result<T>`; Minimal API endpoints form the Presentation layer.
- **Reason:** This separates HTTP concerns from use cases and gives every command/query a consistent success or failure contract.
- **Boundary:** Keep the implementation lightweight: no event bus, generic Unit of Work, generic repository or separate read database.
- **Style:** FactoryMind uses K&R braces, not the LHS/Allman brace style used in the reference repository.
- **Date:** 2026-07-31
