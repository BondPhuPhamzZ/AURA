# Tiến độ hardening sau feedback Sprint 1

Ngày cập nhật: 28/09/2026

## Kết luận hiện tại

Pipeline upload hóa đơn đã chuyển từ một HTTP request chờ model hoàn tất sang cơ chế lưu trước, xử lý nền và theo dõi trạng thái. Thay đổi này giải quyết rủi ro request kéo dài, refresh làm mất trải nghiệm và khóa toàn hệ thống khi nhiều người cùng thao tác. Verify Harness vẫn giữ nguyên đúng 5 ca BTC đã biết.

OpenRouter tiếp tục là provider chính. Ollama có thể làm fallback trên cùng laptop demo, nhưng fallback mặc định tắt để không làm sai benchmark và không tạo phụ thuộc Ollama trên hosted server.

## Những phần đã triển khai

### 1. Durable background processing

- `POST /Applicant/UploadReceipt` xác thực file, lưu ảnh riêng tư, tạo record `PENDING`, audit `AI_QUEUED` và trả `202 Accepted` cùng `statusUrl`.
- `ReceiptProcessingWorker` đọc job từ SQL Server. Channel trong bộ nhớ chỉ là tín hiệu đánh thức; database là nguồn sự thật.
- Worker claim job bằng update có điều kiện, gắn lease và tăng `ProcessingAttemptCount`.
- Job `PROCESSING` bị gián đoạn có thể được reclaim sau khi lease hết hạn.
- UI poll `GET /Applicant/Status/{id}` đến `COMPLETED` hoặc `FAILED` và lưu status URL trong `sessionStorage` để nối lại sau refresh.
- `WorkflowOperationGate` không còn khóa upload, forward hoặc manager action; gate chỉ ngăn hai Verify batch chạy chồng.

### 2. Fallback và phục hồi provider

- Thêm `Vision:FallbackEnabled`, `Vision:FallbackProvider`, ngưỡng circuit và thời gian cooldown.
- Fallback chỉ áp dụng cho timeout, rate limit, auth/credit, model/provider unavailable, lỗi HTTP hạ tầng hoặc Ollama unavailable.
- Không fallback cho schema mismatch, semantic invalid, request invalid, response rỗng hoặc bị cắt.
- Mỗi request chỉ gọi fallback một lần.
- Circuit breaker mở sau ngưỡng lỗi liên tiếp, bỏ qua primary trong cooldown rồi cho primary một lần probe.
- Audit lưu `PrimaryProvider`, `ServedProvider`, `FallbackUsed` và `ProviderErrorCode`.

### 3. Dữ liệu và khả năng truy vết

- Thêm `ProcessingState`, thời điểm queue/start/complete, lease, attempt, provider metadata và duplicate flags.
- Thêm mã, tên và bộ phận người gửi ở UI và database. Đây là metadata demo, chưa thay thế authentication.
- Thêm index `(ProcessingState, QueuedAt)` phục vụ worker và `(RequestId, Timestamp)` phục vụ audit timeline.
- Migration: `20260928095906_AddDurableReceiptProcessing`.
- Dữ liệu cũ được backfill thành `COMPLETED` để worker không chạy lại hồ sơ lịch sử.

### 4. Kiểm thử và công cụ vận hành

- Automated tests tăng từ 70 lên 80; kết quả hiện tại: 80/80 pass.
- Build hiện tại: 0 warning, 0 error.
- EF Core: migration và model snapshot đã được cập nhật cùng thay đổi schema; app smoke test đã truy vấn thành công các cột mới.
- LocalDB đã áp migration thành công trong lần xác minh.
- Thêm `tools/Invoke-ConcurrentUploadSmoke.ps1` để chạy 1-10 upload đồng thời, đo HTTP accepted P95, end-to-end P95, provider phục vụ, fallback và error code.
- Test integrity vẫn khóa Verify Harness ở đúng 5 ca: 3 `AUTO_APPROVE`, 1 `ESCALATE_FACT`, 1 `ESCALATE_POLICY`.

## Cấu hình khuyến nghị

