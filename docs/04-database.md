# Database của chúng ta có một mục tiêu duy nhất

> **Cung cấp dữ liệu cho AI.**

Nghĩa là mỗi bảng phải trả lời được câu hỏi:

> **AI có cần bảng này không?**

Nếu câu trả lời là **không** → không tạo.

---

# 04 - Database Design

Mình chia thành **8 chương**.

---

# 1. Design Principles

Các nguyên tắc:

### Không xây ERP

Chỉ lưu dữ liệu cần thiết.

---

### Không tối ưu quá sớm

Không cần partition.

Không cần sharding.

---

### PostgreSQL First

Chỉ dùng PostgreSQL.

Tận dụng:

* JSONB
* pgvector
* Full Text Search

---

# 2. Core Entities

Đây là lúc chốt Entity.

Theo mình chỉ cần **13 bảng**.

---

## Identity

```text
Company

User
```

---

## Manufacturing

```text
Machine

Material

Product

ProductionOrder

Inventory
```

---

## Knowledge

```text
Document

DocumentChunk
```

---

## AI

```text
Conversation

Message
```

---

## System

```text
Setting

AuditLog

EmbeddingJob
```

Done.

---

# 3. Entity Relationship

Ví dụ.

```text
Company

│

├── User

├── Machine

├── Material

├── Product

├── Inventory

├── ProductionOrder

├── Document

└── Conversation
```

Rất đơn giản.

---

# 4. Table Design

Ví dụ.

## Machine

```text
Id

CompanyId

Code

Name

Status

CreatedAt
```

---

## Material

```text
Id

CompanyId

Code

Name

Unit

CreatedAt
```

---

## Inventory

```text
MaterialId

Warehouse

Quantity
```

Không cần bảng Warehouse riêng trong MVP nếu mỗi doanh nghiệp chỉ có một kho hoặc số kho rất ít. Nếu sau này phát sinh nhiều kho thì mới tách.

---

## Product

```text
Code

Name
```

---

## ProductionOrder

```text
Number

Product

Quantity

Status
```

---

## Document

```text
Id

CompanyId

UploadedByUserId

Title

FileName

ContentType

Path

Size

Status

PageCount

ChunkCount

ProcessingError

ProcessedAt

CreatedAt
```

---

## DocumentChunk

```text
Id

DocumentId

CompanyId

Sequence

PageNumber

Content

CreatedAt
```

`DocumentChunk` stores extracted text only. Vector data belongs to the later `DocumentEmbedding` table so a document can be re-embedded without rewriting its source chunks.

---

## Conversation

```text
Id

CompanyId

UserId

Title

CreatedAt

UpdatedAt
```

---

## Message

```text
Id

ConversationId

Role

Content

CreatedAt
```

Chat queries must filter conversations by both `CompanyId` and `UserId`. A message is accessible only through a conversation owned by that company and user.

---

# 5. Index

Chỉ index những gì thật sự dùng.

Ví dụ.

```text
Machine.Code

Material.Code

Document.Title

ProductionOrder.Number
```

Vector index.

```text
Embedding
```

Done.

---

# 6. Data Flow

Ví dụ.

Upload PDF.

```text
PDF

↓

Document

↓

Chunk

↓

Embedding

↓

Vector
```

---

Chat.

```text
Question

↓

Conversation

↓

Message

↓

Answer
```

---

# 7. Migration

Chỉ dùng EF Core Migration.

Không viết SQL Script thủ công.

---

# 8. Seed Data

Chỉ seed:

```text
Admin

Company Demo

Machine Demo

Material Demo

Inventory Demo
```

Đủ để demo.

---

# 🚨 Nhưng mình muốn thay đổi một quyết định rất quan trọng

## Không lưu Embedding trong bảng `DocumentChunk`.

Lúc đầu mình ghi:

```text
DocumentChunk

Embedding
```

Nhưng sau khi nghĩ kỹ, mình thấy nên tách.

Thành:

```text
Document
```

```text
DocumentChunk
```

```text
DocumentEmbedding
```

### Vì sao?

Một `DocumentChunk` là **nội dung**.

Embedding là **một cách biểu diễn nội dung**.

Sau này nếu:

* đổi model embedding (OpenAI → BGE),
* lưu nhiều phiên bản embedding,
* re-index,

thì không phải sửa bảng `DocumentChunk`.

Đây là nguyên tắc **tách nội dung khỏi chỉ mục tìm kiếm**.

---

# 📊 Chúng ta còn một quyết định lớn nữa

Hiện tại Database chỉ mới là **ý tưởng**.

Tài liệu này sẽ **chưa có ERD chi tiết**.

ERD sẽ được vẽ khi bắt đầu Sprint Backend.

Lý do:

* Sau khi viết Backend mới thấy quan hệ nào thực sự cần.
* Tránh over-design.
* Giữ MVP linh hoạt.

Theo mình, ở giai đoạn hiện tại, **Database Design chỉ nên chốt Entity và nguyên tắc thiết kế**, còn chi tiết cột, khóa ngoại và migration sẽ được hoàn thiện song song khi code. Điều này giúp tài liệu luôn phản ánh đúng hệ thống thay vì trở thành một bản thiết kế cũ không còn khớp với mã nguồn.
