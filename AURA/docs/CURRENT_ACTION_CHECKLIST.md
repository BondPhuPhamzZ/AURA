# Checklist hiện tại: publish, mailbox sandbox và hóa đơn tiếng Trung

Ngày chốt: 17/10/2026. Đây là checklist vận hành cho candidate hiện hành; không phải tuyên bố production-ready.

## 1. Trạng thái đã xác minh trong source

- [x] Manual upload đi qua SQL queue bền vững, worker, AI, semantic, privacy, policy và audit.
- [x] Mailbox sandbox synthetic đã có API key, allow-list sender/folder, idempotency, cursor, quarantine và transactional outbox.
- [x] Mailbox sandbox dùng lại đúng ingestion/worker hiện hành; không tạo pipeline AI thứ hai.
- [x] AI concurrency vẫn là `1`: nhiều attachment được nhận vào SQL nhưng chờ lần lượt trước provider.
- [x] Contract đã nhận chữ Trung Quốc giản thể/phồn thể, giữ nguyên evidence, hiểu nhãn tổng/ngày/định danh phổ biến.
- [x] Semantic hỗ trợ ngày `yyyy年M月d日`, nhãn tổng tiếng Trung và loại dòng tổng bị lặp khỏi line item.
- [x] Policy nhận diện một số danh mục cấm tiếng Trung, nhưng mọi chứng từ có chữ Trung Quốc vẫn fail-safe sang `ESCALATE_FACT`.
- [x] Release test hiện hành: 206/206 pass; Release build 0 warning/error.
- [ ] Candidate source mới nhất chưa được gọi là live cho đến khi publish, recycle và smoke đạt.

## 2. Những thứ người vận hành cần chuẩn bị

- [ ] Quyền cập nhật environment variables và recycle application pool/site.
- [ ] Backup SQL và `App_Data/receipts`; ghi timestamp và vị trí bản backup.
- [ ] Một sender synthetic riêng, ví dụ `demo.sender@example.test`; không dùng mailbox hay dữ liệu cá nhân thật.
- [ ] Một secret ngẫu nhiên tối thiểu 24 ký tự cho `MailboxSandbox__ApiKey`; lưu trong secret store, không commit/ghi vào evidence.
- [ ] Ảnh JPG/PNG đã ẩn danh, tối đa 5 MB/ảnh. Chuẩn bị ít nhất: một hóa đơn hợp lệ, một file sai magic/MIME, và một ảnh hóa đơn tiếng Trung synthetic/đã được phép sử dụng.
- [ ] Ground truth cho ảnh tiếng Trung: merchant, số chứng từ, ngày, tiền tệ, tổng cuối, line items và kết quả kỳ vọng.

Không cần Google Cloud project, Gmail OAuth, Pub/Sub hay mở Gmail API ở bước này. “Mở mailbox sandbox” nghĩa là bật connector synthetic của AURA và gửi attachment bằng API/script. Gmail thật là gate tương lai.

## 3. Environment variables cần giữ hoặc kiểm tra

Các biến nền hiện hành:

```text
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=<existing SQL connection string>

Vision__Provider=OpenRouter
Vision__FallbackEnabled=false
Vision__FallbackProvider=Ollama
Vision__MaxConcurrentRequests=1
Vision__ProviderCooldownSeconds=120

OpenRouter__ApiKey=<existing secret>
OpenRouter__Model=qwen/qwen3.8-flash
OpenRouter__ReasoningEffort=none
OpenRouter__DenyProviderDataCollection=true
OpenRouter__RequireZeroDataRetention=false

ReceiptStorage__Directory=App_Data/receipts
ReceiptStorage__MaxFileSizeMb=5
Verify__Enabled=true
```

Biến mới để bật mailbox sandbox:

```text
MailboxSandbox__Enabled=true
MailboxSandbox__ApiKey=<random secret, at least 24 characters>
MailboxSandbox__Provider=SANDBOX
MailboxSandbox__MailboxId=aura-mail-sandbox
MailboxSandbox__AllowedFolder=AURA_INVOICE
MailboxSandbox__AllowedSenders__0=demo.sender@example.test
MailboxSandbox__RequireMalwareScanner=false
MailboxSandbox__MaxAttachmentsPerMessage=10
```

`RequireMalwareScanner=false` chỉ dành cho fixture synthetic đã kiểm soát. Không dùng cấu hình này để nhận email/attachment thật. `RequireZeroDataRetention=false` phản ánh route hiện đã chạy; chỉ bật Strict ZDR sau khi xác minh account/provider còn route tương thích.

## 4. Thứ tự migrate và publish an toàn

- [ ] Ghi lại commit sẽ publish: `git rev-parse HEAD`.
- [ ] Chạy `dotnet test tests/AURA.Tests/AURA.Tests.csproj -c Release` và yêu cầu 0 fail.
- [ ] Chạy `dotnet build AURA.csproj -c Release --no-restore` và yêu cầu 0 error.
- [ ] Backup SQL/storage trước migration.
- [ ] Ưu tiên apply migration `AddSandboxMailboxIngestion` bằng quy trình migration có rollback.
- [ ] Nếu hosting chỉ cho startup migration: đặt `Database__ApplyMigrationsOnStartup=true` đúng một lần, recycle, chờ `/healthz` báo `pendingMigrationCount=0`, sau đó lập tức đổi lại `false` và recycle lần nữa.
- [ ] Publish đúng `HEAD`, không publish thư mục build cũ.
- [ ] Recycle site/application pool.
- [ ] Không xóa database, receipt storage hay audit cũ.

