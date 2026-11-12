# Hướng dẫn chạy dev và prod

Chạy lệnh từ thư mục gốc repository. Dev dùng `compose.yaml` cho PostgreSQL và MinIO;
API và Angular chạy trên host. Prod dùng `compose.prod.yaml` cho cả PostgreSQL, MinIO,
one-shot migration, API và frontend Nginx.

## Dev trên Windows / PowerShell

Cần Docker Desktop đang chạy, .NET SDK/runtime 9 và Node.js `24.18.1`.
Chỉ có .NET 10 không đủ để chạy ứng dụng target .NET 9 theo cấu hình mặc định.

### 1. Cấu hình

```powershell
Set-Location E:\workspace\factory-mind
if (-not (Test-Path .env.local)) {
    Copy-Item .env.example .env.local
}
```

Điền `GEMINI_API_KEY` trong `.env.local` để dùng chat và embeddings. Không ghi key vào frontend.
Nếu cổng PostgreSQL mặc định bị chiếm, chỉnh cả hai dòng dưới trong `.env.local`
(giữ database, user và password thực tế của bạn):

```dotenv
POSTGRES_PORT=5433
ConnectionStrings__FactoryMind=Host=localhost;Port=5433;Database=factorymind;Username=postgres;Password=postgres
```

Tại lần thiết lập ngày 16/10/2026 trên máy này, cổng `5432` thuộc dự án khác nên
`.env.local` đã được đổi sang `5433`. Máy khác có thể giữ cổng `5432` nếu còn trống.
Các thay đổi `POSTGRES_*` không đổi credentials của database đã tồn tại trong volume.

Trên máy này `9000/9001` cũng thuộc stack khác, nên `.env.local` đang dùng:

```dotenv
MINIO_API_PORT=9100
MINIO_CONSOLE_PORT=9101
Minio__Endpoint=localhost:9100
```

Nếu đổi cổng MinIO, luôn đổi cả `Minio__Endpoint` để API trên host kết nối đúng.

### 2. Chạy container dev

```powershell
docker compose --env-file .env.local -f compose.yaml up -d --build --wait --wait-timeout 120
docker compose --env-file .env.local -f compose.yaml ps
```

