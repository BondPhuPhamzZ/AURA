# Báo cáo triển khai sau feedback doanh nghiệp — 17/10/2026

## 1. Kết luận điều hành

AURA đã triển khai các thay đổi có thể kiểm soát trong cùng kiến trúc hiện hành: upload nhiều hóa đơn, bỏ ô nhập tiền để AI đọc dòng tổng cuối, dashboard/CSV theo kỳ, privacy triage fail-safe và mailbox sandbox synthetic. Sandbox không tạo endpoint AI thứ hai: manual upload và synthetic mail dùng chung application service, storage, SQL queue, worker, semantic/privacy/policy và audit. Gmail/OAuth thật vẫn ở roadmap.

Candidate `de101f2` đã được publish/recycle và smoke thật ngày 17/10. Source mới nhất đạt 206/206 test offline và có migration `AddSandboxMailboxIngestion`; cần backup, migrate, publish và smoke lại trước khi gọi mailbox sandbox/UX/multilingual guard mới là live.

## 2. Đối chiếu feedback và phần đã triển khai

| Feedback | Quyết định | Triển khai thực tế | Ranh giới |
|---|---|---|---|
| Upload nhiều hóa đơn | Làm ngay | UI nhận tối đa 10 JPG/PNG, tiếp nhận tuần tự qua cùng endpoint HTTP 202; mỗi file có case ID, SHA-256, SQL row, audit và status URL riêng | Đây là một lượt chọn nhiều ảnh, chưa phải `InvoiceBatch`/`batchId`; chưa nhận PDF; tối đa 5 MB/file |
| Nhận từ folder/email | Làm sandbox synthetic trước | API key, sender/folder allow-list, source idempotency, cursor, persisted quarantine và outbox; gọi cùng ingestion boundary | Không đọc Gmail thật; chưa OAuth/token vault/webhook/backfill/malware scanner production |
| Không bắt người dùng nhập số tiền | Làm ngay, fail-safe | UI đã bỏ ô nhập và không gửi `claimedAmount`. Worker dùng `totalAmount` đã qua JSON Schema và semantic validation | Backend giữ tham số cũ cho test kit/API lịch sử; không đoán khi AI thiếu final total |
| Báo cáo cho quản lý | Làm ngay | Tab Tổng quan cho chọn kỳ; KPI, bảng ngày/năm và CSV cùng lọc theo tháng/năm đã chọn | CSV không xuất raw AI facts; chưa phải BI/financial ledger |
| Nhận diện dữ liệu cá nhân | Làm ngay ở mức triage | AI chỉ gắn marker category; backend normalize, không lặp lại giá trị gốc; policy trả `ESCALATE_PRIVACY`; manager quyết định tiếp tục hay trả lại | Không phải kết luận pháp lý hoặc chứng nhận compliance |
| Giảm dữ liệu gửi provider | Làm ngay | OpenRouter request mặc định gửi `provider.data_collection=deny`; health công khai trạng thái cấu hình | Strict ZDR mặc định tắt tới khi route live được xác nhận tương thích |

## 3. Kiến trúc sau cập nhật

```text
1–10 JPG/PNG + metadata người gửi; UI không nhận số tiền
→ kiểm extension, MIME, magic bytes, 5 MB/file
→ private storage + SHA-256 + SQL PENDING + audit AI_QUEUED
→ HTTP 202/status URL cho từng ảnh; UI giữ danh sách job sau từng response
→ worker claim bằng lease; coordinator giữ AI concurrency = 1
→ OpenRouter/Qwen3.8 Flash, reasoning none, data_collection=deny
→ JSON Schema → DTO → semantic validation → tối đa một repair
→ amount resolver: AI_EXTRACTED / UNAVAILABLE cho UI; USER_PROVIDED chỉ cho API lịch sử
→ privacy classifier: category-only, không lưu giá trị gốc
→ PolicyDecisionEngine C#: AUTO_APPROVE hoặc ESCALATE_*
→ nhân viên chuyển tiếp → quản lý YES/NO/Undo → audit timeline
→ dashboard SQL theo kỳ được chọn → CSV cùng kỳ cho quản lý
```

Nguồn sandbox bổ sung đi vào trước bước validation: synthetic delivery → API key → sender/folder allow-list → unique message/attachment → quarantine hoặc cùng SQL queue. AI concurrency vẫn là một; các mail hợp lệ khác nằm trong hàng đợi.

