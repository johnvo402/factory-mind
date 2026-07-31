# 01 - Vision

## FactoryMind

**Version:** 1.0

**Status:** Draft

**Last Updated:** July 2026

---

# 1. Executive Summary

FactoryMind là một nền tảng AI Assistant dành cho các doanh nghiệp sản xuất vừa và nhỏ (SME).

Thay vì thay thế ERP hoặc MES, FactoryMind hoạt động như một **AI Copilot**, giúp doanh nghiệp khai thác dữ liệu hiện có để:

* Trả lời câu hỏi bằng ngôn ngữ tự nhiên.
* Tra cứu tài liệu nội bộ.
* Hỗ trợ ra quyết định đơn giản.
* Tổng hợp thông tin từ nhiều nguồn.

Phiên bản đầu tiên tập trung vào việc **giảm thời gian tìm kiếm thông tin** và **hỗ trợ người quản lý đưa ra quyết định nhanh hơn**.

---

# 2. Problem Statement

Hiện nay phần lớn doanh nghiệp sản xuất nhỏ gặp các vấn đề:

### Dữ liệu phân tán

Thông tin nằm ở nhiều nơi:

* Excel
* ERP
* File PDF
* SOP
* Email
* Người quản lý

Không có nơi nào tổng hợp.

---

### Khó tìm thông tin

Ví dụ:

* SOP của máy ở đâu?
* Kho còn đủ nguyên liệu không?
* Đơn hàng nào đang trễ?
* Máy nào đang rảnh?

Muốn trả lời phải hỏi nhiều người.

---

### Phụ thuộc vào kinh nghiệm

Nhiều quyết định dựa trên:

* Trưởng ca
* Quản đốc
* Người làm lâu năm

Nếu họ nghỉ việc, kiến thức cũng mất theo.

---

# 3. Vision

FactoryMind hướng tới việc trở thành **AI Copilot** cho doanh nghiệp sản xuất.

AI không thay thế con người.

AI giúp:

* Tìm thông tin nhanh hơn.
* Hiểu dữ liệu nhanh hơn.
* Đưa ra gợi ý dựa trên dữ liệu.

Người quản lý vẫn là người quyết định cuối cùng.

---

# 4. Product Positioning

FactoryMind **không phải ERP**.

FactoryMind **không phải MES**.

FactoryMind **không phải BI Tool**.

FactoryMind là một lớp AI nằm trên dữ liệu doanh nghiệp.

```text
                 User

                  │

                  ▼

            FactoryMind AI

                  │

     ┌────────────┼────────────┐

     ▼            ▼            ▼

   ERP         Excel       Documents
```

FactoryMind tận dụng dữ liệu hiện có thay vì yêu cầu doanh nghiệp thay đổi toàn bộ hệ thống.

---

# 5. Target Customers

Đối tượng chính:

* Doanh nghiệp sản xuất nhỏ và vừa.
* Quy mô từ 10–100 nhân viên.
* Đang quản lý bằng Excel hoặc ERP đơn giản.
* Chưa có đội ngũ IT lớn.
* Muốn ứng dụng AI với chi phí thấp.

Ngành phù hợp:

* Nhựa.
* Bao bì.
* Nội thất.
* Cơ khí.
* May mặc.
* Thực phẩm.

---

# 6. Core Value Proposition

FactoryMind mang lại bốn giá trị chính.

### AI Chat

Cho phép hỏi dữ liệu bằng tiếng Việt.

Ví dụ:

> "Kho còn bao nhiêu hạt nhựa PP?"

---

### Knowledge Search

Tra cứu:

* SOP
* Manual
* Quy trình
* Hướng dẫn vận hành

---

### Business Search

Tra cứu dữ liệu:

* Kho
* Đơn hàng
* Máy móc
* Sản xuất

---

### AI Suggestion

Đưa ra các gợi ý đơn giản như:

* Có nên nhận đơn hàng?
* Có đủ nguyên liệu không?
* Máy nào phù hợp hơn?

---

# 7. MVP Scope

Phiên bản đầu tiên chỉ tập trung vào các chức năng cốt lõi.

### Authentication

* Đăng nhập.
* Phân quyền cơ bản.

---

### AI Chat

* Hỏi bằng tiếng Việt.
* Trả lời có nguồn tham chiếu.

---

### Knowledge

* Upload PDF.
* Upload Word.
* Upload Excel.
* Tra cứu tài liệu.

---

### Business Data

Quản lý dữ liệu cơ bản:

* Machine
* Material
* Inventory
* Product
* Production Order

---

### Dashboard

Hiển thị:

* Đơn hàng.
* Tồn kho.
* Máy móc.
* Cảnh báo.

---

# 8. Out of Scope

Những chức năng **không** nằm trong MVP.

* IoT.
* Predictive Maintenance.
* Digital Twin.
* Multi-Agent AI.
* Workflow Engine.
* Auto Scheduling.
* Machine Learning Training.
* Mobile Application.
* Multi Factory.
* Real-time Streaming.

Những nội dung này sẽ được xem xét sau khi có khách hàng đầu tiên.

---

# 9. Success Metrics

MVP được xem là thành công khi đạt các mục tiêu sau:

### Product

* Người dùng có thể hỏi AI bằng tiếng Việt.
* AI trả lời đúng trong phần lớn các trường hợp phổ biến.
* Có thể tra cứu tài liệu nội bộ.

---

### Business

* Có ít nhất 3 doanh nghiệp dùng thử.
* Có khách hàng trả phí đầu tiên.
* Thu thập phản hồi để xây dựng phiên bản tiếp theo.

---

### Technical

* Thời gian phản hồi trung bình dưới 5 giây.
* Có khả năng mở rộng thêm module.
* Hệ thống hoạt động ổn định.

---

# 10. Product Roadmap

### Phase 1

FactoryMind Core

* AI Chat
* Knowledge
* Inventory
* Dashboard

---

### Phase 2

ERP Connector

* Đồng bộ dữ liệu từ ERP phổ biến.
* Import dữ liệu tự động.

---

### Phase 3

Decision Support

* Gợi ý nâng cao.
* Phân tích xu hướng.
* Báo cáo thông minh.

---

# 11. Technology Overview

Backend

* ASP.NET Core (.NET 9)

Frontend

* Angular 20
* TypeScript
* Angular Material
* Tailwind CSS
* Angular Signals

Database

* PostgreSQL

Vector Database

* pgvector (trong PostgreSQL)

Cache

* Redis

Storage

* MinIO

LLM

* Google Gemini API.
* `gemini-3.5-flash-lite` cho chat và `gemini-embedding-2` cho semantic retrieval.
* MVP dùng free tier với context nhỏ và quota handling rõ ràng.

---

# 12. Guiding Principles

Trong suốt quá trình phát triển, FactoryMind tuân theo các nguyên tắc sau:

1. **AI hỗ trợ con người, không thay thế con người.**
2. **Ưu tiên đơn giản hơn là đầy đủ.**
3. **Mọi tính năng phải giải quyết một vấn đề thực tế của khách hàng.**
4. **Không phát triển tính năng khi chưa có nhu cầu xác thực từ người dùng.**
5. **Tận dụng dữ liệu hiện có thay vì yêu cầu doanh nghiệp thay đổi quy trình.**
6. **Mỗi Sprint phải tạo ra giá trị có thể demo được.**

---
