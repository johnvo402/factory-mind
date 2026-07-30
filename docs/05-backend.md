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

`FactoryMind.Shared` only contains code that is truly shared by multiple projects or features, such as common result/error contracts, pagination contracts, shared constants and framework-independent helpers. It must not become a dumping ground for feature code, entities or infrastructure implementations.

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

Repository interfaces belong to Application so command/query handlers depend on abstractions. Infrastructure implements those interfaces with EF Core. Repositories are feature-specific (for example, `IMachineRepository`); do not introduce a generic repository that obscures query intent.

---

# 4. Request Flow

Mỗi endpoint được khai báo bằng ASP.NET Core Minimal API. Endpoint chỉ làm HTTP binding, xác thực và trả response; business logic nằm trong handler.

```text
Client

↓

Minimal API endpoint

↓

Command hoặc Query handler

↓

Repository / infrastructure service

↓

PostgreSQL hoặc external service

↓

Response
```

CQRS là bắt buộc:

* Command thay đổi state và có thể trả result nhỏ cần thiết cho client.
* Query chỉ đọc dữ liệu và không thay đổi state.
* Command và query có request, handler và response model riêng.

Không dùng MediatR, Event Bus hoặc read database riêng trong MVP nếu chưa có nhu cầu được xác thực.

---

# 5. Module Structure

Tổ chức theo feature và CQRS. Ví dụ Machine:

```text
Features/
  Machines/
    CreateMachine/
      CreateMachineCommand.cs
      CreateMachineHandler.cs
    GetMachine/
      GetMachineQuery.cs
      GetMachineHandler.cs
    MachineResponse.cs
    MachineEndpoints.cs
```

Endpoint mapping giữ mỏng; handler thực hiện một use case. Domain entity và repository abstraction vẫn nằm ở layer phù hợp theo dependency rule.

Ví dụ repository:

```text
Application/
  Features/Machines/IMachineRepository.cs

Infrastructure/
  Persistence/Machines/EfMachineRepository.cs
```

Query handler gọi method đọc có ý nghĩa nghiệp vụ; command handler gọi method thay đổi state. Endpoint không gọi `DbContext` trực tiếp.

---

# 6. AI Flow

Đây là phần duy nhất đặc biệt.

```text
Chat endpoints

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

Clean Architecture Lite vẫn dùng feature folders và CQRS, nhưng chỉ tạo type khi phục vụ một use case thực tế:

```text
Features/
  Machines/
    CreateMachine/
      CreateMachineCommand.cs
      CreateMachineHandler.cs
    GetMachine/
      GetMachineQuery.cs
      GetMachineHandler.cs
    MachineEndpoints.cs
```

Không thêm mediator, event, profile, validator hay abstraction chỉ vì một template có chúng. Cấu trúc này giữ command/query rõ ràng, tuân thủ SOLID và vẫn phù hợp cho MVP một người phát triển.

