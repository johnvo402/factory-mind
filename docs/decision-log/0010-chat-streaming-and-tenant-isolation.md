# 0010 - Chat streaming and tenant isolation

- **Decision:** Sprint 2 stores every `Conversation` with both `CompanyId` and `UserId`, and repositories require both identifiers when reading or updating chat data.
- **Reason:** Explicit tenant and owner filters prevent cross-company access and keep authorization intent visible in repository methods.
- **Decision:** Application exposes the AI response as `IAsyncEnumerable<string>`; Presentation serializes it as Server-Sent Events, while Infrastructure implements the OpenAI-compatible streaming protocol.
- **Reason:** This keeps HTTP streaming details out of handlers and provider-specific JSON out of Application.
- **Boundary:** Sprint 2 sends conversation history directly to the configured model. Intent routing, retrieval, RAG, citations, tools, and agents remain outside this sprint.
- **Failure handling:** Errors before streaming starts use RFC 7807 Problem Details. After SSE starts, the stream emits one generic `error` event because the HTTP status can no longer change.
- **Date:** 2026-07-31
