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

`document_chunks` has an expression GIN index over
`to_tsvector('simple', coalesce("Content", ''))`. The `simple` configuration preserves multilingual
terms without assuming English stemming. Hybrid retrieval issues one bounded pgvector candidate
query and one bounded lexical candidate query; both retain Company and Ready-document boundaries.

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

WorkCenterId?

CreatedAt

UpdatedAt
```

`Machine.Code` is normalized to uppercase and unique per company. A Machine optionally belongs to one same-tenant active Work Center; nullable `WorkCenterId` preserves legacy rows without fabricated assignments. MVP statuses are `available`, `running`, `maintenance`, and `offline`, but `running` is system-managed by ProductionOrderOperation Start/Complete and cannot be set through administrative create/update requests.

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

`Warehouse.Code` is unique inside a company. `InventoryTransaction` is the immutable source of stock history and stores a positive `numeric(18,6)` quantity; its strongly typed operation determines whether the signed change is positive or negative. `InventoryBalance` is a materialized current value, unique for `(CompanyId, WarehouseId, MaterialId)`, with a database check preventing negative quantities. Six-decimal stock precision matches BOM requirement rounding during production consumption.

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

BillOfMaterialId?

Quantity

Status

ReleasedAt?

StartedAt?

CancelledAt?

CreatedAt

UpdatedAt
```

`ProductionOrder.Number` is normalized to uppercase and unique per company. `Quantity` uses `numeric(18,3)` and must be greater than zero. Product deletion is restricted while an order references it. Statuses are `planned`, `released`, `in_progress`, legacy-readable `completed`, and `cancelled`.

New orders begin Planned. Release stores the exact active `BillOfMaterialId` and `ReleasedAt`; Start records `StartedAt` only after every explicitly allocated raw-material decrement and `ProductionConsume` ledger insert succeeds in one transaction. Cancel records `CancelledAt` and is allowed only before consumption. The nullable BOM reference and timestamps preserve existing rows without inventing history, and the restrictive BOM foreign key prevents deletion of a referenced revision.

Release also locks the active Routing revision and creates immutable ProductionOrderOperation snapshots. Each operation stores its required Work Center snapshot and later records nullable `MachineId`, `MachineCode`, and `MachineName` when an Available Machine from that Work Center is explicitly selected at Start. Machine claim plus operation Start commit atomically; operation Complete plus Machine release commit atomically. Filtered unique indexes enforce at most one InProgress operation per Production Order and per Machine. Legacy operation rows keep null Machine fields and legacy InProgress operations may Complete without a Machine release.

Machine-to-Work-Center and operation-to-Machine foreign keys are restrictive. Once an operation references a Machine, that Machine is manufacturing history and cannot be hard deleted.

---

## Bill of Materials

```text
BillOfMaterial(Id, CompanyId, ProductId, Revision, OutputQuantity, Status,
               CreatedAt, UpdatedAt)

BomItem(Id, BillOfMaterialId, MaterialId, Quantity, ScrapPercentage?,
        CreatedAt, UpdatedAt)
```

A Product may have many BOM revisions but at most one `active` revision per Company. Revisions use `draft`, `active`, and `archived`; there is no physical-delete API. `OutputQuantity` and item `Quantity` use `numeric(18,6)` and must be positive. Optional scrap is limited to 0–100 percent. `(CompanyId, ProductId, Revision)` and `(BillOfMaterialId, MaterialId)` are unique, while a filtered unique index protects the single active revision invariant. Product and Material deletes are restrictive so BOM history is retained.

Material-requirement planning reads the active BOM for a Planned order and the locked BOM for Released/InProgress execution, then sums current `InventoryBalance` quantities for each Material across all warehouses in the same Company. The query does not write balances or ledger transactions. Release also writes no ledger entry and does not reserve stock.

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

Each chunk has one current embedding in the MVP. Re-indexing atomically replaces the document's
chunks and embeddings. Exact cosine search remains in use and is fused with indexed PostgreSQL
full-text ranking through deterministic Reciprocal Rank Fusion. The migration adds only the lexical
index: it does not rewrite chunks, regenerate vectors, or fabricate document content.

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

## Production Order delivery planning

`production_orders` có `DueDate timestamp with time zone NULL` và `Priority varchar(30) NOT NULL
DEFAULT 'normal'`. Check constraint giới hạn priority ở `low|normal|high|urgent`. Migration không suy
diễn hạn giao lịch sử: bản ghi cũ nhận `DueDate = NULL`, `Priority = normal`. UI date-only chuẩn hóa
ngày chọn thành 23:59:59.999 UTC, vì deadline hiện là ngày giao kinh doanh nhưng schema/API hiện dùng
UTC `DateTime` nhất quán.

Các index `(CompanyId, Status, DueDate)` và `(CompanyId, Priority)` phục vụ aggregate/filter theo
tenant. Delivery status và days-to-due không lưu DB; chúng được tính từ DueDate, lifecycle state,
CompletedAt và clock server. Chi tiết quyết định nằm ở decision log 0041.

## Work Center capacity planning schema

Migration `AddWorkCenterCapacityPlanning` thêm `companies.TimeZoneId varchar(100) NOT NULL DEFAULT
'UTC'`, `work_centers.ParallelCapacity integer NOT NULL DEFAULT 1`, `work_center_shifts` và
`work_center_days_off`. Dữ liệu cũ không nhận ca giả; Work Center cũ trả `calendar_missing` tới khi
được cấu hình.

Shift lưu `DayOfWeek` 0–6 và `time without time zone`; ngày/giờ chỉ có nghĩa khi kết hợp Company IANA
timezone. Check constraints bảo vệ day range, start trước end và capacity 1–100. Ngày nghỉ có unique
index `(CompanyId, WorkCenterId, Date)`; overlap ca được validate ở application trước transaction
replace. Không có bảng schedule/lane vì preview không persist.
