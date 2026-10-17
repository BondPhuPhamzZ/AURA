# Báo cáo triển khai sau feedback doanh nghiệp — 17/10/2026

## 1. Kết luận điều hành

AURA đã triển khai năm thay đổi có thể kiểm soát trong cùng kiến trúc hiện hành: upload nhiều hóa đơn, bỏ ô nhập tiền để AI đọc dòng tổng cuối, dashboard ngày/tháng/năm, báo cáo CSV tháng hiện tại và privacy triage fail-safe. Không tạo endpoint AI thứ hai, không thay database schema, không thay model, evidence contract version hay các business rule phê duyệt hiện có. Connector email/folder được giữ ở roadmap vì triển khai gấp mà thiếu OIDC/OAuth, least privilege, consent, idempotency và quarantine sẽ làm giảm an toàn production.

Candidate source hiện đạt build 0 warning/0 error, 181/181 test offline và không có pending EF migration. Baseline live 5/5 ngày 14/10 vẫn là bằng chứng của phiên bản trước feedback. Candidate ngày 17/10 chưa được gọi là live cho tới khi publish/recycle, `/healthz` và synthetic smoke hoàn tất.

## 2. Đối chiếu feedback và phần đã triển khai

| Feedback | Quyết định | Triển khai thực tế | Ranh giới |
|---|---|---|---|
| Upload nhiều hóa đơn | Làm ngay | UI nhận tối đa 10 JPG/PNG, tiếp nhận tuần tự qua cùng endpoint HTTP 202; mỗi file có case ID, SHA-256, SQL row, audit và status URL riêng | Đây là một lượt chọn nhiều ảnh, chưa phải `InvoiceBatch`/`batchId`; chưa nhận PDF; tối đa 5 MB/file |
| Nhận từ folder/email | Chưa nối gấp | Giữ cùng ingestion boundary làm điểm tích hợp tương lai | Cần OIDC/OAuth, least privilege, consent, idempotency, backfill, quarantine và audit |
| Không bắt người dùng nhập số tiền | Làm ngay, fail-safe | UI đã bỏ ô nhập và không gửi `claimedAmount`. Worker dùng `totalAmount` đã qua JSON Schema và semantic validation | Backend giữ tham số cũ cho test kit/API lịch sử; không đoán khi AI thiếu final total |
| Báo cáo cho quản lý | Làm ngay | Tab Tổng quan chiếu dữ liệu SQL theo ngày, tháng và năm; tải CSV tháng hiện tại có route, privacy, trạng thái quản lý và latency | CSV không xuất raw AI facts; chưa phải BI/financial ledger |
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
→ dashboard SQL theo ngày / tháng / năm → CSV tháng cho quản lý
```

Các boundary chính không đổi: model chỉ đọc facts; C# áp policy tất định; con người chịu trách nhiệm ngoại lệ. Privacy triage được đặt sau structured extraction và semantic validation, trước policy decision.

## 4. Cơ chế an toàn mới

- **Lượt nhiều ảnh không làm mất job đã nhận:** UI lưu `aura.pendingReceipts` ngay sau từng HTTP 202. Nếu file sau lỗi, các job trước vẫn nằm trong SQL queue và được resume/poll. Mỗi ảnh là một hồ sơ độc lập, không có atomic batch transaction.
- **Không tin số tiền mù:** UI không còn gửi số tiền. `ReceiptAmountResolver` chỉ nhận `facts.TotalAmount` lớn hơn 0 sau validation cho luồng demo; provenance được lưu vào `ExtractedFactsJson` và audit. Nhánh `USER_PROVIDED` được giữ để các test kit/API lịch sử không bị phá.
- **Privacy không dựa vào keyword quyết định:** model chỉ phát tín hiệu category bằng marker chuẩn; backend chỉ chấp nhận allow-list và có kiểm tra bảo thủ cho email/phone trong text đã trích xuất. Không lưu raw match trong warning.
- **Privacy fail-safe:** `RequiresPrivacyReview=true` dẫn đến `ESCALATE_PRIVACY`, không auto-approve. Fact inconsistency vẫn có ưu tiên cao hơn.
- **Provider boundary:** OpenRouter payload yêu cầu từ chối data collection. Không bật strict ZDR nếu chưa biết route có provider đủ điều kiện.
- **Dashboard không tạo source of truth mới:** chỉ project từ SQL hiện hành và đổi timestamp UTC sang giờ Việt Nam.
- **CSV phục vụ review, không rò facts thô:** report chỉ lấy trường vận hành cần thiết, không xuất `ExtractedFactsJson`; chuỗi do người dùng cung cấp được vô hiệu hóa ký tự đầu có thể kích hoạt công thức Excel.

Theo pháp luật hiện hành, Luật Bảo vệ dữ liệu cá nhân 91/2025/QH15 và Nghị định 356/2025/NĐ-CP đều có hiệu lực từ 01/01/2026. Tính năng này chỉ hỗ trợ sàng lọc để con người xử lý theo quy trình doanh nghiệp; đánh giá tuân thủ đầy đủ vẫn cần pháp chế, data inventory, retention, quyền chủ thể dữ liệu và kiểm soát nhà cung cấp.

## 5. Bằng chứng kiểm thử

| Gate | Kết quả |
|---|---|
| `dotnet build --no-restore` | PASS — 0 warning, 0 error |
| Toàn bộ xUnit | PASS — 181/181 |
| Test mới | amount resolver, privacy classifier/policy, provider payload, dashboard projector, batch/UI contract, privacy manager outcome, CSV scope/formula hardening/UTF 8 BOM |
| `dotnet ef migrations has-pending-model-changes --no-build` | PASS — không có model change |
| Demo readiness pre-check | READY — 0 failure; warning LocalDB registry đã biết và HTTP chủ động skip |
| Local `/healthz` | 503 do LocalDB user-instance của máy hiện tại không khởi tạo được; đồng thời xác nhận AI/policy/storage và privacy flags đúng |

Không dùng lỗi LocalDB của máy này để suy ra database live hỏng. Không xóa LocalDB registry/MDF khi chưa có backup. Cần publish và smoke trên hosting để xác nhận candidate live.

## 6. Quy trình demo mới đề xuất (4–5 phút)

1. **Tổng quan (25 giây):** mở dashboard, chỉ rõ số liệu lấy từ SQL ở ba mức ngày/tháng/năm và nút tải báo cáo CSV.
2. **Multi-file intake (35 giây):** chọn 2–3 receipt JPG/PNG rồi bấm “Xếp hàng AI”; không còn bước nhập số tiền. Nhấn mạnh mỗi ảnh nhận HTTP 202 và job riêng; không gọi đây là server batch có `batchId`.
3. **Xử lý nền (25 giây):** chỉ trạng thái PENDING/PROCESSING và việc người dùng có thể tiếp tục thao tác.
4. **Amount provenance (25 giây):** mở facts, chỉ “Nguồn số tiền: AI trích xuất” và final total; nếu thiếu total thì fail-safe.
5. **Privacy boundary (45 giây):** dùng receipt synthetic có category dữ liệu cá nhân, chỉ `ESCALATE_PRIVACY`; hệ thống nêu category nhưng không lặp lại giá trị gốc.
6. **Human authority (30 giây):** nhân viên chuyển tiếp, quản lý YES/NO; nói rõ AI không tự cấp quyền xử lý dữ liệu cá nhân.
7. **Audit + dashboard (30 giây):** mở timeline và refresh dashboard để thấy route, privacy review và tổng hợp vận hành.
8. **Production reality (30 giây):** nói rõ `data_collection=deny`, ZDR cần route validation, email connector cần identity/least privilege/quarantine, và candidate hôm nay chưa được gọi là live trước publish/smoke.

Luôn chuẩn bị fallback: nếu provider chậm, chuyển sang dữ liệu/audit đã ghi trong SQL và trình bày ảnh/video baseline; không bấm Verify lặp lại để săn kết quả.

## 7. Checklist trước demo chiều nay

1. Commit và push candidate; ghi commit SHA vào log demo.
2. Backup hosting/database và kiểm biến môi trường; không in API key.
3. Publish/recycle; chờ 10–30 giây.
4. `/healthz`: HTTP 200, `status=ok`, database/storage true, migration 0, model/reasoning đúng, `providerDataCollectionDenied=true`.
5. Chạy một synthetic single upload và xác nhận UI không có ô amount; sau đó một lượt chọn nhỏ 2 ảnh.
6. Chạy một privacy synthetic case và kiểm route `ESCALATE_PRIVACY`/manager/audit.
7. Không rerun holdout cũ để tạo claim accuracy mới. Live Verify chỉ chạy nếu credit, thời gian và provider ổn định.
8. Quay lại dashboard và rehearsal theo đúng thứ tự demo mới.

## 8. Việc chưa triển khai và hướng tiếp theo

- Email/folder ingestion theo `docs/RECOMMENDED_INGESTION_WORKFLOW.md`: Agent chỉ điều phối; connector riêng giữ OAuth; scope tối thiểu, allow-list sender/folder, sync cursor, idempotency key, malware scan/quarantine và webhook/backfill.
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

