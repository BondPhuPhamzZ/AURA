# Tiến độ hardening sau feedback Sprint 1

Ngày cập nhật: 01/10/2026

## Kết luận hiện tại

Pipeline upload hóa đơn đã chuyển từ một HTTP request chờ model hoàn tất sang cơ chế lưu trước, xử lý nền và theo dõi trạng thái. Thay đổi này giải quyết rủi ro request kéo dài, refresh làm mất trải nghiệm và khóa toàn hệ thống khi nhiều người cùng thao tác. Verify Harness vẫn giữ nguyên đúng 5 ca BTC đã biết.

OpenRouter tiếp tục là provider chính. Ollama có thể làm fallback trên cùng laptop demo, nhưng fallback mặc định tắt để không làm sai benchmark và không tạo phụ thuộc Ollama trên hosted server.

Live validation ngày 29/09/2026 đã chốt thêm: OpenRouter đạt 15/15 quyết định với P95 16,459 giây; Ollama đạt 14/15 với P95 58,318 giây và bỏ sót escalation TK-12. Vì vậy Ollama vẫn là đường local/offline có giới hạn, chưa đủ điều kiện bật auto-fallback cho toàn bộ request. Xem [báo cáo live validation](LIVE_VALIDATION_2026-09-29.md).

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

- Automated tests tăng từ 70 lên 88 trong hardening Sprint 2, lên 93 sau hardening định danh chứng từ giấy, 100 sau đối chiếu giảm giá và 102 sau postfix live regressions; kết quả hiện tại: 102/102 pass.
- Build hiện tại: 0 warning, 0 error.
- EF Core: migration và model snapshot đã được cập nhật cùng thay đổi schema; app smoke test đã truy vấn thành công các cột mới.
- LocalDB đã áp migration thành công trong lần xác minh.
- Thêm `tools/Invoke-ConcurrentUploadSmoke.ps1` để chạy 1-10 upload đồng thời, đo HTTP accepted P95, end-to-end P95, provider phục vụ, fallback và error code.
- Test integrity vẫn khóa Verify Harness ở đúng 5 ca: 3 `AUTO_APPROVE`, 1 `ESCALATE_FACT`, 1 `ESCALATE_POLICY`.
- EF Core SQL Server/Tools và local `dotnet-ef` đã nâng cùng major lên 8.0.31. Build Release 0 warning/0 error, 102/102 test pass, không có package bị NuGet vulnerability scan cảnh báo và EF không có pending model change.
- Thêm `tools/Invoke-ExtendedDatasetEvaluation.ps1` để chạy 15/30 ca qua upload endpoint mà không thêm nút UI. Runner xuất metadata, CSV, JSON và summary gồm decision/field accuracy, missed/over-escalation, system error, fallback và P50/P95.
- Delay 4 giây của Official Verify chuyển thành `Verify:InterCaseDelayMs`; mặc định vẫn 4000 ms để không thay đổi baseline/quota behavior.
- `/healthz` giờ kiểm tra thêm kết nối database, pending migration và khả dụng của thư mục receipt; thiếu một trong các cổng này sẽ trả `503 degraded` thay vì báo sẵn sàng giả. Endpoint không gọi AI provider nên không tiêu quota.
- Postfix live review ngày 01/10 thay strict semantic date repair bằng lossless canonicalization: paper receipt có `invoiceDate=null` và một `transactionDate` hợp lệ được chuẩn hóa mà không tốn lượt AI thứ hai; `completionDate` vẫn không được nâng. UI và policy cùng dùng ngày chứng từ canonical. Highlands-style điểm `1000` bị nhận nhầm discount chỉ được bỏ khi ba tổng độc lập đã bằng nhau; Vinamilk `2828` vẫn giữ. Bảng escalation tự đồng bộ DB mỗi 5 giây và có progress notice rõ ràng.
- Real receipt Phê La ngày 02/10 phát hiện fail-open khi vùng món hàng bị che nhưng model trả confidence cao và `lineItems=[]`. Guard mới buộc paper receipt thiếu item đi qua one-shot repair rồi `ESCALATE_FACT`, ghi missing field/warning và hạ confidence tối đa 0.69. Readiness thêm transcript `-OutputPath`; lỗi inspect registry không còn che kết quả health database, và default path của smoke/extended runner được resolve sau parameter binding để tương thích Windows PowerShell 5.1.
- Extended evaluator đã chạy thật trên cùng 15 ca với fallback tắt: OpenRouter 15/15, Ollama 14/15. Runner hiện resolve expected facts từ source manifest và lưu hash để field exact không bị rỗng.
- Concurrent smoke OpenRouter 5 request hoàn tất 5/5, HTTP accepted P95 93 ms và end-to-end P95 17,072 giây.
- UI đã đối chiếu Linear design system: một shape token 6 px, palette/contrast gọn hơn và không tràn trang ở 1280, 768, 390 px; Console không có warning/error.
- Workbook so sánh có raw rows, công thức, chart, phương pháp và giới hạn được tạo tại `D:\aura\compare_Provider\AURA_Provider_Comparison_2026-09-29.xlsx`.
- Worker tăng backoff từ 5 giây đến trần 60 giây khi SQL tạm mất kết nối và giảm full-stack log lặp. Khi DB phục hồi, job còn nằm trong SQL và được tiếp tục xử lý.
- Thêm `tools/Test-DemoReadiness.ps1`; script đã chạy thành công bằng Windows PowerShell 5.1, xác nhận đúng `AuraDb`, LocalDB, policy, storage, migration, provider và fallback mà không in secret.
- Hàng đợi quản lý và Lịch sử hành vi hiển thị tên, mã và phòng ban người nộp thay vì nhãn ẩn danh cố định.

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
| Nút batch 15/30 trên dashboard | Không thêm | Runner ngoài UI giữ màn hình gọn và bảo toàn mapping riêng của từng ca |
| SignalR cross-tab realtime | Chưa triển khai | Polling theo request đã giải quyết upload; cần test race và reconnect trước khi mở rộng |
| Authentication và role thật | Chưa triển khai | Metadata người gửi chỉ phục vụ demo; auth cần scope riêng và migration người dùng |
| Resize/compress ảnh trước AI | Chưa bật | Thay pixel có thể làm mất chữ nhỏ; cần benchmark field accuracy trước |
| Auto-fallback Ollama trong demo | Chưa bật | Local 4B còn missed escalation TK-12 và không đủ concurrent capacity trên RTX 3050 4 GB |
| Distributed queue/circuit breaker | Chưa triển khai | Bản demo một instance dùng SQL job state + circuit memory là đủ; scale-out cần Redis/queue service |
| PDF/nhiều trang và antivirus | Chưa triển khai | Không thuộc baseline đã chốt và cần pipeline file riêng |
| Đổi nền tảng cloud | Chưa thực hiện | Demo chung kết chạy trên laptop theo xác nhận BTC; Live URL vẫn cần smoke riêng cho bản bàn giao, nhưng không nên đổi hạ tầng sát demo |

