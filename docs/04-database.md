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

UpdatedAt
```

`Machine.Code` is normalized to uppercase and unique per company. MVP statuses are `available`, `running`, `maintenance`, and `offline`.

---

## Material

```text
Id

CompanyId

Code

Name

Unit

CreatedAt

UpdatedAt
```

`Material.Code` is normalized to uppercase and unique per company. `Unit` remains a required short string for the MVP instead of introducing a separate unit-of-measure table.

---

## Warehouse inventory ledger

```text
Warehouse(Id, CompanyId, Code, Name, Description?, IsActive, CreatedAt, UpdatedAt)

InventoryTransaction(Id, CompanyId, WarehouseId, MaterialId, Type, Quantity,
                     ReferenceType?, ReferenceId?, Note?, CreatedByUserId?, CreatedAt)

InventoryBalance(Id, CompanyId, WarehouseId, MaterialId, Quantity, UpdatedAt)
```

`Warehouse.Code` is unique inside a company. `InventoryTransaction` is the immutable source of stock history and stores a positive `numeric(18,3)` quantity; its strongly typed operation determines whether the signed change is positive or negative. `InventoryBalance` is a materialized current value, unique for `(CompanyId, WarehouseId, MaterialId)`, with a database check preventing negative quantities.

Ledger insertion and balance mutation commit in one database transaction. Transfers write correlated `TransferOut` and `TransferIn` rows and update both balances atomically. Foreign keys to warehouses and materials are restrictive so historical records cannot be orphaned; deleting a warehouse means deactivation.

---

## Product

```text
Id

CompanyId

Code

Name

CreatedAt

UpdatedAt
```

`Product.Code` is normalized to uppercase and unique per company.

---

## ProductionOrder

```text
Id

CompanyId

Number

ProductId

Quantity

Status

CreatedAt

UpdatedAt
```

`ProductionOrder.Number` is normalized to uppercase and unique per company. `Quantity` uses `numeric(18,3)` and must be greater than zero. Product deletion is restricted while an order references it. MVP statuses are `planned`, `in_progress`, `completed`, and `cancelled`.

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

## DocumentEmbedding

```text
Id

DocumentChunkId

CompanyId

Model

Dimensions

Embedding vector(1536)

CreatedAt
```

Each chunk has one current embedding in the MVP. Re-indexing atomically replaces the document's chunks and embeddings. Exact cosine search is used before introducing an approximate vector index.

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

## MessageCitation

```text
Id

MessageId

ReferenceNumber

DocumentId

ChunkId

DocumentTitle

FileName

PageNumber

Excerpt

Score

CreatedAt
```

`MessageCitation` is an immutable source snapshot owned by an assistant message. It intentionally does not reference the live document with a foreign key, so historical answers retain their evidence if a source is later renamed or removed.

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

Development chỉ seed:

```text
1 Company demo
3 Users: Admin, Manager, Operator
6 Machines với nhiều trạng thái vận hành
5 Materials
4 Products
6 Inventory balances tại nhiều kho
5 Production Orders với nhiều trạng thái
```

Seed Development chạy idempotent theo email hoặc mã nghiệp vụ. Database local hiện có sẽ được bổ sung bản ghi còn thiếu khi API khởi động, không cần xóa volume.

Production không seed demo business data hoặc demo password. Khi database trống, production chỉ tạo Company và Admin từ các biến `BootstrapAdmin__*`; startup từ chối cấu hình thiếu hoặc password ngắn hơn 12 ký tự.

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

* đổi model embedding,
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
