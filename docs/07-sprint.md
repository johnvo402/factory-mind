# 📄 07 - Implementation Roadmap

Mục tiêu của document này:

> **Biến toàn bộ tài liệu thành các Sprint có thể thực hiện.**

Không nói về business.

Không nói về AI.

Chỉ nói:

> **Tuần này code gì?**

---

# Nguyên tắc

Có 5 nguyên tắc.

### 1. Mỗi Sprint phải chạy được

Không có Sprint nào chỉ viết model.

Mỗi Sprint phải demo được.

---

### 2. Vertical Slice

Ví dụ Chat.

Không làm:

```text
Backend 100%

↓

Frontend 100%
```

Mà làm:

```text
UI

↓

API

↓

Database

↓

Done
```

Một tính năng hoàn chỉnh.

---

### 3. Không tối ưu sớm

Nếu chạy được.

Để đó.

---

### 4. MVP First

Nếu khách hàng chưa dùng.

Không làm.

---

### 5. Ship Fast

Deploy liên tục.

---

# Timeline

Khoảng **10 tuần**.

```text
Week 1

Week 2

...

Week 10
```

---

# Sprint 1

## Foundation

Mục tiêu

Có thể chạy project.

### Backend

* Solution
* PostgreSQL
* EF Core
* JWT
* Login
* Company
* User

### Frontend

* Login
* Layout
* Sidebar
* Theme

### Done

Có thể đăng nhập.

---

# Sprint 2

## AI Chat

### Backend

* Chat API
* Gemini API
* Streaming
* Conversation
* Message

### Frontend

* Chat UI
* Markdown
* Streaming

### Done

Có thể chat với AI.

Current provider: `gemini-3.5-flash-lite` through the native Gemini streaming API.

---

# Sprint 3

## Knowledge

### Backend

* Upload PDF
* Parse
* Chunk
* Embedding

### Frontend

* Upload
* Document List
* Search

### Done

Upload được tài liệu.

AI trả lời được tài liệu.

---

# Sprint 4

## Business Data

### Backend

CRUD

* Machine
* Material
* Product
* Inventory
* Production Order

### Frontend

Table

Form

Search

### Done

Có dữ liệu.

---

# Sprint 5

## Hybrid RAG

### Backend

Intent

Business Retrieval

Knowledge Retrieval

Merge Context

Citation

### Frontend

Source

Evidence

### Done

AI trả lời từ:

* SQL
* PDF

Implementation status: Hybrid RAG routes `Business`, `Knowledge`, and `Hybrid` questions, merges
bounded tenant-scoped context, and returns separately rendered SQL evidence and PDF sources.

### Step 7 - RAG quality upgrade

Done.

* Knowledge search combines 20 pgvector and 20 PostgreSQL `simple` FTS candidates.
* Reciprocal Rank Fusion (`K = 60`), exact identifier/title/file boosts, and adjacent-chunk
  deduplication deterministically select up to 8 final sources.
* Chunking stays within each PDF page and prefers paragraph/sentence boundaries with overlap.
* Citation/evidence numbering follows the final bounded context exactly.
* Business scopes now include Work Centers, Routings, and Production Operations.
* Business retrieval prioritizes exact Codes/Numbers and includes current manufacturing execution
  evidence without inventing scheduling or downtime facts.
* A 25-case offline RAG evaluation suite gates Recall@5, MRR, routing, scopes, and exact identifiers
  and entities; Testcontainers covers real pgvector and indexed lexical retrieval.
* Gemini remains generation/embedding only. Reranking is deterministic and all AI behavior remains
  read-only.

### Step 8 - Observability and production hardening

Done.

* Microsoft ILogger remains the logging stack; Production emits built-in structured JSON with
  CorrelationId, W3C TraceId and SpanId context.
* OpenTelemetry ASP.NET Core, HttpClient and runtime instrumentation share the canonical
  `FactoryMind` ActivitySource/Meter; optional OTLP export is configuration-driven and validated.
