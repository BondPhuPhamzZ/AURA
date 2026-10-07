# AURA — Playbook Chung kết onsite 17/10/2026

Cập nhật: 06/10/2026. Tài liệu này chuyển thông báo chính thức của BTC thành kế hoạch hành động. Đây không phải suy đoán đề onsite; yêu cầu và cách đánh giá chỉ được công bố trong lễ khai mạc ngày 17/10.

## 1. Kết luận chiến lược

Baseline AURA hiện đã đủ ổn định cho supervised demo. Từ nay đến Chung kết, không nên tự mở thêm PDF, auth/RBAC, GPU self-host hoặc một thay đổi dữ liệu lớn chỉ vì đoán đề. Lợi thế cần xây là khả năng nhận yêu cầu mới, xác định acceptance criteria, sửa đúng lớp, kiểm thử và trình bày một vertical slice hoàn chỉnh trong bốn giờ.

Ưu tiên ôn theo tỷ lệ tham khảo **60% nghiệp vụ/luồng quyết định và 40% code navigation/kiểm thử**. BGK cần nghe được vì sao hệ thống ra quyết định và cách kiểm soát rủi ro; phần code cần nắm theo bản đồ “đổi yêu cầu này thì sửa ở đâu”, không cần học thuộc từng dòng.

## 2. Mốc thời gian và hậu cần chính thức

| Giờ | Việc phải làm |
|---|---|
| 08:00–08:30 | Làm thủ tục, nhận đồng phục/thẻ và chụp ảnh tại sảnh E1. Mục tiêu có mặt 07:30–07:45 để có biên an toàn. |
| 08:30–09:00 | Khai mạc tại E1-02.10. Ghi nguyên văn yêu cầu mới, giới hạn, tiêu chí chấm và điều cấm. Không bắt đầu code từ diễn giải chưa xác nhận. |
| 09:00–09:30 | Ổn định kỹ thuật; Track VNG ở E2-01.01. Kiểm Git, .NET, database, OpenRouter, mạng, màn hình trình chiếu và local fallback. |
| 09:30–13:30 | Bốn giờ phát triển theo yêu cầu onsite. Ăn trưa tại chỗ nhưng vẫn dành một khoảng ngắn để nghỉ và rà quyết định. |
| 13:30–17:00 | Chấm vòng bảng, tối đa 15 phút mỗi đội. Chuẩn bị một bản trình bày 8 phút, khoảng 5 phút Q&A và 2 phút đệm. |
| 17:00–18:00 | Chung kết sáu đội. BTC chưa công bố thời lượng từng đội; chuẩn bị thêm bản nén 5 phút vì tổng khung chỉ 60 phút kể cả chuyển đội. |
| 18:30–19:30 | Trao giải tại E1-02.10. |

Không có thành viên nào có mặt đúng thời gian làm thủ tục có thể khiến đội bị loại trực tiếp. Nếu đã biết có tình huống bất khả kháng, phải email BTC trước ngày 10/10. Mặc áo BTC trong suốt phần thi và lễ trao giải.

## 3. Kế hoạch 11 ngày có giới hạn tải

Kế hoạch này cố ý giữ khối lượng AURA khoảng 45–90 phút trong ngày thường để còn chỗ cho các môn/project khác; chỉ hai buổi drill cần khối tập trung dài hơn.

- **06–08/10:** khóa baseline; hoàn thành điện thoại thật qua 4G/5G; xác minh local fallback; gom một thư mục trình chiếu offline. Không thêm feature lớn.
- **07–09/10:** đặt/chạy GPU BTC bằng v3.1 tổng hợp; mỗi ngày 45–60 phút ôn business flow và trace một request qua code. Chạy một mini drill 60 phút với yêu cầu giả.
- **10–11/10:** một drill 90 phút và một rehearsal; audit README, slide, Build Log, video/live link. Nếu BTC kết nối được người dùng thì thực hiện user session có consent.
- **12/10:** chạy full gate, chốt commit/demo data/tài liệu nộp. Sau mốc này chỉ sửa blocker có test hồi quy.
- **13/10:** kiểm repository/link từ thiết bị hoặc tài khoản khác, rehearsal 8 phút và chuẩn bị bản nén 5 phút.
- **14/10:** nộp và xác minh receipt/link trước hạn khóa; không để thao tác upload/link cuối cùng sát giờ.
- **15/10:** giữ submission baseline, chỉ luyện change drill onsite hoặc sửa blocker có thể chứng minh; không thay link đã nộp nếu BTC không cho phép.
- **16/10:** một dress rehearsal 8 phút và một bản nén 5 phút; sạc thiết bị, tải tài liệu offline, kiểm adapter/hotspot. Không nâng package hoặc đổi schema.
- **17/10:** đến sớm, nghe đề và dùng quy trình bốn giờ bên dưới.

