# 0018 - RAG context and persisted chat citations

- **Decision:** Every chat message retrieves up to five tenant-scoped knowledge chunks and adds a compact system context before the current user message.
- **Context limits:** Include at most 8,000 context characters and the latest 20 conversation messages. Label sources as `[S1]` through `[S5]` and require the model to say it does not know when the context is insufficient.
- **Citations:** Only sources referenced by `[S#]` in the completed answer are emitted and persisted. Store citation snapshots with the assistant message so conversation history remains explainable if a document later changes.
- **Streaming contract:** SSE emits `conversation`, zero or more `token` events, one `citations` event, then `done`. Failures before streaming remain RFC 7807 Problem Details; failures after streaming use the existing generic `error` event.
- **Persistence:** Add `message_citations` owned by `messages`; citation rows snapshot source document/chunk identifiers, title, file name, page, excerpt, score, and reference number.
- **Boundary:** This slice uses knowledge retrieval only. Intent routing, business-data retrieval, hybrid merging, reranking, and agents remain outside the slice.
- **Date:** 2026-07-31
