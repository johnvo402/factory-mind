# 1. AI Overview

Mục tiêu của AI.

FactoryMind AI chỉ làm **3 việc**.

```text
1. Trả lời dữ liệu doanh nghiệp

2. Trả lời tài liệu

3. Đưa ra gợi ý đơn giản
```

Không làm:

* Viết email
* Sinh code
* Dịch thuật
* AI Agent
* Auto Workflow

=> AI chỉ phục vụ sản xuất.

---

# 2. AI Flow

Đây là flow duy nhất của AI.

```text
User

↓

Question

↓

Intent Detection

↓

Retrieve Context

↓

LLM

↓

Response
```

Rất đơn giản.

---

# 3. Intent Detection

AI phải biết người dùng đang hỏi gì.

Chỉ có **3 loại Intent**.

---

## Intent 1

Business Data

Ví dụ

```text
Kho còn bao nhiêu?

Máy nào rảnh?

Đơn hàng nào trễ?
```

↓

SQL

---

## Intent 2

Knowledge

Ví dụ

```text
SOP

Manual

ISO

QC
```

↓

Vector Search

---

## Intent 3

Hybrid

Ví dụ

```text
Có nên nhận đơn hàng này?
```

↓

SQL

*

Vector

↓

Merge

---

Chỉ vậy.

Không cần 50 intent.

---

# 4. Retrieval

Đây là RAG.

Chỉ có hai nguồn dữ liệu.

---

## Business

```text
PostgreSQL
```

---

## Knowledge

```text
pgvector
```

---

Không Elastic.

Không Neo4j.

Không Qdrant.

MVP dùng PostgreSQL + pgvector là đủ.

---

Sprint 3 exposes tenant-scoped semantic knowledge search through `POST /api/knowledge/search`. The query is embedded with the same configured model used for document chunks, then PostgreSQL returns the nearest ready chunks by exact cosine distance. Results include document, page, chunk content, and similarity score for the later citation step.

Chat now uses the same retrieval path to build a compact knowledge context. The five nearest chunks are labeled `[S1]` through `[S5]`, capped at 8,000 characters, and placed in a system message before the current question. The model must cite supporting labels inline and say that it does not know when the supplied context is insufficient. Only labels present in the completed answer become persisted citations.

Sprint 5 routes chat questions to exactly one of `Business`, `Knowledge`, or `Hybrid`. The deterministic router normalizes Vietnamese text and uses documented manufacturing keywords; an ambiguous question falls back to `Hybrid` so classification does not require another LLM call. Business retrieval reads only small tenant-scoped projections from PostgreSQL, while Knowledge retrieval continues to use pgvector.

Step 9 adds a bounded two-phase path for live manufacturing questions. Business intent and explicit
manufacturing Hybrid intent may make one non-streaming native Gemini function-calling request. The
At the Step 9 milestone, the planner could select zero to three calls from seven registered read-only tools; it never writes the final
answer. The server rejects unknown names, unexpected properties (including model-supplied tenant
identity), malformed values, invalid enums and unbounded limits, then injects authenticated
`CompanyId` into typed EF Core queries.

Tool results become priority `BusinessDataRecord` values, dedupe with normal Business RAG, and retain
the existing `[B#]` persistence/rendering path. Knowledge RAG still contributes `[S#]`. The second
phase is the existing Gemini stream with no tool definitions, so function calls cannot recurse or
appear during SSE output. Planner failure or a zero-call plan falls back to the pre-Step-9 RAG path.

The approved tools are:

* `get_production_order_status`
* `get_machine_status`
* `list_machines`
* `get_work_center_status`
* `get_material_inventory`
* `get_production_order_material_readiness`
* `list_production_orders`

Step 12B extends that bounded registry to nine read-only tools with
`get_production_order_schedule_preview` and `get_work_center_capacity_preview`. Both require an exact
tenant-scoped identifier, default to a 14-day horizon, cap AI requests at 30 days, and report a
deterministic planning preview rather than a guaranteed commitment, OEE measurement, or actual
utilization. They cannot persist schedules, change priorities, assign Machines, or reserve inventory.

There is no SQL/query tool and no mutation tool. Material readiness is a current, standalone stock
comparison for one Planned/Released order, not a reservation or scheduling guarantee; it is explicitly
not applicable once the order is InProgress, Completed or Cancelled.

Business records are labeled `[B1]`, `[B2]`, and so on. Hybrid context merges these records with `[S#]` document sources under one system instruction. Only labels referenced by the final answer are returned and persisted as immutable evidence snapshots.

---

# 5. Context Builder

Đây là phần mình thích nhất.

Ví dụ.

User hỏi.

```text
Có đủ nguyên liệu không?
```

AI không gửi cả database.

Mà tạo.

```json
{
  "Material":"PP",
  "Stock":1200,
  "Required":800
}
```

Rất nhỏ.

↓

LLM.

---

Nếu hỏi SOP.

