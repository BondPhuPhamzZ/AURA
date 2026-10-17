# Checklist kiến trúc sau feedback doanh nghiệp

**Cập nhật:** 17/10/2026  
**Nguồn đối chiếu:** `Hackathon_2h30_Implementation_Recommend.docx` trong thư mục `recommend_check`  
**Trạng thái:** baseline `de101f2` đã live-smoke; source mới có UX follow-up + mailbox sandbox, đạt gate offline và chờ migrate/publish/smoke

## 1. Quyết định chốt

AURA giữ pipeline hiện tại và không thêm `InvoiceBatch`, OAuth Gmail, agent tự trị hoặc distributed infrastructure. Lượt chọn nhiều ảnh là tiện ích intake phía UI. Mailbox sandbox synthetic chỉ bổ sung một nguồn vào đã giới hạn; nó không tạo pipeline AI thứ hai và không kết nối mailbox thật.

Quyết định này giữ được các invariant đã đo, tránh migration gấp và không tạo một đường xử lý thứ hai. `InvoiceBatch` chỉ nên thêm khi doanh nghiệp thật sự cần theo dõi tiến độ cả lô, retry cả lô, báo cáo theo lô hoặc idempotency theo nguồn.

## 2. Đối chiếu 10 architecture gates

| Gate | Trạng thái | Bằng chứng hiện tại | Việc còn thiếu trước production |
|---|---|---|---|
| Một ingestion boundary | Đạt | Manual upload và mailbox sandbox cùng gọi `IInvoiceIngestionService`, dùng chung validator, storage, SQL queue và worker | Gmail/Graph tương lai phải thay adapter sandbox, không bypass service |
| SQL là source of truth | Đạt | Job, status, facts, quyết định và audit nằm trong SQL; dashboard/report đọc repository | Thêm backup/restore, retention và read model nếu tải lớn |
| Agent không có authority | Đạt | Model chỉ trả facts; `PolicyDecisionEngine` chọn route; nhân viên/quản lý xử lý ngoại lệ | Thêm SSO/RBAC và actor identity thật |
| Extraction integrity | Đạt có điều kiện | JSON Schema, semantic validation, tối đa một repair; thiếu/mâu thuẫn chuyển `ESCALATE_FACT` | Mở rộng evaluation độc lập, không nới guard bằng keyword sát demo |
| Idempotency | Đạt cho sandbox | Unique DB `(connection,message)` và `(message,attachment)`; replay trả `requestId` cũ, không tạo job mới. Manual vẫn dùng SHA-256 signal | Gmail thật cần provider event semantics, retention và concurrency/capacity test |
| Quarantine | Đạt có điều kiện cho sandbox | Attachment sai extension/MIME/magic bytes/size được persist quarantine trước AI; scanner-required chưa có thì fail-closed | Chưa có malware scanner production, archive/PDF sanitizer hay quarantine object store riêng |
| Privacy | Một phần, fail-safe | Category allow-list, backend email/phone checks, không lặp raw match, `ESCALATE_PRIVACY`, `data_collection=deny` | Triage chạy sau VLM nên không ngăn ảnh nhạy cảm rời trust boundary. Production cần pre-inference redaction/local VLM, strict ZDR đã xác minh, RBAC và retention |
| Failure visibility | Đạt | `FAILED`/`ESCALATE_SYSTEM_ERROR`, error code, audit và status URL; dashboard candidate không hiển thị số 0 giả khi SQL lỗi | Monitoring/alerting cho queue age, latency, error rate và backlog |
| Scope discipline | Đạt | Sandbox mặc định tắt, API key/allow-list; không OAuth/model/policy change | Chỉ mở Gmail khi có token vault, least privilege, revoke, webhook/backfill và scanner |
| Demo honesty | Đạt | Status nói rõ `synthetic-sandbox-only`, `gmailConnected=false`, `productionReady=false` | Sau publish phải cập nhật evidence bằng đúng commit và không gọi offline test là live Gmail evidence |

## 3. Kiến trúc hiện tại khác baseline trước feedback

```text
TRƯỚC
1 JPG/PNG + claimed amount nhập thủ công
→ HTTP 202 → SQL queue → VLM → semantic → policy → human → audit

HIỆN TẠI TRONG SOURCE CANDIDATE
manual 1–10 JPG/PNG hoặc synthetic mailbox delivery; không có ô amount
→ sandbox API key + sender/folder allow-list + source idempotency
→ mỗi ảnh kiểm độc lập; invalid mail persist quarantine, valid mail nhận HTTP 202/status URL
→ private storage + SHA-256 + SQL PENDING + audit AI_QUEUED
→ transactional outbox đánh thức worker; SQL vẫn là source of truth
→ worker lease + AI concurrency 1
→ Qwen structured facts + semantic validation + một repair
→ amount provenance: AI_EXTRACTED / UNAVAILABLE trong UI
→ privacy category triage, không lặp giá trị gốc
→ C# policy → human authority → audit
→ dashboard SQL theo kỳ được chọn + CSV cùng kỳ
```

Các phần không đổi: SQL queue bền, model không tự phê duyệt, policy C# tất định, human review cho ngoại lệ, AI concurrency một trong mỗi process và Verify lock theo process.

## 4. Thay đổi UX/hardening đã chốt sau recommend check

