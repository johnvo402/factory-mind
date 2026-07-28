Giờ mình sẽ **cắt giảm Backend tối đa**.

Đây là sai lầm mình thấy rất nhiều dev mắc phải:

> Chưa có khách hàng nhưng đã xây kiến trúc như hệ thống 10 triệu user.

FactoryMind **không cần** như vậy.

---

# 📄 06 - Backend Architecture

Mục tiêu của tài liệu này chỉ có một câu:

> **Làm sao để code dễ đọc, dễ sửa và có thể phát triển thêm sau này.**

Không phải để khoe kiến trúc.

---

# 1. Technology Stack

Chốt luôn, không thay đổi trong MVP.

| Thành phần     | Công nghệ             |
| -------------- | --------------------- |
| Framework      | ASP.NET Core (.NET 9) |
| ORM            | Entity Framework Core |
| Database       | PostgreSQL            |
| Vector Search  | pgvector              |
| Cache          | Redis                 |
| Storage        | MinIO                 |
| Background Job | Hangfire              |
| Authentication | JWT                   |
| AI             | OpenAI Compatible API |
| Logging        | Serilog               |

Không RabbitMQ.

Không Kafka.

Không Elasticsearch.

Không Kubernetes.

---

# 2. Solution Structure

Chỉ có **6 project**.

```text
FactoryMind.sln

src/

FactoryMind.Api

FactoryMind.Application

FactoryMind.Domain

FactoryMind.Infrastructure

FactoryMind.Shared

tests/

FactoryMind.Tests
```

Done.

---

# 3. Dependency Rule

```text
Api
↓

Application
↓

Domain

Infrastructure
```

Domain không được reference bất kỳ project nào.

Application không biết PostgreSQL.

Api không viết business.

Đủ.

---

# 4. Request Flow

Đây là flow duy nhất.

```text
Client

↓

Controller

↓

Application Service

↓

Repository

↓

PostgreSQL

↓

Application

↓

Response
```

Không MediatR.

Không CQRS.

Không Event Bus.

Không Command.

Không Query.

---

### Vì sao bỏ CQRS?

MVP chỉ có khoảng 20 API.

CQRS chỉ làm code dài hơn.

Sau này nếu có 500 API thì tính tiếp.

---

# 5. Module Structure

Ví dụ Machine.

```text
Machine

MachineController

MachineService

MachineRepository

MachineEntity

MachineDto
```

Module nào cũng giống nhau.

Rất dễ tìm.

---

# 6. AI Flow

Đây là phần duy nhất đặc biệt.

```text
ChatController

↓

ChatService

↓

IntentService

↓

RetrievalService

↓

PromptService

↓

LlmService

↓

Response
```

Không Agent.

Không Planner.

---

# 7. Background Jobs

Chỉ có 3 Job.

```text
Embedding Job

Document Parsing

Cleanup
```

Không Scheduler phức tạp.

---

# 8. Configuration

Chỉ có:

```text
appsettings.json

appsettings.Development.json

.env
```

Không 20 file config.

---

# 9. Error Handling

Một format duy nhất.

```json
{
  "success": false,
  "message": "...",
  "errors": []
}
```

Không exception lộn xộn.

---

# 10. Logging

Chỉ log những thứ quan trọng.

* Login
* Upload
* AI Request
* Error

Không log mọi thứ.

---

# 11. Authentication

JWT.

Refresh Token.

Done.

Không OAuth.

Không IdentityServer.

---

# 12. File Storage

PDF.

↓

MinIO.

↓

Database chỉ lưu path.

---

# 13. Testing

Chỉ viết:

* Unit Test cho AI service.
* Integration Test cho API quan trọng.

Không cố 100% coverage.

---

# 🚨 Điều mình muốn thay đổi lớn nhất

Lúc đầu chúng ta dự định dùng **Clean Architecture đầy đủ**.

Sau khi xem lại MVP, mình nghĩ nên dùng:

## Clean Architecture Lite

Ví dụ:

```text
Machine/

MachineController.cs

MachineService.cs

MachineRepository.cs

Machine.cs

MachineDto.cs
```

Thay vì:

```text
Features/

Machine/

Commands/

Queries/

Handlers/

Validators/

Responses/

Requests/

Profiles/

Events/

...
```

Mỗi module có vài chục file.

Điều đó **không phù hợp với một người phát triển**.

