# 0009 - Authorization, validation, and Problem Details

Date: 2026-07-31

## Status

Accepted

## Context

FactoryMind needs consistent authorization and request validation across Minimal API endpoints without moving HTTP concerns into command handlers. Error responses also need a standards-based shape and must not carry duplicate messages in multiple languages.

The implementation in `minhsangdotcom/clean-architecture` was reviewed for its policy-based authorization, endpoint validation filter, and centralized Problem Details handling. FactoryMind keeps the useful boundaries while retaining its source-generated Mediator and K&R formatting conventions.

## Decision

- Define named authorization policies in Application and configure them in Presentation.
- Mark protected Mediator requests with `IAuthorizedRequest`.
- Run `AuthorizationBehavior` before protected request handlers.
- Keep `.RequireAuthorization(...)` on protected Minimal API endpoints.
- Validate Minimal API request models through FluentValidation endpoint filters.
- Map expected `Result` failures, validation failures, authentication failures, authorization failures, and unhandled exceptions to RFC 7807 Problem Details.
- Return one clear English message in `detail`; do not return bilingual message variants.

## Consequences

- HTTP requests are rejected before dispatch when the endpoint policy fails.
- Protected use cases remain guarded when dispatched outside the HTTP endpoint.
- Validators are reusable and endpoints stay focused on binding, dispatch, and response mapping.
- API clients consume a predictable `application/problem+json` error contract.
- Authorization policy names must remain synchronized between request declarations and ASP.NET Core policy registration.