## 5. Health gate sau publish

- [ ] `GET /healthz` trả HTTP 200 và `status=ok`.
- [ ] `databaseAvailable=true`, `databaseUpToDate=true`, `pendingMigrationCount=0`, `storageAvailable=true`.
- [ ] `model=qwen/qwen3.8-flash`, `maxConcurrentAiRequests=1`, `fallbackEnabled=false`.
- [ ] `providerDataCollectionDenied=true`; ghi trung thực `strictZeroDataRetention=false` nếu chưa bật.
- [ ] Response phải có `mailboxSandboxEnabled`, `mailboxSandboxMode=synthetic-sandbox-only` và `gmailConnected=false`. Nếu thiếu các field này, live site vẫn đang chạy bản cũ.
- [ ] `GET /api/mailbox-sandbox/status` báo `enabled=true`, đúng folder, sender count và `productionReady=false`.

## 6. Cách “upload qua mail sandbox”

Đây là synthetic delivery, không gửi mail trong Gmail UI:

```powershell
$env:MailboxSandbox__ApiKey = Read-Host "Sandbox API key"
Set-Location D:\aura\AURA\AURA
.\tools\Invoke-SandboxMailboxPipeline.ps1 `
  -BaseUrl "https://<live-host>" `
  -ApiKey $env:MailboxSandbox__ApiKey `
  -ImagePath ".\wwwroot\test_data\images\HoaDon1.jpg" `
  -RepeatDelivery
```

Kết quả bắt buộc:

- [ ] Lần đầu trả HTTP 202/`ACCEPTED`, có `requestId` và `statusUrl`.
- [ ] Job về `COMPLETED` hoặc `FAILED` có lý do; không kẹt `PENDING`.
- [ ] Gửi lặp cùng message/attachment trả `DUPLICATE` và cùng `requestId`.
- [ ] Sender/folder ngoài allow-list bị từ chối.
- [ ] Thiếu/sai API key bị HTTP 401.
- [ ] File sai MIME/magic bytes vào `QUARANTINED`, không tạo AI job.
- [ ] Audit có `AI_QUEUED`, source `SANDBOX_MAILBOX` và timeline về terminal.

Endpoint synthetic nhận một attachment mỗi request. Cùng một email có thể delivery nhiều attachment bằng các `AttachmentId` khác nhau, tối đa 10 theo cấu hình; mỗi attachment tạo một job độc lập. Đây không phải giới hạn tổng số hóa đơn của hệ thống.

## 7. Gate kiểm thử hóa đơn tiếng Trung

Nguồn model cho thấy Qwen là VLM đa ngôn ngữ có năng lực đọc chữ Trung/Anh và dòng Qwen hiện hành nhận ảnh. Tuy vậy, năng lực model không thay thế benchmark của AURA.

- [ ] Tạo tối thiểu 10 ảnh holdout được phép dùng: giản thể/phồn thể, ảnh rõ/mờ vừa phải, retail/VAT/restaurant, CNY và một ca VND có chữ Trung.
- [ ] Khóa ground truth trước khi chạy: merchant, ID, ngày, currency, total, từng line item và category cấm.
- [ ] Chạy cùng ảnh qua manual upload và mailbox sandbox; kết quả extraction phải nhất quán.
- [ ] Kiểm chữ Trung được giữ nguyên, không dịch; ngày `2026年10月17日` thành `2026-10-17`; `人民币/RMB/元` thành `CNY`; tổng lấy từ dòng `合计/总计/应付金额/实付金额/价税合计`.
- [ ] Ca `青岛啤酒`, `香烟`, `电影票` hoặc tương đương phải được nêu là policy signal.
- [ ] Mọi chứng từ tiếng Trung hiện phải ra `ESCALATE_FACT`, không `AUTO_APPROVE`.
- [ ] Báo riêng field accuracy, decision route, missed-escalation, over-escalation và lỗi provider; không dùng một ảnh thành tuyên bố “hỗ trợ production”.
- [ ] Chỉ cân nhắc tự duyệt sau khi có benchmark đủ mạnh, quy đổi CNY, rule theo pháp nhân/thị trường, kiểm privacy và người nghiệp vụ phê duyệt.

## 8. Sau demo

- [ ] Lưu evidence đã redaction: health JSON, script output, request IDs, timestamps, commit SHA và kết quả test; không lưu secret/raw PII.
- [ ] Đặt lại `Database__ApplyMigrationsOnStartup=false`.
- [ ] Tắt `MailboxSandbox__Enabled=false` nếu không còn demo; recycle và xác nhận endpoint delivery không còn mở.
- [ ] Không gọi sandbox này là Gmail integration, mail ingestion production hoặc production-ready.

## 9. Câu trả lời ngắn nếu BGK hỏi tiếng Trung

> Model vision nền có năng lực đọc chữ Trung, và AURA đã bổ sung contract cùng semantic cho nhãn tổng, ngày, tiền tệ và item tiếng Trung. Tuy nhiên chúng em tách “đọc được” khỏi “được phép tự quyết định”: trước khi có holdout tiếng Trung, quy đổi CNY và rule theo thị trường, hệ thống luôn chuyển người kiểm tra, không tự duyệt. Đây là fail-safe có chủ đích.
