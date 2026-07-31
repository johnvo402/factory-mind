# 0014 - Background PDF parsing and chunking

- **Decision:** After a PDF upload is persisted, enqueue document processing with Hangfire instead of parsing inside the HTTP request.
- **Storage:** Hangfire uses its own PostgreSQL schema in the existing database. Application tables continue to be managed only by EF Core migrations; Redis is not introduced until a running cache use case needs it.
- **Parsing:** Infrastructure uses PdfPig's content-order text extraction. Image-only/scanned PDFs are marked failed because OCR is outside the MVP slice.
- **Chunking:** Application creates page-aware chunks of approximately 1,200 characters with 200 characters of overlap, preferring whitespace boundaries.
- **Data model:** `DocumentChunk` stores tenant, document, sequence, page, and text. Embeddings remain a separate future entity.
- **State:** Documents move through `uploaded`, `processing`, `ready`, or `failed`, with page/chunk counts, processing time, and a short failure message.
- **Reliability:** Processing is idempotent by replacing a document's chunks. Hangfire persists jobs and retries unexpected failures; a protected retry endpoint repairs documents that were uploaded but not queued.
- **Resource limit:** The local worker processes one document at a time and buffers at most the already-enforced 100 MB upload limit. Streaming to a temporary file is deferred until measurements justify it.
- **Date:** 2026-07-31
