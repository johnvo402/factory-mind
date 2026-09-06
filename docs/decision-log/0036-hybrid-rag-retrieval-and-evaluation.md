# 0036 - Hybrid RAG retrieval and evaluation

## Status

Accepted.

## Context

Vector-only pgvector retrieval handled semantic paraphrases but could miss exact SOP identifiers,
model numbers, policy codes, and rare technical terms. Character-window chunking also ignored useful
paragraph and sentence boundaries. Business RAG selected small generic record sets and did not yet
represent Work Centers, Routings, production operations, or live Machine assignments.

## Decision

- Retain exact pgvector cosine retrieval and add PostgreSQL full-text retrieval over
  `DocumentChunk.Content`. Lexical search uses the multilingual `simple` configuration so PostgreSQL
  does not apply English-only stemming.
- Create an expression GIN index on
  `to_tsvector('simple', coalesce("Content", ''))`. No generated content, embedding rewrite, or
  external search service is introduced.
- Retrieve at most 20 vector and 20 lexical candidates. Vector candidates remain restricted to the
  authenticated Company, Ready documents, and the current embedding model.
- Fuse candidate ranks with Reciprocal Rank Fusion: each channel contributes `1 / (60 + rank)`.
  Apply only modest deterministic boosts for appearing in both channels, exact normalized phrases,
  exact hyphenated identifiers, title/file matches, token coverage, and bounded channel scores.
  No Gemini or cross-encoder reranker is used.
- Union duplicate Chunk IDs and suppress highly overlapping adjacent chunks from the same document.
  Public `Score` is the deterministic final score normalized against ideal rank-one dual-channel RRF
  and clamped to the range 0-1; it is not described as cosine similarity.
- Chunk independently within every PDF page, preferring paragraph and sentence boundaries, falling
  back to whitespace for long text, and retaining bounded overlap. Explicit re-indexing recreates
  chunks and embeddings with this algorithm; migration does not alter historical rows.
- Preserve the 8,000-character knowledge context budget. Assign `[S#]` labels after final ranking and
  include a Citation only when that exact source was supplied to the model.
- Pass the question to business retrieval. Deterministically rank exact Code/Number, normalized name,
  and token matches ahead of bounded fallbacks. Extend business scopes with Work Centers, Routings,
  and Production Operations, including compact live execution evidence.
- Keep `[B#]` business evidence separate from `[S#]` knowledge sources and retain strict tenant
  filtering. Retrieved text remains untrusted evidence and never becomes an instruction.
- Add a network-free, machine-readable RAG evaluation dataset and CI gate covering Intent Accuracy,
  Business Scope Accuracy, Recall@5, MRR, exact identifier hits, exact business entities, and a
  vector-only baseline comparison. Real PostgreSQL integration tests cover pgvector, FTS, the GIN
  index, document/model/status filters, and tenant isolation.
- AI behavior remains read-only. AI write actions, LLM reranking, external vector databases,
  scheduling, OEE, tools, autonomous agents, and production observability are deferred.

## Date

2026-09-06
