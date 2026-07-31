# 0026 - Hybrid RAG routing and business evidence

- **Decision:** Chat routes each question to exactly one of `Business`, `Knowledge`, or `Hybrid`, then builds one bounded context from the selected retrieval paths.
- **Routing:** Normalize Vietnamese text and use deterministic manufacturing keywords. Questions with both source groups or a recommendation cue such as "có nên" are `Hybrid`; questions without a recognized keyword also fall back to `Hybrid`. Do not spend an additional LLM request on intent classification in the MVP.
- **Business retrieval:** Application owns the explicit `IBusinessContextRepository` contract and Infrastructure projects Machine, Material, Inventory, Product, and Production Order data from PostgreSQL. Every query filters `CompanyId`, limits rows per selected scope, and never loads a whole database or entity graph into the prompt.
- **Context:** Label live business records as `[B#]` and knowledge chunks as `[S#]`. Business-only skips vector retrieval, Knowledge-only skips business SQL retrieval, and Hybrid merges both under one system instruction.
- **Evidence:** Persist only `[B#]` records referenced by the completed answer in `message_business_evidence`. Store immutable entity type, entity identifier, title, detail, and reference number snapshots so history remains explainable when live business data changes.
- **Streaming:** SSE emits `conversation`, zero or more `token` events, one `business-evidence` event, one `citations` event, then `done`. The frontend renders business evidence separately from PDF citations.
- **Boundary:** The repository is purpose-built for the RAG read model; generic repositories, a generic Unit of Work, reranking, agents, and an LLM intent classifier remain outside this slice.
- **Date:** 2026-08-01
