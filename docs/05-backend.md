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

## Dependency injection ownership

```text
Application/DependencyInjection.cs
  Mediator, behaviors, validators, application services

Infrastructure/DependencyInjection.cs
  EF Core, repositories, security, external providers

Api/DependencyInjection.cs
  Authentication, authorization, Problem Details, HTTP services

Api/Program.cs
  Compose layers and configure the HTTP pipeline
```

Domain and Shared do not register services. They remain independent from the dependency-injection framework; do not add empty `AddDomain()` or `AddShared()` methods only for symmetry.

---

# 4. Request Flow

Mỗi endpoint được khai báo bằng ASP.NET Core Minimal API trong Presentation layer. Endpoint chỉ làm HTTP binding, xác thực, gửi request qua `Mediator` và map `Result` sang HTTP response; business logic nằm trong handler.

```text
Client

↓

Presentation Minimal API endpoint

↓

Mediator

↓

Command hoặc Query handler

↓

Repository / infrastructure service

↓

PostgreSQL hoặc external service

↓

Response
```

CQRS và Mediator là bắt buộc:

* Command thay đổi state và có thể trả result nhỏ cần thiết cho client.
* Query chỉ đọc dữ liệu và không thay đổi state.
* Command và query có request, handler và response model riêng.
* Handler trả về `Result` hoặc `Result<T>` từ `FactoryMind.Shared`.

Dùng source-generated `Mediator` để dispatch command/query. Không dùng Event Bus, read database riêng, generic repository hoặc Unit of Work abstraction trong MVP nếu chưa có nhu cầu được xác thực.

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

Endpoint mapping giữ mỏng; handler thực hiện một use case. Domain entity và repository abstraction vẫn nằm ở layer phù hợp theo dependency rule. Presentation mapping nhận `Result` và chuyển nó thành HTTP response.

Ví dụ repository:

```text
Application/
  Features/Machines/IMachineRepository.cs

Infrastructure/
  Persistence/Machines/EfMachineRepository.cs
```

Query handler gọi method đọc có ý nghĩa nghiệp vụ; command handler gọi method thay đổi state. Endpoint không gọi `DbContext` trực tiếp.

Sprint 4 starts with Machine as a complete business-data vertical slice. `GET`, `POST`, `PUT`, and `DELETE` operations are company-scoped, use the existing `Manager` policy, and expose search by code or name. Machine code uniqueness is enforced both by the use case and a tenant-scoped database index.

Material and Product reuse the same HTTP and authorization conventions while keeping explicit feature-specific commands, queries, repositories, validators, and response models. Their codes are also unique per company; Material keeps `Unit` as a required string and Product remains code/name only for the MVP.

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

Sprint 2 implements an OpenAI-compatible chat stream and persists `Conversation` and `Message`. Application exposes semantic stream updates; Presentation maps them to Server-Sent Events, and Infrastructure owns the provider-specific HTTP protocol.

Every chat repository operation is scoped by the current `CompanyId` and `UserId`. Knowledge RAG retrieves up to five company-scoped chunks, injects a bounded `[S#]` context, and persists only sources cited by the completed assistant answer. SSE emits `conversation`, `token`, `citations`, and `done` events. Intent detection and business-data retrieval remain later slices.

## API route definitions

All business endpoints keep the existing `/api` prefix. Presentation keeps route templates in a single `ApiRoutes` class, for example `ApiRoutes.Base`, `ApiRoutes.Auth.Login`, and `ApiRoutes.Documents.Process`. Endpoint mappings must not repeat literal API paths.

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

Sprint 3 runs PDF parsing and chunking through Hangfire after upload. Hangfire persists its internal jobs in a separate PostgreSQL schema and the document worker processes one PDF at a time locally. The job is idempotent: it replaces the document's existing chunks before marking the document ready.

PdfPig extracts text in content order. Chunking remains framework-independent Application logic and preserves the source page number for later citations. Image-only PDFs require OCR and are reported as failed in the MVP.

After chunking, the same background use case calls the configured OpenAI-compatible `embeddings` endpoint in batches of 64. The MVP requires 1,536-dimensional vectors and stores them in PostgreSQL through pgvector. Chunks and embeddings are committed together before the document becomes ready.

Configure the embedding model with `OpenAi__EmbeddingModel`. Keep the API key outside source control. The selected compatible provider must accept `POST /embeddings` with `model`, `input`, and `dimensions` and return indexed embedding arrays.

Semantic knowledge search is a CQRS query behind `POST /api/knowledge/search`. Presentation validates the request, Application embeds the question and applies the authenticated company scope, and Infrastructure performs exact pgvector cosine ordering. The search repository only returns chunks belonging to ready documents in the same company.