* Gemini chat/embedding calls have bounded timeouts, safe retry boundaries and consistent
  success/error/cancelled/timeout/quota/invalid-response outcomes.
* Provider usage metadata records exact input/output/total tokens without estimation or hardcoded
  pricing. Chat telemetry distinguishes response headers, first generated chunk and total stream.
* Knowledge RAG exposes embedding/vector/lexical/rank trace structure and stage metrics; Business RAG
  and document processing expose bounded, low-cardinality measurements without sensitive content.
* `/health/live` is dependency-free. `/health/ready` and compatibility `/health` check PostgreSQL;
  Gemini does not gate readiness and is never pinged by health probes.
* ActivityListener/MeterListener unit tests and real PostgreSQL HTTP/RAG integration tests validate
  correlation, trace identity, health response safety, telemetry values and privacy boundaries.
* No telemetry persistence migration, dashboard, collector deployment, AI tool, or write action was
  added.

### Step 9 - Bounded read-only manufacturing AI tools

Done.

* A deterministic eligibility policy invokes a separate non-streaming Gemini native function-calling
  planner only for Business intent and explicit manufacturing Hybrid intent.
* One planning round may request zero to three distinct calls. Canonical duplicate calls are removed,
  the server hard limit is three, and final `StreamAsync` receives no function declarations.
* The explicit registry contains only `get_production_order_status`, `get_machine_status`,
  `list_machines`, `get_work_center_status`, `get_material_inventory`,
  `get_production_order_material_readiness`, and `list_production_orders`.
* Every tool is typed, tenant-scoped, bounded, `AsNoTracking`, and strictly validates arguments with
  no model-supplied identity. Unknown, malformed, cross-tenant, and injection-like inputs cannot
  escape the registered read-only surface.
* Material readiness reuses the production requirement calculator, aggregates active-warehouse stock,
  states reservation/scheduling limitations, and returns `not_applicable` after consumption begins.
* Targeted tool records precede and dedupe with Business RAG, reuse contiguous `[B#]` evidence and
  existing citation-filtered persistence, while Knowledge RAG continues to provide `[S#]`.
* Planner failures gracefully fall back to existing RAG. Cancellation and a validated 15-second
  planning timeout are propagated without changing the SSE/frontend contract.
* OpenTelemetry covers plan/execution counts, duration, outcomes and finite rejection reasons without
  logging questions, prompts, arguments, results, identifiers, tenant IDs, or other high-cardinality data.
* Unit, real PostgreSQL integration, hybrid/evidence persistence, read-only regression, RAG evaluation,
  frontend, release build, and production-image checks cover the milestone without a migration.

Step 10 is completed below. Unrestricted writes and all mutation tools remain deferred.

---

# Sprint 6

## Dashboard

Widget

* Orders
* Inventory
* Machine
* Alert

Done.

Implementation status: tenant-scoped KPI summary is rendered on the Chat home.

---

# Sprint 7

## Import Excel

Backend

* Upload Excel
* Mapping
* Import

Frontend

Wizard

Preview

Done.

Implementation status: exactly five business entity types support bounded preview, mapping, row validation, and transactional `.xlsx` import: Machine, Material, Product, raw Inventory opening balance, and Production Order. Each wizard downloads an authenticated backend-generated template with `Data` and `Hướng dẫn` sheets. Work Centers and read-only Finished Goods hide import completely.

---

# Sprint 8

## Settings

* Company
* Users
* AI Model

Done.

Implementation status: Admin-only Company/Users settings and safe read-only Gemini metadata are implemented; provider secrets remain server-side.

---

# Sprint 9

## Polish

* Loading
* Error
* UX
* Performance
* Bug

Done.

Implementation status: the Industrial AI Cockpit redesign is implemented with semantic tokens, route-backed navigation, responsive mobile navigation, accessible dialogs, consistent SVG icons, and refined Login/Chat/Data/Knowledge/Settings workspaces.

---

# Sprint 10

## Deploy

Docker

MinIO

Redis

PostgreSQL

VPS

Demo

Khách hàng đầu tiên.