### Benchmark một provider

```powershell
dotnet user-secrets set "Vision:FallbackEnabled" "false"
dotnet user-secrets set "Vision:Provider" "OpenRouter"
```

Khi đo Ollama, đổi `Vision:Provider` sang `Ollama` nhưng vẫn giữ fallback tắt.

### Demo laptop có fallback

```powershell
ollama list
Invoke-RestMethod 'http://127.0.0.1:11434/api/tags'
dotnet user-secrets set "Vision:Provider" "OpenRouter"
dotnet user-secrets set "Vision:FallbackEnabled" "true"
dotnet user-secrets set "Vision:FallbackProvider" "Ollama"
dotnet ef database update
dotnet run
```

Không bật Ollama fallback trên hosted server nếu Ollama chỉ chạy ở laptop. `127.0.0.1` của hosted server không trỏ về laptop.

## Những phần cố ý chưa triển khai

| Hạng mục | Trạng thái | Lý do |
|---|---|---|
| Thay 5 ca Verify chính thức | Không thay đổi | BTC yêu cầu báo lại nếu thay đổi đáng kể; baseline hiện đã ổn định |
| SignalR cross-tab realtime | Chưa triển khai | Polling theo request đã giải quyết upload; cần test race và reconnect trước khi mở rộng |
| Authentication và role thật | Chưa triển khai | Metadata người gửi chỉ phục vụ demo; auth cần scope riêng và migration người dùng |
| Resize/compress ảnh trước AI | Chưa bật | Thay pixel có thể làm mất chữ nhỏ; cần benchmark field accuracy trước |
| Distributed queue/circuit breaker | Chưa triển khai | Bản demo một instance dùng SQL job state + circuit memory là đủ; scale-out cần Redis/queue service |
| PDF/nhiều trang và antivirus | Chưa triển khai | Không thuộc baseline đã chốt và cần pipeline file riêng |
| Đổi nền tảng cloud | Chưa thực hiện | Demo chung kết chạy trên laptop theo xác nhận BTC; deploy chỉ là tùy chọn |

## Checklist người vận hành cần làm tiếp

1. Pull commit mới và chạy `dotnet ef database update` trên đúng database demo.
2. Chạy `dotnet build --no-restore` và `dotnet test tests/AURA.Tests/AURA.Tests.csproj --no-restore`; kỳ vọng 80/80.
3. Giữ fallback tắt, test một upload OpenRouter và một Verify 5 ca để tái xác nhận baseline.
4. Mở Ollama, bật fallback, chủ động mô phỏng lỗi primary bằng một key/model test không hợp lệ chỉ trong database test; xác nhận audit provider. Không làm bước này trên database/video chính.
5. Chạy concurrency smoke 5 request trên database riêng hoặc bản sao demo và ghi Accepted P95, End-to-end P95, error rate.
6. Chạy lại ba lượt upload và ba batch Verify với fallback tắt nếu cần benchmark so sánh sau thay đổi pipeline. Không dùng số cũ để tuyên bố latency end-to-end của pipeline mới.
7. Chỉ sau khi các cổng trên pass mới quay video/chốt slide và báo BTC. Nếu 5 fixture/expected không đổi, không cần ticket riêng về test case; nên gửi check-in về thay đổi kiến trúc xử lý nền và fallback opt-in.

## Tiêu chí hoàn tất trước demo

- App khởi động sạch trên database đã migrate; `/healthz` trả `ok`.
- Upload trả `202` nhanh và vẫn hoàn tất sau refresh.
- Một lỗi provider không tạo PASS giả; fallback hoặc `ESCALATE_SYSTEM_ERROR` có audit rõ.
- Verify đúng 5/5 trong cấu hình demo đã chốt.
- Hai thao tác quản lý cùng hồ sơ không ghi đè im lặng.
- Ảnh và audit vẫn mở sau restart app.
- Fallback được bật/tắt bằng cấu hình, không sửa code sát giờ demo.
- Slide, workflow, README, runbook và build log nêu cùng một pipeline và cùng mốc 80 tests.
