# 📄 06 - Frontend Design

Đây **không phải** là tài liệu về CSS hay Tailwind.

Đây là tài liệu trả lời:

> Người dùng sẽ sử dụng FactoryMind như thế nào?

---

# 1. Design Principles

Chỉ có 5 nguyên tắc.

### 1. AI First

Chat luôn là trung tâm.

---

### 2. Simple

Không quá 2 lần click để đến bất kỳ chức năng nào.

---

### 3. Read First

80% thời gian người dùng chỉ xem.

20% mới chỉnh sửa.

---

### 4. Adaptive

Desktop và laptop là trải nghiệm chính. Mobile vẫn phải điều hướng được, đọc được và thao tác được với các luồng cốt lõi.

---

### 5. Fast

Không có loading dài.

Streaming khi AI trả lời.

---

# 2. Navigation

Chỉ có 6 menu.

```text
🏠 Dashboard

💬 Chat

📚 Knowledge

📦 Data

⚙ Settings

👤 Profile
```

Không có Inventory.

Không có Machine.

Không có Material.

Những thứ đó nằm trong Data.

---

# 3. Screens

MVP chỉ có **8 màn hình**.

## Login

Login dùng một form rõ ràng, không điền sẵn mật khẩu ngoài môi trường Development. Input có label, trạng thái focus, validation và nút hiện/ẩn mật khẩu.

---

## Dashboard

Hiển thị:

```text
Orders

Inventory

Machine

Knowledge

Recent Chat
```

---

## Chat ⭐

Đây là màn hình quan trọng nhất.

```
------------------------------------

FactoryMind

------------------------------------

Hỏi bất cứ điều gì...

________________________

------------------------------------

Chat History

------------------------------------

AI Response

------------------------------------
```

Chat chiếm khoảng 70% diện tích màn hình.

The implemented chat workspace keeps transport, state, and rendering separate. `ChatApiService` owns REST and authenticated POST streaming, `ChatStore` owns conversations and optimistic stream state, and standalone components render the sidebar, messages, Markdown, and citation evidence. Native `fetch` is used for the SSE response because the endpoint requires a JSON POST body and bearer token; one retry is allowed after refreshing an expired access token.

Assistant Markdown is compiled with `marked` and bound as an untrusted string through Angular `[innerHTML]`, allowing Angular's HTML sanitizer to remain active. The frontend never calls a `bypassSecurityTrust...` API for model output.

---

## Knowledge

```
Upload

↓

Documents

↓

Search
```

---

## Data

Tabs:

```text
Machine

Material

Inventory

Product

Production Order
```

Mỗi tab chỉ có:

* Danh sách
* Thêm
* Sửa
* Xóa

---

## Settings

```text
Company

AI

Users
```

---

## Profile

Thông tin cá nhân.

---

## Not Found

404.

Done.

---

# 4. Component

Chỉ định nghĩa những component sẽ tái sử dụng.

Ví dụ:

```text
Button

Input

Table

Dialog

Toast

Chat Bubble

Markdown Viewer

Citation Card
```

---

# 5. Layout

```
+--------------------------------------+

Sidebar

-------------------

Dashboard

Chat

Knowledge

Data

Settings

-------------------

Main Content

+--------------------------------------+
```

Sidebar cố định trên desktop. Ở màn hình nhỏ, sidebar trở thành top bar và navigation chính chuyển xuống bottom navigation tối đa 4 mục. Không được ẩn đường dẫn tới một workspace mà không cung cấp cách điều hướng thay thế.

Mỗi workspace có URL riêng để reload, deep link và browser Back hoạt động:

```text
/chat
/knowledge
/data/machines
/data/materials
/data/inventories
/data/products
/data/production-orders
/settings
```

Nội dung desktop dùng container nhất quán; Chat giới hạn chiều rộng đọc, các màn hình dữ liệu tận dụng chiều rộng còn lại cho table.

---

# 5.1 Visual System

FactoryMind dùng hướng **Industrial AI Cockpit**: tối giản, tin cậy, ưu tiên khả năng đọc và dữ liệu vận hành.

* Màu nền, surface, text, border và trạng thái phải đi qua semantic CSS variables.
* Emerald là màu thương hiệu và primary action; blue chỉ dùng cho AI evidence/citation.
* Spacing theo nhịp 4/8px; radius chỉ dùng các mức 8/12/16px.
* Body 16px, table/compact UI 14px, metadata không nhỏ hơn 12px.
* Icon là SVG từ một visual language thống nhất, không dùng emoji hoặc ký tự font làm structural icon.
* Control tương tác cao tối thiểu 40px trên desktop và 44px trên mobile.
* Mọi control có `:focus-visible`; motion tôn trọng `prefers-reduced-motion`.
* Dialog/sheet phải trap focus, đóng bằng Escape và trả focus về trigger.

