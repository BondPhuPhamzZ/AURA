# Checklist kiến trúc sau feedback doanh nghiệp

**Snapshot kiến trúc:** 17/10/2026; các trạng thái hiện hành được đối chiếu lại 24/10/2026.
**Nguồn đối chiếu:** `Hackathon_2h30_Implementation_Recommend.docx` trong thư mục `recommend_check`  
**Trạng thái tại snapshot:** candidate `de101f2` đã live-smoke; UX follow-up đã qua gate offline và chờ publish lại. Đây không phải trạng thái publish hiện hành; xem [CURRENT_PROJECT_STATUS.md](CURRENT_PROJECT_STATUS.md).

## 1. Quyết định chốt

AURA giữ pipeline hiện tại và không thêm `InvoiceBatch`, OAuth mailbox, agent tự trị hoặc distributed infrastructure ngay trước demo. Lượt chọn nhiều ảnh là tiện ích intake phía UI: mỗi ảnh vẫn tạo một hồ sơ độc lập, nhận một HTTP 202, status URL, SQL row, lease và audit riêng. Đây không phải batch transaction có `batchId` ở server.

Quyết định này giữ được các invariant đã đo, tránh migration gấp và không tạo một đường xử lý thứ hai. `InvoiceBatch` chỉ nên thêm khi doanh nghiệp thật sự cần theo dõi tiến độ cả lô, retry cả lô, báo cáo theo lô hoặc idempotency theo nguồn.

## 2. Đối chiếu 10 architecture gates

| Gate | Trạng thái | Bằng chứng hiện tại | Việc còn thiếu trước production |
|---|---|---|---|
| Một ingestion boundary | Đạt | Tất cả ảnh thủ công đi qua `ApplicantController.UploadReceipt`, cùng kiểm file, storage, SQL queue và worker | Mailbox/folder tương lai phải gọi application service chung, không bypass pipeline |
| SQL là source of truth | Đạt | Job, status, facts, quyết định và audit nằm trong SQL; dashboard/report đọc repository | Thêm backup/restore, retention và read model nếu tải lớn |
| Agent không có authority | Đạt | Model chỉ trả facts; `PolicyDecisionEngine` chọn route; nhân viên/quản lý xử lý ngoại lệ | Thêm SSO/RBAC và actor identity thật |
| Extraction integrity | Đạt có điều kiện | JSON Schema, semantic validation, tối đa một repair; thiếu/mâu thuẫn chuyển `ESCALATE_FACT` | Mở rộng evaluation độc lập, không nới guard bằng keyword sát demo |
| Idempotency | Một phần, cập nhật 24/10 | Drive sandbox dùng source key ổn định thành request ID; SQL primary key phân xử hai intake đồng thời. LocalDB race test: hai intake độc lập cùng source tạo đúng một request, audit và file; SQL rollback không bị báo thành công giả. | Chỉ áp dụng cho Drive sandbox/source-key input. Manual upload vẫn nhận ID mới mỗi lần; SHA-256 chỉ gắn cờ trùng, không phải idempotency. Cần chính sách source identity/idempotency cho từng nguồn production và kiểm thử GCS/host thật. |
| Quarantine / malware | Một phần | Extension, MIME, magic bytes và 5 MB chặn trước VLM; file sai bị HTTP 400. Drive sandbox từ chối chạy khi `RequireMalwareScanner=true` vì chưa tích hợp scanner. | Manual upload chưa có malware scan hoặc quarantine record. Format checks không thay thế antivirus. |
| Privacy | Một phần, fail-safe | Category allow-list, backend email/phone checks, không lặp raw match, `ESCALATE_PRIVACY`, `data_collection=deny` | Triage chạy sau VLM nên không ngăn ảnh nhạy cảm rời trust boundary. Production cần pre-inference redaction/local VLM, strict ZDR đã xác minh, RBAC và retention |
| Failure visibility | Đạt | `FAILED`/`ESCALATE_SYSTEM_ERROR`, error code, audit và status URL; dashboard candidate không hiển thị số 0 giả khi SQL lỗi | Monitoring/alerting cho queue age, latency, error rate và backlog |
| Scope discipline | Đạt | Không nối OAuth/mailbox gấp; không đổi model, policy hoặc evidence contract | Chỉ mở connector khi có token vault, least privilege, cursor, revoke, allow-list và quarantine |
| Demo honesty | Đạt | Candidate và baseline live được ghi tách biệt; mailbox/evidence overlay đều gắn roadmap/prototype | Sau publish phải cập nhật evidence bằng đúng commit và không dùng video cũ để claim UI mới |

## 3. Kiến trúc hiện tại khác baseline trước feedback

```text
TRƯỚC
1 JPG/PNG + claimed amount nhập thủ công
→ HTTP 202 → SQL queue → VLM → semantic → policy → human → audit

HIỆN TẠI TRONG SOURCE CANDIDATE
1–10 JPG/PNG trong một lượt chọn + metadata; không có ô amount
→ mỗi ảnh kiểm độc lập và nhận HTTP 202/status URL riêng
→ private storage + SHA-256 + SQL PENDING + audit AI_QUEUED
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

## 5. Evidence thực tế ngày 17/10/2026

| Kiểm tra | Kết quả | Cách diễn giải |
|---|---|---|
| Release xUnit | 190/190 pass | UX và multilingual fail-safe qua regression offline; không gọi AI API trả phí |
| EF migration gate | Không có model change | Mailbox sandbox và migration thử nghiệm đã được gỡ khỏi candidate |
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

1. Publish candidate hiện tại, smoke single + multi-file + privacy + CSV; freeze nếu pass.
2. Thêm pre-inference privacy boundary hoặc local VLM cho dữ liệu nhạy cảm; benchmark trước khi đổi route.
3. Mở rộng idempotency theo source/transport production (manual upload hiện chưa idempotent); triển khai persisted quarantine và malware scan trong hạ tầng được kiểm soát.
4. Chỉ khi có nhu cầu vận hành thật mới thêm `InvoiceBatch` và batch status API.
5. Sau đó mới triển khai mailbox connector với OIDC/OAuth, token vault, least privilege, sync cursor, revoke và audit.
6. Cuối cùng mới scale multi-instance bằng distributed limiter/lock/circuit state và managed queue/object storage.

## 8. Điều kiện GO NO GO

**GO cho demo có giám sát** sau khi candidate rollback được publish đúng commit, Release build sạch, 190/190 test pass, không có pending migration, `/healthz` khỏe và smoke lại dashboard/CSV cùng một upload synthetic.

**NO GO cho claim production-ready** khi chưa có SSO/RBAC, pre-inference privacy control, strict ZDR/local route đã đo, source-specific idempotency cho mọi input được bật, persisted quarantine/malware scan, managed storage, retention, monitoring và distributed coordination.
