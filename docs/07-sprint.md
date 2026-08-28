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

Implementation status: Hybrid RAG routes `Business`, `Knowledge`, and `Hybrid` questions, merges bounded tenant-scoped context, and returns separately rendered SQL evidence and PDF sources.

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

Implementation status: all five business entity types support bounded preview, mapping, row validation, and transactional `.xlsx` import.

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

Implementation status: Knowledge upload/search/status UI, role-aware navigation, retryable errors, loading/empty states, and production bundle budgets are implemented.

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

Implementation status: production API/frontend images, internal PostgreSQL/MinIO topology, health checks, required secrets, and GHCR image delivery are implemented. VPS/TLS rollout remains environment-specific and requires an approved target, credentials, backup, and rollback procedure. Redis remains deferred because no running cache or session use case requires it.

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

