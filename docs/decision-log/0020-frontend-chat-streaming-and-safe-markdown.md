# 0020 - Frontend chat streaming and safe Markdown

- **Decision:** The Angular chat client uses `fetch` with a `ReadableStream` and a small framework-independent SSE parser instead of `EventSource`.
- **Reason:** The chat contract is an authenticated `POST` with a JSON body; native `EventSource` only opens GET streams and cannot attach the in-memory bearer token.
- **Session:** The chat API retries the stream once after the shared refresh flow returns a new access token. `AbortController` cancels the request when the subscription or workspace closes.
- **State:** `ChatStore` owns conversation selection, optimistic user/assistant messages, semantic stream updates, and a canonical REST reload after completion.
- **Rendering:** Model output is compiled with `marked`, then passed as an untrusted string to Angular `[innerHTML]`. Angular sanitization remains enabled; no trust-bypass API is permitted.
- **Citations:** Citation snapshots from the backend are rendered as expandable evidence cards with document, page, excerpt, file name, and similarity score.
- **Boundary:** This slice implements Chat UI only. Knowledge upload/search UI and business-data UI remain later frontend slices.
- **Date:** 2026-08-01