Implementation status: production API/frontend images, internal PostgreSQL/MinIO topology, health checks, required secrets, and GHCR image delivery are implemented. Production Compose is registry-only, defaults to the moving `prod` tag for the latest successful `main` release, supports a full commit-SHA override for rollback, hard-codes the Production runtime, and never builds application images on the deployment host. VPS/TLS rollout remains environment-specific and requires an approved target, credentials, backup, and rollback procedure. Redis remains deferred because no running cache or session use case requires it.

---

# Definition of Done

Mỗi task chỉ được Done nếu:

* Có API.
* Có UI.
* Test thủ công.
* Không lỗi nghiêm trọng.
* Deploy được.

---

# Manufacturing planning increment

## Versioned BOM and material requirements

* Draft, Active, and Archived BOM revisions per Product.
* One active revision per Product and Company.
* Decimal output quantity, component quantity, and optional scrap.
* Read-only Product and Production Order material-requirement previews.
* Availability summed from tenant-scoped warehouse balances.
* Product BOM management and accessible shortage/adequacy UI.
* No reservation, consumption, production output, or scheduling.

Implementation status: implemented as the next focused manufacturing-domain slice after the warehouse inventory ledger. Production execution must later snapshot/reference the BOM revision it uses.

---

## Production execution increment

* Explicit Planned -> Released -> InProgress lifecycle; Cancel only before consumption.
* Release locks the exact active BOM revision without reserving stock.
* Start requires explicit warehouse allocations and recalculates requirements on the server.
* Whole-order raw-material consumption, ledger insertion, and state change are atomic and double-start safe.
* Complete accepts an active destination Warehouse and atomically records Product balance and immutable ProductionOutput history.
* Conditional state claims prevent duplicate output; PostgreSQL upsert preserves concurrent output to the same Product/Warehouse balance.
* Partial completion, over/underproduction, reservation, and reversal remain out of scope.

Implementation status: the lifecycle now runs Planned -> Released -> InProgress -> Completed. Raw-material consumption occurs only at Start, finished-goods output occurs only at Complete, and legacy Completed orders remain readable without fabricated historical output.

---

## Routing and production operation execution increment

* Tenant-owned Work Centers with deactivation and company-unique codes.
* Draft, Active, and Archived Routing revisions per Product, with one Active revision.
* Strictly sequential Routing Operations with Work Center, setup time, and run time.
* Release atomically locks the exact Active BOM and Routing revisions and creates immutable ProductionOrderOperation snapshots.
* Operations execute `Pending -> InProgress -> Completed` in Sequence with PostgreSQL-safe conditional transitions.
* Production Order Complete is blocked until every required operation is Completed.
* Existing orders keep nullable `RoutingId`; the migration does not invent historical routes or operations.
* Machine assignment, capacity scheduling, parallel graphs, OEE, downtime, scrap, rework, and AI planning remain deferred.

Implementation status: implemented as the execution bridge between Production Order Start and Complete, including HTTP/PostgreSQL integration tests and minimal Angular management/execution workspaces.

---

## Machine assignment and operation execution increment

* Optional Machine-to-Work-Center ownership preserves legacy Machines with no fabricated assignment.
* Routing identifies the required Work Center; a user explicitly selects the physical Machine at operation Start.
* Start atomically claims an Available Machine as Running and snapshots Machine identity/code/name into the ProductionOrderOperation.
* Complete atomically completes the operation and releases its assigned Machine to Available.
* PostgreSQL partial uniqueness enforces at most one InProgress operation per Machine while retaining the existing per-order constraint.
* Running is system-managed; administrative updates expose only Available, Maintenance, and Offline.
* Active execution blocks Machine edits and deletion; any historical operation reference permanently blocks hard deletion.
* Legacy InProgress operations without Machine assignment may still complete without fabricated Machine history.
* Automatic selection, capacity scheduling, calendars, telemetry, OEE, downtime, maintenance planning, labor, shifts, quality, scrap, rework, and AI scheduling remain deferred.