Các boundary chính không đổi: model chỉ đọc facts; C# áp policy tất định; con người chịu trách nhiệm ngoại lệ. Privacy triage được đặt sau structured extraction và semantic validation, trước policy decision.

## 4. Cơ chế an toàn mới

- **Lượt nhiều ảnh không làm mất job đã nhận:** UI lưu `aura.pendingReceipts` ngay sau từng HTTP 202. Nếu file sau lỗi, các job trước vẫn nằm trong SQL queue và được resume/poll. Mỗi ảnh là một hồ sơ độc lập, không có atomic batch transaction.
- **Không tin số tiền mù:** UI không còn gửi số tiền. `ReceiptAmountResolver` chỉ nhận `facts.TotalAmount` lớn hơn 0 sau validation cho luồng demo; provenance được lưu vào `ExtractedFactsJson` và audit. Nhánh `USER_PROVIDED` được giữ để các test kit/API lịch sử không bị phá.
- **Privacy không dựa vào keyword quyết định:** model chỉ phát tín hiệu category bằng marker chuẩn; backend chỉ chấp nhận allow-list và có kiểm tra bảo thủ cho email/phone trong text đã trích xuất. Không lưu raw match trong warning.
- **Privacy fail-safe:** `RequiresPrivacyReview=true` dẫn đến `ESCALATE_PRIVACY`, không auto-approve. Fact inconsistency vẫn có ưu tiên cao hơn.
- **Provider boundary:** OpenRouter payload yêu cầu từ chối data collection. Không bật strict ZDR nếu chưa biết route có provider đủ điều kiện.
- **Dashboard không tạo source of truth mới:** chỉ project từ SQL hiện hành và đổi timestamp UTC sang giờ Việt Nam.
- **CSV phục vụ review, không rò facts thô:** report chỉ lấy trường vận hành cần thiết, không xuất `ExtractedFactsJson`; chuỗi do người dùng cung cấp được vô hiệu hóa ký tự đầu có thể kích hoạt công thức Excel.
- **Mailbox replay an toàn:** unique key trên mailbox/message/attachment khiến cùng delivery trả lại `requestId` cũ và không tạo job AI thứ hai.
- **Quarantine trước AI:** sai extension/MIME/magic bytes, quá kích thước hoặc yêu cầu malware scanner chưa có đều tạo record quarantine và không tạo `ReimbursementRequest`/outbox.
- **Outbox bền:** hồ sơ, audit và `RECEIPT_QUEUED` được commit cùng transaction; dispatcher chỉ đánh thức worker, còn SQL vẫn là source of truth nếu process recycle.

Theo pháp luật hiện hành, Luật Bảo vệ dữ liệu cá nhân 91/2025/QH15 và Nghị định 356/2025/NĐ-CP đều có hiệu lực từ 01/01/2026. Tính năng này chỉ hỗ trợ sàng lọc để con người xử lý theo quy trình doanh nghiệp; đánh giá tuân thủ đầy đủ vẫn cần pháp chế, data inventory, retention, quyền chủ thể dữ liệu và kiểm soát nhà cung cấp.

## 5. Bằng chứng kiểm thử

| Gate | Kết quả |
|---|---|
| `dotnet build --no-restore` | PASS — 0 warning, 0 error |
| Toàn bộ xUnit | PASS — 206/206 |
| Test mới | shared intake validator, mailbox guard, SQLite persistence/idempotency/quarantine/outbox, fail-closed scanner option và controller auth/status; cùng các test amount/privacy/dashboard trước đó |
| `dotnet ef migrations has-pending-model-changes --no-build` | PASS — model khớp migration `AddSandboxMailboxIngestion` |
| NuGet vulnerability scan | PASS — app và test project không có package bị báo vulnerable từ nguồn hiện hành |
| Sandbox smoke script parser | PASS — PowerShell syntax hợp lệ; live execution chờ migrate/publish/bật feature |
| Demo readiness pre-check | READY — 0 failure; warning LocalDB registry đã biết và HTTP chủ động skip |
| Live `/healthz` | PASS — HTTP 200; DB/storage/migration khỏe; Qwen3.8 Flash; reasoning none; data collection denied; concurrency 1; fallback tắt |
| Live Verify | PASS — 5/5, 0 system error, E2E 49,518 giây |
| Live no-amount smoke | PASS vận hành — 3/3 hoàn tất, HTTP 202, `AI_EXTRACTED`, 0 provider error; 2/3 khớp manifest cũ, TC-05 đi `ESCALATE_PRIVACY` thay vì `ESCALATE_POLICY` |
| Live input validation | PASS — 2/2 file giả MIME/magic bytes và file quá 5 MB bị HTTP 400 trước queue |
| Live human workflow | PASS — Forward, manager YES, Undo và immutable Audit |