---

# 6. AI Response

Response luôn theo format.

```
Answer

-------------------

Evidence

-------------------

Source
```

Ví dụ:

```
Kho hiện còn 1.200 kg PP.

-------------------

Tồn kho thực tế: 1.200 kg

Đã đặt trước: 300 kg

Khả dụng: 900 kg

-------------------

Nguồn:

Inventory
```

---

# 7. Empty State

Đây là phần nhiều dự án quên.

Ví dụ.

Knowledge.

```
Chưa có tài liệu.

↓

Upload tài liệu đầu tiên.
```

---

Chat.

```
Bạn muốn hỏi điều gì?
```

---

Data.

```
Chưa có dữ liệu.

↓

Import Excel.
```

---

# 8. Error State

Ví dụ.

```
AI không thể trả lời.

↓

Thử lại.
```

Hoặc.

```
Không tìm thấy dữ liệu.
```

---

# 9. Responsive

MVP ưu tiên Desktop/Laptop nhưng hỗ trợ đầy đủ navigation và luồng đọc/ghi cơ bản trên Tablet/Mobile.

Các breakpoint kiểm thử bắt buộc:

* 375px: điện thoại nhỏ.
* 768px: tablet.
* 1024px: laptop nhỏ.
* 1440px: desktop.

Table có thể cuộn ngang trong vùng riêng; page không được tạo horizontal scroll. Fixed composer và bottom navigation phải chừa đúng content inset.

---

# 🎨 Tech Stack

* Angular 20

* Signals

* RxJS

* Angular Router

* SCSS + semantic CSS variables

Đây là toàn bộ stack.

---

# Authentication session

The access token and user profile live only in an Angular in-memory auth store. The refresh token is an `HttpOnly` cookie and is never read by JavaScript. Application bootstrap restores the session through `POST /api/auth/refresh`; the HTTP interceptor performs one shared refresh on 401 and retries the failed request once.

Development uses the Angular `/api` proxy. Production should serve the frontend and API from the same site.

---

# 🚨 Một thay đổi rất lớn

Lúc đầu mình định có menu **Dashboard**.

Sau khi suy nghĩ lại...

## Mình muốn bỏ Dashboard làm trang chủ.

Trang chủ sẽ là **Chat**.

Giống ChatGPT.

Khi người dùng mở FactoryMind:

```
Xin chào 👋

Hôm nay bạn muốn biết điều gì?

____________________________
```

Bên dưới là:

* 4 KPI nhỏ (Orders, Inventory, Machines, Alerts).
* Các cuộc trò chuyện gần đây.

The implemented chat home loads these KPI values from the tenant-scoped dashboard summary endpoint. KPI failure is isolated from chat: the user can still start a conversation and retry the dashboard without reloading the workspace.

The Data workspace includes an Excel import wizard for Machine, Material, Product, Inventory, and Production Order. It previews the first rows, proposes a header mapping, requires confirmation, and renders row-level validation errors without partially importing the workbook.

The Inventory workspace is a warehouse ledger view rather than balance CRUD. It lists current material/warehouse quantities and last update time, provides Receive, Issue, Adjust, and Transfer forms, manages active/deactivated warehouses, and opens a paged transaction history. Positive and negative changes use both a sign and accessible color treatment. Forms keep visible labels, inline validation, focus indicators, and disabled/loading feedback consistent with the existing workspace styling.

The Production Order workspace treats status as a business state instead of an editable field. Planned rows expose material preview, edit, Release, and Cancel; Released rows expose the locked BOM revision, material preview, explicit multi-warehouse allocation, Start, and Cancel; InProgress rows show execution timestamps and remain frozen. Allocation inputs keep per-Material totals visible, use inline validation, and enable Start only when every server-calculated requirement is matched. Server validation remains authoritative.

Knowledge is a first-class workspace for PDF upload, asynchronous processing status, retry after parsing failure, and semantic-search inspection with document, page, excerpt, and score. While documents are uploaded or processing, the list polls quietly without blocking the rest of the UI.

Settings has Company, Users, and AI tabs. Admins can rename the company and manage tenant users. The AI tab shows the active Gemini models, key readiness, and a re-index action; it never accepts, stores, or renders provider credentials in browser state.

Lý do:

* Người dùng mở ứng dụng vì muốn hỏi AI.
* Dashboard chỉ đóng vai trò cung cấp ngữ cảnh nhanh.
* Toàn bộ trải nghiệm xoay quanh cuộc hội thoại.

Điều này cũng giúp sản phẩm khác biệt với ERP truyền thống, vốn luôn bắt đầu bằng một dashboard đầy biểu đồ.

---
