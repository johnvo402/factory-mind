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
