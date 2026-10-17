# Mailbox sandbox runbook

## Ranh giới

Mailbox sandbox mô phỏng delivery của provider để kiểm tra pipeline. Nó không kết nối Gmail, không giữ OAuth token, không đọc mailbox thật và không phải malware scanner production. Feature mặc định tắt.

```text
synthetic delivery + API key
→ allow-list sender/folder
→ source idempotency
→ attachment validation/quarantine
→ SQL message/attachment/cursor/outbox
→ cùng ReceiptProcessingWorker
→ AI → semantic → privacy → policy → human → audit
```

## Biến môi trường local sandbox

Không commit API key. Dùng key ngẫu nhiên tối thiểu 24 ký tự.

```text
MailboxSandbox__Enabled=true
MailboxSandbox__ApiKey=<secret-random-key>
MailboxSandbox__Provider=SANDBOX
MailboxSandbox__MailboxId=aura-mail-sandbox
MailboxSandbox__AllowedFolder=AURA_INVOICE
MailboxSandbox__AllowedSenders__0=demo.sender@example.test
MailboxSandbox__RequireMalwareScanner=false
```

`RequireMalwareScanner=false` chỉ hợp lệ cho fixture synthetic. Nếu đặt `true` khi chưa tích hợp scanner production, mọi attachment sẽ bị quarantine fail-closed.

## Chuẩn bị database

1. Dùng database sandbox riêng hoặc backup database phát triển.
2. Chạy migration `AddSandboxMailboxIngestion`.
3. Không bật `Database__ApplyMigrationsOnStartup` trên hosting.

## Chạy smoke

Khởi động ứng dụng với mailbox sandbox bật, sau đó:

```powershell
.\tools\Invoke-SandboxMailboxPipeline.ps1 `
  -BaseUrl "https://localhost:5001" `
  -ApiKey $env:MailboxSandbox__ApiKey `
  -ImagePath ".\wwwroot\test_data\images\HoaDon1.jpg" `
  -RepeatDelivery
```

Kỳ vọng:

- delivery đầu trả HTTP 202/`ACCEPTED` và một `requestId`;
- job đi tới `COMPLETED` hoặc `FAILED`, không kẹt `PENDING`;
- delivery lặp lại cùng message/attachment trả `DUPLICATE` và cùng `requestId`;
- SQL chỉ có một `ReimbursementRequest` cho source key đó;
- audit có `AI_QUEUED` với `Source=SANDBOX_MAILBOX`;
- cursor, email message, attachment và outbox được lưu;
- file sai MIME/magic bytes được ghi `QUARANTINED` và không tạo AI job.

## Điều kiện không được bật trên live

- API key chưa đặt trong secret store;
- chưa dùng mailbox synthetic chuyên dụng;
- chưa apply migration có backup;
- endpoint chưa qua smoke unauthorized/sender/folder/duplicate/quarantine;
- định dùng dữ liệu thật khi chưa có malware scanner, pre-inference privacy control và retention policy.

## Nâng cấp Gmail sau này

Thay connector sandbox bằng Gmail OAuth/read-only + Pub/Sub/history cursor. Không đổi ingestion service, SQL queue, worker, semantic, privacy hoặc policy. OAuth credential chỉ thuộc connector/secret vault; model không được nhận token.
