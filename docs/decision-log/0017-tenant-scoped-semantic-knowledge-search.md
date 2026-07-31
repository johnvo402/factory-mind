# 0017 - Tenant-scoped semantic knowledge search

- **Decision:** Expose authenticated semantic search as `POST /api/knowledge/search` with a JSON body containing `query` and `limit`.
- **CQRS:** A query handler embeds the question, validates the returned vector shape, and delegates the read to a dedicated `IKnowledgeSearchRepository`.
- **Retrieval:** PostgreSQL performs exact cosine-distance ordering over pgvector; no approximate index or second search engine is introduced.
- **Tenant boundary:** Filter embedding, chunk, and ready document rows by the authenticated `CompanyId` before ordering and limiting results.
- **Limits:** Query text is required and at most 2,000 characters. Result count defaults to 5 and is constrained to 1–20.
- **Response:** Return document and chunk identifiers, document title/file, page number, content, and cosine similarity score so citation can be added without changing retrieval.
- **Boundary:** This slice retrieves knowledge only. Prompt construction, chat integration, thresholds, reranking, and citations remain subsequent work.
- **Date:** 2026-07-31