```json
{
 "Document":"SOP Injection Machine",
 "Content":"..."
}
```

↓

LLM.

---

# 6. Prompt

Prompt cũng rất ngắn.

```text
Bạn là FactoryMind AI.

Bạn chỉ được trả lời dựa trên context.

Nếu không có dữ liệu.

Hãy nói không biết.

Không được bịa.
```

Done.

---

# 7. Response

Response luôn theo format.

```text
Answer

---------

Evidence

---------

Source
```

Ví dụ.

```text
Có đủ nguyên liệu.

--------

1200kg

Cần

800kg

--------

Nguồn:

Inventory
```

Không nói lan man.

---

# 8. Limitation

AI KHÔNG

* đoán
* suy diễn nếu thiếu dữ liệu
* trả lời ngoài lĩnh vực sản xuất
* tự thay đổi dữ liệu
* tự đưa quyết định cuối cùng

Luôn ghi rõ khi thiếu thông tin.

## Step 10 — Tool safety và manufacturing decision support

Tool planner chỉ chạy một vòng và chọn tập nhỏ nhất đủ trả lời, tối đa ba tool read-only. Tool result
được đưa vào final generation dưới dạng `[B#]`; output của tool hoặc tài liệu chỉ là untrusted evidence,
không thể kích hoạt thêm planner/tool. Planner timeout, provider error, malformed response hoặc zero-call
plan không làm hỏng Business/Knowledge RAG fallback; caller cancellation vẫn được propagate.

Final generation phải phân biệt fact có citation, cautious inference và unknown. Không được suy ETA từ
Routing runtime, bottleneck từ số máy, nguyên nhân hỏng từ trạng thái maintenance, delay hoặc hiệu suất
khi thiếu schedule/capacity/telemetry/cause evidence.

`tests/FactoryMind.AiToolEval` chạy offline trong CI với 50 case tiếng Việt, tiếng Việt không dấu và
tiếng Anh. Metrics gồm tool selection, exact tool+arguments, no-tool accuracy/precision, argument và
identifier accuracy, unauthorized rejection, bounded-call compliance, precision/recall, duplicate rate
và average calls. Đây là deterministic policy + server-enforcement evaluation, không phải live Gemini
benchmark. Bảy tool hiện tại vẫn read-only; không có tool mutation.

---

# 📌 Kiến trúc AI cuối cùng

```text
                    User
                      │
                      ▼
               Intent Detection
                      │
          ┌───────────┴───────────┐
          ▼                       ▼
    Business Data          Knowledge Base
     (PostgreSQL)            (pgvector)
          │                       │
          └───────────┬───────────┘
                      ▼
               Context Builder
                      │
                      ▼
                    LLM
                      │
                      ▼
                  Response
```

---

# 🚨 Nhưng mình muốn thay đổi một quyết định kỹ thuật

Lúc trước chúng ta nói AI sẽ tự phân loại Intent.

Mình nghĩ **không nên**.

Thay vào đó, dùng **một Intent Router bằng code**.

Ví dụ:

```text
Nếu câu hỏi chứa:

"SOP"
"Manual"

↓

Knowledge

----------------

Nếu chứa

"Kho"

"Tồn"

"Đơn hàng"

↓

Business

----------------

Nếu không chắc

↓

LLM phân loại
```

### Tại sao?

* Nhanh hơn.
* Rẻ hơn (ít token hơn).
* Dễ debug.
* Dễ mở rộng.
* Với tiếng Việt và phạm vi MVP nhỏ, hiệu quả thường đủ tốt.

Nghĩa là **80% câu hỏi sẽ được route bằng code**, chỉ **20% câu hỏi mơ hồ** mới nhờ LLM xác định.

## Step 11 — Controlled release proposal

FactoryMind giữ nguyên đúng 7 read-only tools và thêm một kiến trúc action tách biệt. Gemini chỉ thấy
`propose_release_production_order(number)`, không thấy command release. Deterministic gate chỉ cho explicit
single-order release intent đi vào action planner; câu hỏi, `yes/ok/confirm`, unsupported writes và
multi-order requests vẫn read-only.

Server lấy Company/User từ authentication, kiểm tra Manager, resolve PO/BOM/Routing/Work Centers và lưu
proposal Pending với snapshot product/quantity/state/BOM/Routing. Chat chỉ emit `ai-action-proposal`; không
mutation manufacturing. Confirm button POST đúng proposal ID. Server query theo tenant + creator,
authorize Manager lần hai, kiểm tra expiry/staleness/readiness và gọi canonical
`ReleaseProductionOrderCommand`. Release không start order/operation/machine và không consume inventory.

Trust boundary: Gemini output là untrusted proposal input → strict one-field schema → server validation →
human confirmation → authorization/revalidation again → canonical locked release transaction. Không có
natural-language hoặc autonomous confirmation, generic command tool, background execution hay hidden
reasoning persistence.
