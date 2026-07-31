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

### 4. Desktop First

MVP chỉ tối ưu Desktop.

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

Sidebar cố định.

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

MVP chỉ hỗ trợ:

* Desktop.
* Laptop.

Tablet và Mobile chỉ hiển thị cơ bản.

---

# 🎨 Tech Stack

* Angular 20

* Angular Material

* Tailwind

* Signals

* RxJS

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

Lý do:

* Người dùng mở ứng dụng vì muốn hỏi AI.
* Dashboard chỉ đóng vai trò cung cấp ngữ cảnh nhanh.
* Toàn bộ trải nghiệm xoay quanh cuộc hội thoại.

Điều này cũng giúp sản phẩm khác biệt với ERP truyền thống, vốn luôn bắt đầu bằng một dashboard đầy biểu đồ.

---