Nếu lịch trường dồn, ưu tiên theo thứ tự: điện thoại 4G + local fallback, một full gate, hai drill thay đổi, ba rehearsal ngắn, rồi mới tới user session/P2. GPU/PDF/auth luôn đứng sau.

## 4. Những phần phải hiểu, không học thuộc

### Nghiệp vụ và sản phẩm

1. User chính: nhân viên nộp hoàn ứng và quản lý xử lý ngoại lệ; Audit là lớp truy vết.
2. VLM chỉ trích xuất facts; `PolicyDecisionEngine` mới có authority ra quyết định.
3. Thứ tự ưu tiên là `FACT → POLICY → AUTHORITY`; thiếu bằng chứng quan trọng phải fail-safe.
4. Phân biệt claimed amount của người dùng, extracted/printed total của hóa đơn và quyết định cuối.
5. Hiểu các nhánh AUTO, ESCALATE_FACT, ESCALATE_POLICY, ESCALATE_AUTHORITY và SYSTEM_ERROR; biết bước tiếp theo của người dùng ở mỗi nhánh.
6. Nói đúng evidence: 122 code tests không phải 122 hóa đơn; holdout raw 10/15, adjusted 11/15; không đổi raw và không gọi lượt chạy lại là blind.
7. Biết giới hạn hiện tại: ảnh JPG/JPEG/PNG một trang tối đa 5 MB; chưa claim PDF, auth/RBAC production, malware scan, object storage/retention hoặc production accuracy.

### Bản đồ code cần nhớ

| Khi yêu cầu thay đổi | Điểm vào cần xem | Kiểm thử tối thiểu |
|---|---|---|
| Cấu hình, health, provider | `Program.cs`, `Options/`, `/healthz` | health/config + startup failure case |
| Upload/status của nhân viên | `Controllers/ApplicantController.cs`, `Views/Home/Index.cshtml`, `wwwroot/css/site.css` | HTTP guard + mobile/manual flow |
| Queue/retry/persistence | `ReceiptProcessingQueue`, `ReceiptProcessingWorker`, backoff/lease | worker/reclaim/idempotency tests |
| Schema VLM hoặc field mới | `ReceiptExtractionDto`, `ReceiptExtractionContract`, vision service | contract parse + missing/invalid field |
| Evidence và semantic guard | `ReceiptSemanticValidator` | fail-open/fail-safe boundary tests |
| Policy/ngưỡng/quyết định | `BUSINESS_RULES.md`, `DecisionPolicyOptions`, `PolicyDecisionEngine` | decision precedence + boundary tests |
| Human workflow | `ReviewerController`, `EscalationWorkflow`, `WorkflowOperationGate` | approve/reject/undo/idempotency |
| Audit và thời gian | `AuditLogger`, `AuditTrailProjector`, Audit ViewComponent | event ordering + UTC/UTC+7 |
| Verify/regression | `VerifyController`, `tests/AURA.Tests` | targeted test rồi full suite |

Cách học nhanh: chọn một hồ sơ, kể thành tiếng đường đi `UI → Controller → SQL queue → Worker → VLM contract → Semantic guard → Policy → Workflow/Audit → UI`. Sau đó trả lời ba câu cho từng yêu cầu giả: **vì sao phải đổi, sửa ở đâu, test nào chứng minh**.

## 5. Quy trình bốn giờ onsite

| Thời gian | Kết quả bắt buộc |
|---|---|
| 09:30–09:50 | Chép yêu cầu thành Must/Should/Cut; hỏi lại điểm mơ hồ; viết 3–5 acceptance criteria đo được. |
| 09:50–10:10 | Chụp trạng thái baseline, tạo checkpoint/branch, chọn lớp bị tác động, viết test hoặc fixture thất bại đầu tiên. |
| 10:10–11:30 | Làm vertical slice nhỏ nhất đi xuyên từ input đến output; commit checkpoint khi core pass. |
| 11:30–12:10 | Nối UI, validation và error path; không polish phần không nằm trong acceptance criteria. |
| 12:10–12:45 | Chạy targeted tests, full suite, build/EF nếu liên quan; kiểm một negative case và một recovery path. |
| 12:45–13:05 | Nếu cần thì publish/smoke; lưu commit, timestamp, health, request/response và ảnh UI không lộ secret/PII. |
| 13:05–13:30 | Code freeze, rehearsal phần mới, chuẩn bị câu “đã làm/chưa làm/rủi ro còn lại”; không sửa thẩm mỹ phút cuối. |

Luôn giữ một người làm owner yêu cầu và đồng hồ. Nếu làm cá nhân, dùng một file `ONSITE_REQUIREMENT.md` với bốn mục: nguyên văn đề, acceptance criteria, quyết định scope, evidence. Chỉ đổi database schema khi đề bắt buộc và còn đủ thời gian migration + rollback + persistence test.