Implementation status: implemented as the focused physical-resource execution layer on top of Work Centers and immutable ProductionOrderOperation snapshots, including PostgreSQL concurrency and compatibility coverage plus minimal Angular selectors.

---

# MVP Checklist

## Authentication

* [x] Login
* [x] Logout

---

## AI

* [x] Chat
* [x] Streaming
* [x] Citation

---

## Knowledge

* [x] Upload
* [x] Search

---

## Data

* [x] CRUD

---

## Dashboard

* [x] KPI

---

## Deploy

* [ ] VPS

---

# Versioning

Không cần phức tạp.

```text
v0.1

Foundation

↓

v0.2

Chat

↓

v0.3

Knowledge

↓

v0.4

Business

↓

v0.5

Hybrid RAG

↓

v1.0

Production Ready
```

---

# 📚 Đến đây chúng ta có bộ tài liệu hoàn chỉnh

```text
docs/

01-vision.md
02-prd.md
03-ai-rag.md
04-database.md
05-frontend.md
06-backend.md
07-implementation-roadmap.md
```

---

# Nhưng mình muốn đề xuất **một thay đổi cuối cùng**

Mình muốn thêm **một thư mục không phải tài liệu thiết kế**, mà là nơi ghi lại các quyết định trong quá trình phát triển:

```text
docs/

decision-log/

0001-use-pgvector.md

0002-chat-homepage.md

0003-use-cqrs.md

0004-no-agent.md
```

Mỗi file chỉ khoảng 5–10 dòng:

* **Quyết định:** Dùng pgvector thay vì Qdrant.
* **Lý do:** Đơn giản hóa hạ tầng MVP.
* **Ngày:** 2026-07-24.

Điều này rất hữu ích sau vài tháng khi bạn nhìn lại và tự hỏi: *"Tại sao mình lại làm như vậy?"*

---

# Step 10 — AI tool safety, evaluation và decision support

* [x] Giữ one-round native Gemini planner và tối đa ba tool.
* [x] Giữ registry đúng bảy manufacturing tool read-only, không thêm mutation/SQL tool.
* [x] Thêm 50 deterministic evaluation cases đa ngôn ngữ.
* [x] Đo selection, exact arguments, no-tool, exact identifier, unauthorized rejection và bounds.
* [x] Kiểm tra minimal sufficient và multi-tool planning.
* [x] Kiểm tra unknown tool, identity fields, wrong types, invalid limits, duplicate và call cap.
* [x] Giữ Planned/Released BOM semantics và InProgress readiness not applicable.
* [x] Tăng instruction cho fact/inference/unknown; cấm ETA, bottleneck và root-cause suy diễn.
* [x] Kiểm tra timeout/malformed planner fallback và caller cancellation.
* [x] Kiểm tra tenant collision và no-mutation bằng PostgreSQL integration tests.
* [x] Thêm deterministic AI tool evaluation thành Backend CI quality gate.
* [x] Không migration, không thay đổi SSE/frontend contract.

# Step 11 — Controlled AI release action

* [x] Tách action planner/registry khỏi 7 read-only tools; allowlist chỉ có `propose_release_production_order`.
* [x] Dùng conservative explicit-intent gate; reject question, natural-language confirm, unsupported và multi-order.
* [x] Persist pending proposal 10 phút + server snapshot + append-only lifecycle audit.
* [x] Enforce tenant, same-user và canonical Manager policy cả lúc propose lẫn confirm.
* [x] Confirm/cancel/detail/reload endpoints chỉ nhận proposal ID; frontend card inert cho tới click.
* [x] Revalidate PO/BOM/Routing/Work Centers và dùng lại `ReleaseProductionOrderCommand` với locked snapshot guard.
* [x] Idempotent retry, concurrent confirmation reconciliation, stale/expired/cancelled terminal handling.
* [x] Thêm `ai-action-proposal` SSE, finite telemetry và deterministic action evaluation (25 cases) vào CI.

AI vẫn không thể start/complete/cancel order, start/complete operation, assign/change Machine, consume/adjust
inventory hoặc activate BOM/Routing. Release proposal luôn cần explicit UI/API confirmation.

