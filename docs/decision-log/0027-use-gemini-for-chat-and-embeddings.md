# 0027 - Use Gemini for chat and embeddings

- **Decision:** Remove the OpenAI-compatible provider and use the native Google Gemini API exclusively.
- **Chat:** Use stable `gemini-3.5-flash-lite` through `streamGenerateContent` with server-side `x-goog-api-key` authentication. Preserve the existing Application `IChatCompletionClient` and Presentation SSE boundaries.
- **Embeddings:** Use stable `gemini-embedding-2` through `batchEmbedContents`, with `RETRIEVAL_DOCUMENT` for chunks and `RETRIEVAL_QUERY` for questions. Request 1,536 dimensions so the current pgvector schema remains valid.
- **Re-indexing:** Embeddings from different models are not comparable. Provide an explicit tenant-scoped re-index command that replaces existing document embeddings through the existing processing workflow.
- **Free tier:** Keep prompts, result counts, output size, and retries bounded. Return a safe provider failure when quota is exhausted. Do not enable paid grounding or tools for the MVP.
- **Secrets:** Load the key from `Gemini:ApiKey` or `GEMINI_API_KEY`; use user secrets/environment/deployment secrets and never source control, logs, browser code, or readable settings responses.
- **Privacy:** Gemini free-tier data may be used by Google to improve its products. Treat the free configuration as demo/MVP mode and document that sensitive production data requires a separate privacy and billing decision.
- **Date:** 2026-08-01