---

# 8. Configuration

Chỉ có:

```text
appsettings.json

appsettings.Development.json

.env
```

Không 20 file config.

Configure the Sprint 2 AI provider with environment variables (or the matching `OpenAi` configuration section):

```text
OpenAi__BaseUrl
OpenAi__ApiKey
OpenAi__Model
```

Never commit a real provider API key. The base URL must expose an OpenAI-compatible `chat/completions` streaming endpoint.

## Local PostgreSQL

The repository root contains `compose.yaml`. Start the current backend dependency with:

```text
docker compose up -d postgres
```

The container uses PostgreSQL 17 with pgvector binaries available, a persistent named volume, and a readiness healthcheck. EF Core migrations remain the only mechanism that creates or changes application database objects.

The host-run API connects through `localhost:${POSTGRES_PORT}`. Keep `ConnectionStrings__FactoryMind` aligned when changing database name, user, password, or port in `.env`.

MinIO is added in Sprint 3 when PDF upload starts. Redis and Hangfire infrastructure are not started until a running feature needs them.

## Local MinIO

Start PostgreSQL and MinIO with:

```text
docker compose up -d
```

The S3-compatible API is available at `localhost:${MINIO_API_PORT}` and the development console at `localhost:${MINIO_CONSOLE_PORT}`. The API creates the configured bucket on the first upload. Store object keys in PostgreSQL; never store PDF bytes in the database.

The local Compose ports bind to `127.0.0.1` only. The pinned MinIO image is for workstation development; select a maintained, security-reviewed deployment image before production.

---

# 9. Error Handling

Failures use RFC 7807 Problem Details (`application/problem+json`).

```json
{
  "type": "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.1",
  "title": "Validation failed",
  "status": 400,
  "detail": "One or more request fields are invalid.",
  "instance": "/api/auth/login",
  "traceId": "...",
  "errors": {
    "Email": ["Email is invalid."]
  }
}
```

Application handlers return `Result`/`Result<T>` for expected failures. Presentation maps those failures to Problem Details and exposes the stable application error code as an extension. Unexpected exceptions are handled centrally by `IExceptionHandler`.

Each response contains one clear English message in `detail`; do not return bilingual or localized message pairs in the MVP.

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

The SPA receives a short-lived access token and keeps it in memory only. The rotating refresh token is returned as an `HttpOnly`, `SameSite=Strict` cookie scoped to `/api/auth`; it is `Secure` outside Development. Login and refresh response bodies never expose the raw refresh token. Logout can revoke the cookie even after the access token expires.

Minimal API endpoints use named ASP.NET Core authorization policies. Protected commands and queries also implement `IAuthorizedRequest`; the Mediator `AuthorizationBehavior` checks authentication and the required policy before the handler runs.

This gives two boundaries:

* Presentation rejects unauthorized HTTP requests early with RFC 7807 Problem Details.
* Application prevents protected use cases from bypassing policy checks when dispatched through Mediator.

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
* Unit Test cho command/query handler, validator và authorization behavior quan trọng.
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

Chỉ thêm mediator, validator, behavior hoặc abstraction khi nó phục vụ use case thực tế. Cấu trúc này giữ command/query rõ ràng, tuân thủ SOLID và vẫn phù hợp cho MVP một người phát triển.

Business-data slices use explicit feature repositories and tenant-scoped CQRS handlers. Inventory references an existing material in the current company, keeps warehouse as a normalized scalar for the MVP, and enforces one balance row per material and warehouse. The API uses FluentValidation before dispatching commands and returns domain failures through the shared RFC 7807 mapping.

Production Order follows the same vertical-slice boundary: the handler resolves Product within the current tenant, normalizes the order number and status, and persists only the documented MVP lifecycle. Product references use restrictive deletion so historical order data cannot be orphaned.

Sprint 5 chat uses `IChatContextBuilder` to orchestrate deterministic intent routing and bounded retrieval. `Business` reads compact SQL projections through the feature-specific `IBusinessContextRepository`; `Knowledge` uses the existing tenant-scoped pgvector retrieval; `Hybrid` merges both. Every SQL projection filters `CompanyId` before ordering and limiting rows, and no entity graph or full table is sent to the model.

Business context uses `[B#]` labels and document context uses `[S#]`. The completed assistant answer is scanned for referenced labels, then `message_business_evidence` and `message_citations` store immutable snapshots. SSE emits business evidence separately from document citations so Presentation and the frontend keep the two source types explicit.