- Gọi đúng tên **lượt chọn nhiều ảnh** và nói rõ mỗi ảnh là một hồ sơ độc lập; không trình bày như server batch có `batchId`.
- Dashboard dùng nhãn **Đã kết thúc** cho tập `COMPLETED + FAILED`, đồng thời tách số lỗi riêng.
- Tổng tiền trên dashboard được ghi rõ là **tiền tham chiếu**, không phải tổng đã phê duyệt hay financial ledger.
- Nếu SQL không tải được, dashboard hiển thị trạng thái không khả dụng, ẩn KPI/CSV và không dùng các số 0 giả.
- Khu vực upload nhắc dùng dữ liệu tổng hợp/đã ẩn danh vì privacy triage hiện chạy sau extraction.
- Khu vực upload đã bỏ ô nhập số tiền và không gửi `claimedAmount`; backend chỉ giữ nhánh `USER_PROVIDED` cho test kit/API lịch sử.
- Dashboard cho chọn kỳ; tải CSV cùng kỳ; refresh dùng icon. Lối tắt quay về hồ sơ đã bỏ vì các tab chính đã đảm nhiệm điều hướng.
- Hai bảng dashboard có vùng cuộn, header sticky và đường kẻ tương phản hơn.
- Mailbox sandbox có status endpoint công khai ranh giới, delivery endpoint khóa bằng secret header và script smoke kiểm replay.

## 5. Evidence thực tế ngày 17/10/2026

| Kiểm tra | Kết quả | Cách diễn giải |
|---|---|---|
| Release xUnit | 203/203 pass | UX/mailbox regression offline; không gọi AI API trả phí |
| EF migration gate | Model khớp `AddSandboxMailboxIngestion` | Migration chưa được apply trên live |
| Live `GET /healthz` lúc 12:54 ICT | HTTP 200, DB/storage/migration khỏe, Qwen3.8 Flash, reasoning none, data collection denied, concurrency 1 | Candidate sau feedback đã live |
| Live no-amount synthetic upload | 3/3 hoàn tất; 0 provider/system error; 2/3 khớp manifest cũ | TC-05 chuyển bảo thủ sang privacy; không ghi 3/3 exact |
| Live Verify Harness | 5/5, E2E 49,518 giây, 0 system error | Bộ Verify vẫn ổn sau publish |
| Live workflow | Forward → YES → Undo → Audit pass | Human authority và audit hoạt động thật |
| Live upload validation | 2/2 ca xấu bị HTTP 400 trước queue | Magic bytes và giới hạn 5 MB hoạt động |
| LocalDB | Không khởi tạo được do lỗi user-instance registry của máy | Không xóa registry/MDF; không dùng lỗi local để suy ra live DB hỏng |
| SQL Server mặc định local | Service chạy nhưng test database không đăng nhập được do TLS/SSPI của máy | Không hạ bảo mật hoặc đổi cấu hình hệ thống sát demo |
| In-app browser | Bị Work settings vô hiệu hóa | Không có browser evidence giả; dùng API read-only + test/source audit |

## 6. Các bước demo candidate sau khi publish

1. Mở `/healthz`; kiểm HTTP 200 và có `providerDataCollectionDenied=true`.
2. Mở **Tổng quan**; chọn kỳ, chỉ rõ SQL source of truth, nhãn tiền tham chiếu và CSV cùng kỳ.
3. Trở lại **Cửa sổ nhân viên**; chọn 2–3 ảnh tổng hợp/đã ẩn danh và chỉ metadata người gửi; xác nhận không còn ô amount.
4. Nói rõ mỗi ảnh nhận job/status URL riêng; không gọi đây là batch transaction.
5. Chỉ một ca amount AI đủ evidence, một ca `ESCALATE_FACT`, một ca `ESCALATE_PRIVACY` nếu có fixture phù hợp.
6. Chuyển tiếp ngoại lệ, quản lý YES/NO, rồi mở Audit.
7. Tải CSV, xác nhận không có raw `ExtractedFactsJson` và số dashboard khớp SQL.

## 7. Thứ tự phát triển tiếp theo

1. Backup, migrate, publish source hiện tại; smoke manual baseline trước.
2. Bật sandbox trong môi trường riêng và smoke accepted/replay/quarantine/unauthorized; tắt lại nếu public demo không cần.
3. Thêm malware scanner production và pre-inference privacy boundary/local VLM; benchmark trước khi dùng dữ liệu thật.
4. Sau đó mới triển khai Gmail/Graph adapter với OAuth/token vault/least privilege/revoke/webhook/backfill.
5. Chỉ khi có nhu cầu vận hành thật mới thêm `InvoiceBatch` và batch status API.
6. Cuối cùng scale multi-instance bằng distributed limiter/lock/circuit state, managed queue/object storage và capacity test.

## 8. Điều kiện GO NO GO

**GO cho demo mailbox sandbox có giám sát** sau khi backup/migration thành công, source đúng commit được publish, Release build sạch, 203/203 test pass, `/healthz` có migration 0 và smoke script xác nhận ACCEPTED → terminal + DUPLICATE cùng request. Nếu chưa đạt đủ, giữ sandbox tắt và demo manual baseline.

**NO GO cho claim production-ready/Gmail-connected** khi chưa có OAuth/token vault/revoke, SSO/RBAC, malware scanner, pre-inference privacy control, strict ZDR/local route đã đo, managed storage, retention, monitoring, capacity và distributed coordination.