## Checklist người vận hành cần làm tiếp

1. Pull commit mới, chạy `.\tools\Test-DemoReadiness.ps1 -StartLocalDb -SkipHttp`, rồi `dotnet ef database update` trên đúng database demo.
2. Chạy `dotnet build ../AURA.sln --no-restore` và `dotnet test tests/AURA.Tests/AURA.Tests.csproj --no-restore`; kỳ vọng 102/102. Không dùng `--no-build` nếu test project chưa vừa được build đúng configuration.
3. Giữ fallback tắt, test một upload OpenRouter và một Verify 5 ca để tái xác nhận baseline.
4. Mở Ollama, bật fallback, chủ động mô phỏng lỗi primary bằng một key/model test không hợp lệ chỉ trong database test; xác nhận audit provider. Không làm bước này trên database/video chính.
5. Chạy concurrency smoke 5 request trên database riêng hoặc bản sao demo và ghi Accepted P95, End-to-end P95, error rate.
6. Chạy lại ba lượt upload và ba batch Verify với fallback tắt nếu cần benchmark so sánh sau thay đổi pipeline. Không dùng số cũ để tuyên bố latency end-to-end của pipeline mới.
7. Chỉ sau khi các cổng trên pass mới quay video/chốt slide và báo BTC. Nếu 5 fixture/expected không đổi, không cần ticket riêng về test case; nên gửi check-in về thay đổi kiến trúc xử lý nền và fallback opt-in.
8. Khi quota cho phép, chạy gói 15 ca bằng runner ngoài UI và lưu nguyên thư mục evidence. Gói 30 ca chỉ chạy trong phiên benchmark riêng; không chạy trong phần trình bày.

## Tiêu chí hoàn tất trước demo

- App khởi động sạch trên database đã migrate; `/healthz` trả `ok` với `databaseAvailable=true`, `databaseUpToDate=true`, `pendingMigrationCount=0` và `storageAvailable=true`.
- Upload trả `202` nhanh và vẫn hoàn tất sau refresh.
- Một lỗi provider không tạo PASS giả; fallback hoặc `ESCALATE_SYSTEM_ERROR` có audit rõ.
- Verify đúng 5/5 trong cấu hình demo đã chốt.
- Hai thao tác quản lý cùng hồ sơ không ghi đè im lặng.
- Ảnh và audit vẫn mở sau restart app.
- Fallback được bật/tắt bằng cấu hình, không sửa code sát giờ demo.
- Slide, workflow, README, runbook và build log nêu cùng một pipeline và cùng mốc 102 tests. Mốc 88/93/100 chỉ còn xuất hiện khi mô tả lịch sử của các đợt hardening trước.
