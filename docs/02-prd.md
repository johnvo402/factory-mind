# 02 - PRD

## FactoryMind Core

Version 1.0

---

# 1. Product Overview

## Mục tiêu

FactoryMind giúp doanh nghiệp sản xuất **trò chuyện với dữ liệu và tài liệu nội bộ bằng AI**.

Người dùng không cần biết dữ liệu đang nằm trong Excel, PostgreSQL hay PDF.

Chỉ cần đặt câu hỏi bằng ngôn ngữ tự nhiên.

---

## Người dùng chính

### Manager

Muốn biết:

* Đơn hàng
* Tồn kho
* Tiến độ

---

### Production Leader

Muốn biết:

* Máy
* Lệnh sản xuất
* SOP

---

### Warehouse

Muốn biết:

* Tồn kho
* Nguyên liệu

---

### Director

Muốn Dashboard tổng quan.

---

# 2. Jobs To Be Done

Đây là phần quan trọng nhất.

---

## Job 1

### Tôi muốn hỏi AI về doanh nghiệp.

Ví dụ.

```text
Kho còn bao nhiêu PP?
```

```text
Đơn hàng nào đang trễ?
```

```text
Máy nào đang rảnh?
```

---

### Expected Result

AI trả lời trong vòng vài giây.

Có nguồn.

Có dữ liệu.

---

## Job 2

### Tôi muốn tìm SOP nhanh.

Ví dụ.

```text
Reset máy HA250
```

↓

AI mở đúng tài liệu.

↓

Trả lời.

---

## Job 3

### Tôi muốn upload tài liệu.

Ví dụ.

```text
Manual

ISO

SOP

QC
```

↓

AI hiểu.

↓

Có thể search.

---

## Job 4

### Tôi muốn quản lý dữ liệu cơ bản.

Ví dụ.

```text
Machine

Material

Inventory

Product

Production Order
```

CRUD.

---

## Job 5

### Tôi muốn xem tình hình sản xuất.

Dashboard.

```text
Inventory

Machine

Order

Alert
```

---

# 3. Functional Requirements

Bây giờ mới tới chức năng.

---

## FR-001

Authentication

---

User có thể

* Login
* Logout

---

Acceptance

```text
✔ JWT

✔ Refresh Token
```

---

## FR-002

AI Chat

User nhập.

```text
Máy nào rảnh?
```

↓

AI.

↓

Response.

Acceptance.

```text
✔ Streaming

✔ Citation

✔ Markdown
```

---

## FR-003

Knowledge

Upload.

```text
PDF

DOCX

XLSX
```

↓

Embedding.

↓

Search.

---

## FR-004

Business Data

CRUD.

```text
Machine

Material

Inventory

Product

Production Order
```

---

## FR-005

Dashboard

Widget.

```text
Order

Inventory

Machine

Alert
```

---

## FR-006

Settings

```text
Company

User

AI
```

---

# 4. User Flow

## AI Chat

```text
Login

↓

Dashboard

↓

Chat

↓

Question

↓

AI

↓

Answer
```

---

## Upload

```text
Upload

↓

Processing

↓

Embedding

↓

Done
```

---

## CRUD

```text
List

↓

Create

↓

Update

↓

Delete
```

---

# 5. Permission

## Admin

Toàn quyền.

---

## Manager

Chat.

Dashboard.

CRUD.

---

## User

Chat.

Knowledge.

Dashboard.

Không sửa cấu hình.

---

# 6. Non Functional

Response.

```text
<5s
```

Upload.

```text
100MB
```

Availability.

```text
99%
```

Browser.

```text
Chrome

Edge
```

---

# 7. MVP Acceptance

MVP được coi là hoàn thành khi:

* Người dùng đăng nhập được.
* Upload được tài liệu.
* AI trả lời được tài liệu.
* AI trả lời được dữ liệu doanh nghiệp.
* CRUD dữ liệu hoạt động.
* Dashboard hiển thị đúng dữ liệu.
* Có thể deploy trên một VPS.

---

# 📌 Mình muốn bổ sung một phần mà nhiều PRD bỏ qua

## **Business Assumptions**

Đây là các giả định của MVP.

Ví dụ:

* Khách hàng đã có dữ liệu (Excel hoặc ERP).
* Khách hàng sẵn sàng upload tài liệu PDF/Word.
* Khách hàng không cần AI tự ra quyết định, chỉ cần gợi ý.
* Dữ liệu không cần đồng bộ thời gian thực.
* Mỗi doanh nghiệp có quy mô nhỏ (10–100 người dùng).

Nếu một trong các giả định này sai khi gặp khách hàng, thì **không phải sửa code ngay**, mà phải xem lại PRD và phạm vi MVP.

---
