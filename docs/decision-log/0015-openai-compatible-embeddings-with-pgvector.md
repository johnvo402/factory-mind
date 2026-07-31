# 0015 - OpenAI-compatible embeddings with pgvector

> Superseded by decision 0027. The pgvector shape remains, but the provider is now Gemini-only.

- **Decision:** Document processing requests embeddings from the configured OpenAI-compatible `embeddings` endpoint after parsing and chunking.
- **Vector shape:** The MVP fixes vectors at 1,536 dimensions. The configured provider/model must return that size so PostgreSQL can enforce `vector(1536)`.
- **Batching:** Send at most 64 chunk texts per provider request and preserve provider indices when mapping vectors back to chunks.
- **Data model:** Store one `DocumentEmbedding` per `DocumentChunk`, including company, model, dimensions, vector, and creation time.
- **Dependency boundary:** Domain stays independent from pgvector. Infrastructure owns the EF persistence record and maps the provider-independent Application draft to `Pgvector.Vector`.
- **Consistency:** Replace chunks and embeddings in one repository transaction; a document becomes `ready` only after both are stored.
- **Search:** Start with exact cosine distance. Do not add HNSW/IVFFlat until document volume and query measurements justify an approximate index.
- **Failure:** Invalid provider responses and transient HTTP failures fail the processing attempt, mark the document failed, and let Hangfire retry.
- **Date:** 2026-07-31