Không dùng lỗi LocalDB của máy này để suy ra database live hỏng. Không xóa LocalDB registry/MDF khi chưa có backup. Cần publish và smoke trên hosting để xác nhận candidate live.

## 6. Quy trình demo mới đề xuất (4–5 phút)

1. **Mailbox sandbox (40 giây, nếu đã bật/smoke):** gửi một fixture synthetic bằng script, nhận HTTP 202; nói rõ đây là provider-neutral sandbox, không phải Gmail thật.
2. **Replay/idempotency (20 giây):** gửi lại đúng message/attachment, nhận `DUPLICATE` và cùng `requestId`.
3. **Xử lý nền (25 giây):** chỉ trạng thái PENDING/PROCESSING và việc người dùng có thể tiếp tục thao tác; AI concurrency vẫn là một.
4. **Kết quả và human boundary (45 giây):** chỉ facts/amount provenance, privacy/fact escalation, manager YES/NO và audit.
5. **Dashboard (25 giây):** chỉ rõ số liệu/CSV lấy từ SQL theo kỳ.
6. **Production reality (30 giây):** Gmail OAuth, malware scanner, pre-inference privacy, monitoring và capacity lớn chưa được claim.

Luôn chuẩn bị fallback: nếu provider chậm, chuyển sang dữ liệu/audit đã ghi trong SQL và trình bày ảnh/video baseline; không bấm Verify lặp lại để săn kết quả.

## 7. Checklist trước demo chiều nay

1. Commit và push candidate; ghi commit SHA vào log demo.
2. Backup hosting/database; apply migration mới bằng quy trình có rollback.
3. Giữ sandbox tắt hoặc đặt secret/allow-list đúng môi trường; không in API key.
4. Publish/recycle; chờ 10–30 giây.
5. `/healthz`: HTTP 200, `status=ok`, database/storage true, migration 0, model/reasoning đúng; kiểm đúng `mailboxSandboxEnabled`.
6. Nếu sandbox bật, chạy script smoke với `-RepeatDelivery`; nếu tắt, endpoint delivery phải trả 404.
7. Chạy một manual synthetic upload/privacy case để bảo toàn baseline cũ.
8. Không rerun holdout cũ để tạo claim accuracy mới; quay lại dashboard và rehearsal.

## 8. Việc chưa triển khai và hướng tiếp theo

- Gmail/Graph thật theo `docs/RECOMMENDED_INGESTION_WORKFLOW.md`: connector riêng giữ OAuth; scope read-only tối thiểu, token vault, revoke, webhook/backfill và malware scanner. Sandbox hiện đã có allow-list, cursor, idempotency và persisted quarantine nhưng không thay thế các gate này.
- Managed private object storage, encryption/KMS, retention/deletion policy và access logging.
- Authentication/SSO, RBAC thực, segregation of duties và audit identity thay cho demo identities.
- Distributed provider limiter/Verify lock/circuit state nếu chạy nhiều instance.
- Monitoring/alerting cho queue age, error rate, provider latency, privacy review backlog và capacity.
- Strict ZDR hoặc local VLM cho luồng nhạy cảm sau benchmark chất lượng/latency/capacity; không bật chỉ để tạo claim.
- User study ba người với protocol trước/sau đã khóa ngưỡng, giữ cả phản hồi tiêu cực.

## 9. Nguồn quyết định

- Feedback doanh nghiệp: `D:\aura\demo_Day\final_feedback_trienkhai_ngay\main_feedback\final feedback need to upgrade.txt`.
- Trạng thái kỹ thuật: `docs/CURRENT_PROJECT_STATUS.md`.
- Luồng vận hành: `docs/RUNBOOK.md`.
- Luật Bảo vệ dữ liệu cá nhân 91/2025/QH15: <https://vanban.chinhphu.vn/?docid=214590&pageid=27160>.
- Nghị định 356/2025/NĐ-CP: <https://vanban.chinhphu.vn/?docid=216387&pageid=27160&typegroupid=4>.