Lần đầu sẽ build MinIO từ tag upstream `RELEASE.2025-09-07T16-13-09Z` với commit được
kiểm tra trong `docker/minio.Dockerfile`. Các lần sau dùng cache. Cách này tránh phụ thuộc
image lịch sử hiện không tải được từ Docker Hub/Quay. Upstream mô tả hình thức phân phối
source-only tại [MinIO README](https://github.com/minio/minio#source-only-distribution).

### 3. Chạy API trong terminal thứ nhất

```powershell
Get-Content .env.local | ForEach-Object {
    if ($_ -match '^\s*([^#=\s]+)=(.*)$' -and $Matches[2].Length -gt 0) {
        Set-Item "Env:$($Matches[1])" $Matches[2]
    }
}
dotnet run --project src/FactoryMind.Api --launch-profile http
```

Đoạn nạp env trên dùng giá trị không bọc dấu nháy, như `.env.example`.
Development tự migrate database và seed demo idempotent khi API khởi động.

### 4. Chạy Angular trong terminal thứ hai

```powershell
Set-Location E:\workspace\factory-mind\frontend
npm ci
npm start -- --host 127.0.0.1 --port 4200
```

| Dịch vụ | Địa chỉ trên máy này |
| --- | --- |
| UI | http://127.0.0.1:4200 |
| API readiness | http://localhost:5047/health/ready |
| PostgreSQL | localhost:5433 |
| MinIO S3 API | http://localhost:9100 |
| MinIO Console | http://localhost:9101 |

MinIO dùng `MINIO_ROOT_USER` và `MINIO_ROOT_PASSWORD` trong `.env.local`.
Đăng nhập ứng dụng dev bằng `admin@factorymind.local`, mật khẩu `Demo@123`.
Còn có `manager@factorymind.local` và `operator@factorymind.local` với cùng mật khẩu demo.

### 5. Xem log và dừng

```powershell
docker compose --env-file .env.local -f compose.yaml logs --tail 100 postgres minio
docker compose --env-file .env.local -f compose.yaml stop
# Chạy lại container đã tạo:
docker compose --env-file .env.local -f compose.yaml start
# Xóa container/network, giữ volume dữ liệu:
docker compose --env-file .env.local -f compose.yaml down
```

Dừng API/Angular bằng `Ctrl+C` ở terminal tương ứng. `down -v` sẽ xóa dữ liệu volume;
không cần dùng cho thao tác dừng/chạy thông thường.

## Prod trên Linux VPS

Quy trình đầy đủ: [production-deployment.md](production-deployment.md).
Cần Docker Engine 27+, Compose 2.24+, Bash 5, curl, flock và GNU coreutils.
API/frontend dùng image đã publish lên GHCR bởi CI, cùng một Git SHA đủ 40 ký tự.

### 1. Chuẩn bị checkout và env

```bash
sudo install -d -m 700 /opt/factorymind/{app,releases,backups}
# Chạy các bước tiếp theo bằng tài khoản có quyền ghi các thư mục trên và dùng Docker.
cd /opt/factorymind/app
git clone https://github.com/johnvo402/factory-mind.git .
cp .env.production.example .env.production
chmod 600 .env.production
```

Điền `POSTGRES_PASSWORD`, `MINIO_ROOT_USER`, `MINIO_ROOT_PASSWORD`, `JWT_KEY` (ít nhất
32 ký tự), `GEMINI_API_KEY`. Database trống cần đủ `BOOTSTRAP_COMPANY_NAME`,
`BOOTSTRAP_ADMIN_NAME`, `BOOTSTRAP_ADMIN_EMAIL`, `BOOTSTRAP_ADMIN_PASSWORD` (ít nhất 12 ký tự).
Đặt `FACTORYMIND_STATE_DIR=/opt/factorymind/releases`, `BACKUP_ROOT=/opt/factorymind/backups`.
Không sao chép credentials dev sang prod.

### 2. Chuẩn bị image MinIO và công cụ backup

Các image lịch sử `minio/minio` và `quay.io/minio/minio` đã trả lỗi khi thử tải trên máy
này ngày 16/10/2026. Build server và client từ source ngay trên VPS trước khi deploy:

```bash
docker build -f docker/minio.Dockerfile --target server \
  -t factory-mind-minio:RELEASE.2025-09-07T16-13-09Z-source .
docker build -f docker/minio.Dockerfile --target client \
  -t factory-mind-mc:RELEASE.2025-08-13T08-35-41Z-source .
```

Đặt trong `.env.production`:

```dotenv
MINIO_IMAGE=factory-mind-minio:RELEASE.2025-09-07T16-13-09Z-source
MINIO_MC_IMAGE=factory-mind-mc:RELEASE.2025-08-13T08-35-41Z-source
```

Có thể build/push vào registry do bạn quản lý và dùng các image reference đó thay cho image
local. Prod Compose vẫn chỉ dùng image, không build trong lúc deploy. Dockerfile giữ nguyên
phiên bản MinIO hiện có của dự án; việc build từ source không tự nâng cấp phiên bản đó.

### 3. Deploy release

Nếu package GHCR private, đăng nhập trước bằng token có `read:packages`:

```bash
printf '%s' "$CR_PAT" | docker login ghcr.io --username YOUR_GITHUB_USER --password-stdin
```

Chọn SHA có API/frontend images đã build thành công trong CI. Trong `.env.production`, đặt
`IMAGE_TAG` bằng chính SHA đó để các lệnh Compose vận hành sau deploy dùng đúng release.

```bash
./scripts/production/deploy.sh YOUR_40_CHARACTER_RELEASE_SHA
docker compose --env-file .env.production -f compose.prod.yaml ps
./scripts/production/smoke.sh YOUR_40_CHARACTER_RELEASE_SHA
```

Script thực hiện pull image → backup đã kiểm tra → migration/bootstrap → API → frontend →
health/smoke. Đăng nhập UI bằng tài khoản `BOOTSTRAP_ADMIN_EMAIL`/`BOOTSTRAP_ADMIN_PASSWORD`.
Sau lần deploy đầu thành công, xóa các giá trị `BOOTSTRAP_*` khỏi env.

Chỉ frontend được publish ở `HTTP_PORT` (mặc định `80`). Đặt reverse proxy/TLS phía trước
để phục vụ HTTPS; PostgreSQL, MinIO và API nằm trong Docker network riêng.

### 4. Vận hành

```bash
docker compose --env-file .env.production -f compose.prod.yaml logs --tail 100 api frontend
./scripts/production/backup.sh YOUR_40_CHARACTER_RELEASE_SHA
./scripts/production/rollback.sh
docker compose --env-file .env.production -f compose.prod.yaml stop
```

Rollback chỉ đổi application images, không đảo migration. Tham khảo
[backup-restore.md](backup-restore.md) trước khi restore dữ liệu.