## 6. Router cho các dạng đề mới có thể gặp

Đây là bản đồ thích ứng, không phải danh sách feature cần làm trước:

- **Thêm rule/ngưỡng/loại chi:** cập nhật business rule, options/policy engine, boundary tests và lý do trong UI/Audit.
- **Thêm field cần đọc:** mở rộng DTO/contract/prompt, provenance, semantic validator, UI và contract tests; field mới không được âm thầm trở thành authority.
- **PDF/nhiều trang:** nếu đề bắt buộc, giới hạn scope rõ ràng; cần MIME/signature validation, page/size limit, raster hóa, provenance theo trang và negative case. Không chỉ đổi `accept=.pdf`.
- **Role/quyền truy cập:** phân biệt role demo với authentication/RBAC thật; không claim bảo mật production nếu chưa có identity/authorization test.
- **Export/reporting:** ưu tiên dữ liệu đã chuẩn hóa/Audit; mặc định không xuất ảnh gốc hoặc PII.
- **Model/GPU/provider mới:** đi qua adapter/config hiện có; đo latency, failure và decision regression; không để model thay policy.
- **Concurrency/performance:** đo queue/lease/idempotency và P50/P95; không tối ưu cảm tính.
- **Mobile/UI:** sửa Razor/CSS/JS theo breakpoint và kiểm 360–430 px; giữ backend contract nếu đề không yêu cầu đổi.

## 7. Kịch bản 15 phút và bản nén Chung kết

### Vòng bảng: chuẩn bị 8 + 5 + 2

- 0:00–0:45: vấn đề và user.
- 0:45–1:30: AI đọc facts, policy quyết định, con người xử lý ngoại lệ.
- 1:30–4:15: demo một luồng chính và Audit/Undo.
- 4:15–5:00: yêu cầu onsite, quyết định scope và phần đã thay đổi.
- 5:00–6:00: kiến trúc/fail-safe/persistence.
- 6:00–7:15: evidence, holdout và giới hạn trung thực.
- 7:15–8:00: phản hồi user/roadmap/kết luận.
- 8:00–13:00: Q&A; 13:00–15:00 là buffer theo điều phối thực tế.

### Chung kết: bản nén 5 phút dự phòng

Không suy đoán đây là giới hạn chính thức. Dùng khi BTC yêu cầu rút gọn: 30 giây vấn đề, 45 giây nguyên tắc, 2 phút demo, 45 giây feature onsite, 40 giây evidence, 20 giây kết luận.

## 8. Demo kit và phương án sự cố

- Laptop, sạc, hotspot/điện thoại, adapter trình chiếu, chuột và ổ lưu trữ nếu quy chế cho phép.
- Slide/PDF offline, source đúng commit, package đã restore, database local đã migrate và fixture tổng hợp.
- Live URL là đường chứng minh bàn giao; localhost là fallback. Nếu live chậm, chuyển fallback sau 30–45 giây thay vì debug hosting trước BGK.
- Một video/screen recording 60–90 giây và ảnh evidence cho luồng cốt lõi; không dùng chúng để che một baseline đang lỗi.
- Không hiện environment variables, API key, mật khẩu DB, dashboard hosting hoặc hóa đơn thật chưa được phép.
- Trước demo kiểm health, model/provider, fallback state, số dư/quota, giờ hệ thống, độ phân giải và tab trình duyệt.

## 9. Evidence onsite

Nếu được phép dùng máy và thư mục local, lưu theo cấu trúc:

```text
17_onsite_2026-10-17/
  00_official_requirement/
  01_baseline_before_change/
  02_acceptance_and_scope/
  03_implementation_tests/
  04_runtime_smoke/
  05_demo_and_limits/
```

Tối thiểu ghi: nội dung đề chính thức, commit trước/sau, test liên quan, build status, health/runtime response, screenshot luồng mới, lỗi còn lại và quyết định cắt scope. Không đưa secret hoặc PII vào Git.

## 10. Điều kiện GO và các việc không nên chen vào

GO khi phone 4G không tràn/nháy sai tab, local fallback chạy, full gate sạch, ít nhất một rehearsal 8 phút và bản nén 5 phút sạch, đúng commit/demo data đã khóa. Ba user session giúp development story nhưng nếu phụ thuộc BTC chưa có lịch, phải nói rõ dependency thay vì tự tạo evidence.

Không chen vào baseline trước Chung kết nếu không do đề onsite yêu cầu: PDF production, auth/RBAC đầy đủ, đổi model primary, bật Ollama fallback, migration lớn, major package upgrade hoặc chạy lại bộ blind holdout cũ để làm đẹp accuracy.