# Manual manufacturing frontend completion

Done.

* [x] Planned Production Order có material preview, confirmed manual Release, Edit/Delete và confirmed Cancel.
* [x] Released Production Order hiển thị BOM/Routing đã khóa, cho phép cấp phát một Material qua nhiều Warehouse và chỉ Start sau bước xác nhận thứ hai.
* [x] Allocation dùng đúng DTO backend, yêu cầu Warehouse, số lượng dương và tổng chính xác theo requirement sáu chữ số thập phân.
* [x] Execution panel tải lại dedicated operations endpoint và Machines; Start/Complete Operation refresh cả ba state Orders/Operations/Machines.
* [x] InProgress Production Order có Complete dialog với active destination Warehouse; backend vẫn quyết định operations đã hoàn tất và tạo ProductionOutput.
* [x] `/data/product-inventories` cung cấp Kho thành phẩm read-only với balance filters, transaction filters và pagination.
* [x] Kho vật tư và Kho thành phẩm được tách rõ theo Material/Inventory ledger và Product/ProductInventory ledger.
* [x] Raw inventory history hỗ trợ các filter Warehouse, Material, transaction type, from/to và pagination đã có ở backend.
* [x] Route, service, store và component tests bảo vệ manual lifecycle; không thêm AI mutation mới hoặc thay đổi backend business rule.
* [x] Finished Goods history hiển thị `referenceType`, `referenceId` khi có và note để truy vết Production Output.
* [x] Mutation success được tách khỏi refresh failure; follow-up GET lỗi chỉ tạo warning và không retry POST.
* [x] Excel template `.xlsx` được sinh từ `ExcelImportDefinition`, có `Data` đầu tiên và `Hướng dẫn`; Work Centers/Finished Goods không có import action.

Lifecycle UI:

```text
Planned -> Release -> Released -> Start + raw material consumption
         \-> Cancel      \-> Cancel
InProgress -> sequential operations -> Complete -> ProductInventory ProductionOutput
```

Manual Release và AI-confirmed Release là hai entry point có xác nhận riêng nhưng cùng giữ backend
làm authority. Frontend không optimistic-update status, không tự issue raw inventory và không ghi
ProductInventory trực tiếp.

---

# Step 12A — Production planning & delivery risk

* [x] DueDate UTC nullable và typed Priority với migration tương thích dữ liệu cũ.
* [x] Central deterministic risk calculator, fixed-clock boundaries và Due Soon 3 ngày cấu hình được.
* [x] Tenant-scoped filters, allowlisted sort, stable server pagination và planning endpoint không load Operations.
* [x] Dashboard planning aggregates và Angular form/table/filter/risk shortcuts.
* [x] Bảy AI read tools được giữ nguyên; hai PO tools mở rộng delivery facts/filter, không thêm mutation.
* [x] Business RAG, 74-case AI Tool Eval, RAG Eval và PostgreSQL tenant/migration/dashboard tests.
* [x] Scheduling, Gantt, capacity calendar, bottleneck prediction, ETA, rescheduling và auto-priority vẫn deferred sang 12B+.

# Step 12B — Work Center capacity & deterministic scheduling preview

* [x] Company IANA timezone, Work Center ParallelCapacity, weekly shifts và full-day exceptions.
* [x] Atomic tenant-scoped calendar GET/PUT với validation và không giả định 24/7.
* [x] Central UTC calendar expansion và deterministic abstract-lane scheduler dùng TimeProvider.
* [x] Planned active Routing provisional; Released/InProgress locked operation snapshots.
* [x] Working-time remaining cho in-progress, strict horizon và typed unscheduled reasons.
* [x] Projected completion/delivery và planned Work Center capacity load, không gọi là OEE.
* [x] Angular Planning workspace, 7/14/30 refresh, capacity/order tables, warnings và Gantt read-only.
* [x] Hai AI read tools planning; không có schedule/capacity/Machine mutation hoặc guarantee.
* [x] Unit, PostgreSQL integration, AI eval và Angular coverage cho boundary chính.

