# Trạng thái hiện hành AURA ngày 22/10/2026

> Source 22/10 có **Drive synthetic sandbox Development-only**, tắt mặc định,
> không thêm EF migration. Manual upload và Drive staging cùng dùng
> ReceiptIntakeService; Drive chỉ đọc file con trực tiếp của folder test riêng,
> chống trùng bằng request ID tất định theo file ID + version. **E2E local thật**
> đã đọc Google Drive qua service account, tạo SQL job và gọi OpenRouter:
> 3/3 lần xử lý job synthetic hoàn tất (2 fixture khác nhau, một fixture chạy
> lại để xác nhận sửa lỗi), một file `.txt` bị chặn, restart không tạo trùng.
> Fixture thứ hai lộ false positive `TaxId` giống PHONE; backend đã sửa và
> chạy lại bằng file ID mới ra `AUTO_APPROVE`, vẫn giữ audit cũ.
> `/healthz` local `status=ok`, migration 0, dashboard 200. Connector đã tắt
> lại sau test; **bản live chưa publish lại, không nhận dữ liệu thật**.
> Rejection log được hardening để cùng source-version/reason chỉ ghi một lần;
> vượt giới hạn scan 100 entries cũng fail-closed, không intake một phần.
> Hiện **216/216** Release tests pass, build 0 warning/error, EF không pending
> model changes. Xem [evidence Drive local](DRIVE_SANDBOX_EVIDENCE_TEMPLATE.md),
> [checklist/GO-NO-GO](DRIVE_SANDBOX_CHECKLIST.md) và [runbook](DRIVE_SANDBOX_LOCAL.md).

> Bản source sau cuộc thi có thêm gate Verify chỉ dành cho Development và rate limit HTTP trong một process; 196/196 offline tests, Release build 0 warning và EF model không đổi. **Chưa publish/smoke bản hardening này trên live; chưa sẵn sàng cho dữ liệu người dùng thật.** Xem [báo cáo hardening 21/10](POST_CONTEST_HARDENING_2026-10-21.md). Các số live bên dưới là evidence lịch sử của bản trước, không xác nhận hành vi bản mới.

## Kết luận

Candidate sau feedback tại commit `de101f2` đã được publish và smoke trên live ngày 17/10: health HTTP 200, Verify 5/5, ba upload synthetic đều hoàn tất và workflow Forward → YES → Undo → Audit đi hết luồng. Source sau đó có một UX follow-up: rút gọn intake, cho chọn kỳ dashboard/CSV và tăng tương phản/scroll bảng; phần UX này cần publish lại trước khi gọi là live. Mỗi ảnh vẫn là một hồ sơ độc lập; hệ thống chưa có `InvoiceBatch`/`batchId` phía server. Video cũ chỉ là bằng chứng của baseline trước feedback.

## Cấu hình live

| Thành phần | Giá trị hiện hành |
|---|---|
| Provider gateway | OpenRouter |
| Model | `qwen/qwen3.8-flash` |
| Route | Alibaba |
| Reasoning | `none` |
| Processing | Durable SQL background queue |
| AI concurrency | 1 trong mỗi process |
| Provider cooldown | 120 giây |
| Verify Harness | Bản source mới mặc định tắt; chỉ cho phép trong Development khi bật rõ ràng. Live cũ từng bật, cần publish bản mới để khóa. |
| Live fallback | Tắt; Ollama chỉ là contingency local |
| Lượt chọn nhiều ảnh | Tối đa 10 JPG/PNG; mỗi ảnh tối đa 5 MB và tạo một job/status URL riêng, không có batch transaction phía server |
| Amount | UI không có ô nhập; worker dùng `totalAmount` AI trích xuất sau validation. `USER_PROVIDED` chỉ còn cho test kit/API lịch sử |
| Provider privacy | `data_collection=deny`; strict ZDR mặc định tắt chờ xác nhận route live |
| Privacy route | `ESCALATE_PRIVACY`; triage category, không lặp lại giá trị gốc |

Read-only health ngày 17/10 lúc 12:54 ICT trả HTTP 200, `status=ok`, database/storage sẵn sàng, migration 0, Qwen3.8 Flash, reasoning none, `data_collection=deny`, AI concurrency 1 và fallback tắt. Health không gọi inference nên đã được đối chiếu thêm bằng live smoke phía dưới.

## Gate kỹ thuật đã đạt

- Debug build: 0 warning, 0 error.
- Offline tests sau UX follow-up và multilingual fail-safe: 190/190 pass.
- Mailbox sandbox thử nghiệm cùng migration liên quan đã được gỡ; EF model hiện không có pending model change.
- Live Verify sau publish: 5/5 trong 49,518 giây, 0 system error.
- Duplicate Verify clients: 1 HTTP 200 và 4 HTTP 409 trên một process.
- Năm upload khác nhau: 5/5 hoàn tất, không mất job; E2E P95 93,684 giây do concurrency một.
- Live upload không gửi `claimedAmount`: 3/3 hoàn tất, đều HTTP 202, AI amount provenance đúng, 0 provider/system error; 2/3 khớp manifest cũ, TC-05 chuyển bảo thủ sang `ESCALATE_PRIVACY` thay vì `ESCALATE_POLICY`.
- Live input validation: 2/2 ca file giả magic bytes và file 5 MB + 1 byte bị HTTP 400 trước storage/queue.
- Workflow live: Forward → manager YES → Undo → Audit đều pass trên fixture synthetic.

## Thay đổi sau feedback doanh nghiệp

1. **Nhiều hóa đơn:** UI nhận tối đa 10 ảnh và gọi tuần tự cùng endpoint HTTP 202. Mỗi ảnh vẫn qua kiểm extension/MIME/magic bytes/5 MB, private storage, SHA-256, SQL row, lease và audit độc lập. Danh sách job được lưu sau từng response 202 để lỗi giữa batch không làm mất job đã nhận.
2. **Bỏ nhập tiền khỏi UI:** trình duyệt không còn hiển thị hoặc gửi `claimedAmount`. Worker chỉ dùng `facts.TotalAmount` do model trả về sau JSON Schema và semantic validation; nguồn mới của luồng demo là `AI_EXTRACTED` hoặc `UNAVAILABLE`. Backend giữ `USER_PROVIDED` để tương thích test kit/API lịch sử. Thiếu total vẫn fail-safe về kiểm tra dữ kiện.
3. **Dashboard quản lý:** tab Tổng quan cho chọn tháng/năm; KPI, bảng ngày, bảng đối chiếu năm và CSV dùng cùng kỳ được chọn. Dữ liệu được project theo giờ Việt Nam từ SQL.
4. **Privacy triage:** model chỉ gắn marker category trong `suspiciousSignals`; backend normalize category, không ghi lại giá trị cá nhân, và policy C# chuyển `ESCALATE_PRIVACY`. Request OpenRouter mặc định yêu cầu provider không dùng dữ liệu để thu thập/huấn luyện.
5. **Báo cáo quản lý:** dashboard tải CSV theo kỳ đang chọn từ SQL. File chỉ chứa dữ liệu vận hành cần review, không chứa raw AI facts và có chống CSV formula injection.
6. **Drive folder connector:** Drive synthetic sandbox đã qua E2E local trên fixture tổng hợp, tắt mặc định và chỉ chạy Development loopback. Service account Viewer trên folder test, metadata/MD5/version + file gate và cùng ingestion boundary. Kênh production vẫn cần identity người nộp, consent, malware quarantine, retention, distributed coordination và capacity test.
7. **Dashboard fail-closed và UX:** khi SQL không tải được, UI không hiển thị KPI bằng 0 hoặc cho tải CSV. Bảng có vùng cuộn, header sticky và đường kẻ tương phản hơn; nút refresh dùng icon. “Cần kiểm tra dữ liệu cá nhân” là số hồ sơ có marker privacy cần người xem lại, không phải kết luận pháp lý.

Privacy triage là công cụ hỗ trợ rà soát, không phải kết luận pháp lý hay chứng nhận tuân thủ. Strict zero-data-retention chỉ được bật sau khi route/provider live được xác nhận hỗ trợ; nếu bật mù có thể làm không còn provider đủ điều kiện. Rate limit mới chỉ nằm trong memory từng process và không thay thế SSO/RBAC hoặc edge limiter.

## Evidence đánh giá

| Tập | Kết quả | Ranh giới claim |
|---|---|---|
| Prospective mini-holdout 10 | 8/10, 0 missed, 2 over, 0 system/provider/429, 28/30 field | Một official run; mẫu nhỏ có redaction; không phải production accuracy |
| Regression 12 original bytes | 10/12, 0 missed, 2 over, 35/36 field | Dữ liệu đã biết; stability/concurrency only |
| Sequential repeat 12 | 11/12, 0 missed, 1 over, P95 32,903 giây | Không phải holdout mới |

FAHASA còn over ổn định do validator chưa chấp nhận chắc chắn nhãn `Thanh toán 91.500 đ` là final-payable. Không vá keyword sát ngày thi vì có nguy cơ nhầm tender, tiền khách đưa hoặc tiền thối. Evidence localization vẫn là prototype cô lập và chưa ảnh hưởng pipeline/policy.

## Gate con người còn mở

1. Publish/recycle UX follow-up mới nhất, kiểm lại `/healthz`, bộ chọn kỳ/CSV và một synthetic smoke; không rerun holdout cũ để tạo accuracy claim mới.
2. Xác nhận route Alibaba/OpenRouter vẫn nhận `data_collection=deny`; chỉ cân nhắc bật strict ZDR sau một probe có kiểm soát.
3. Hoàn tất ba phiên user study thật, có consent, quote nguyên văn, một thay đổi truy vết được và một negative effect.
4. Rehearsal có bấm giờ theo demo mới: dashboard → lượt nhiều ảnh không nhập tiền → privacy escalation → manager → audit.

Đối chiếu đầy đủ với tài liệu `recommend_check`, 10 architecture gates và evidence read-only ngày 17/10 nằm trong `AFTER_FEEDBACK_ARCHITECTURE_CHECKLIST.md`.

## Cách đọc ngày trong gói Demo Day

Các gói evidence có timestamp ngoài repository là bằng chứng lịch sử bất biến, không được sửa ngày để tạo cảm giác đây là phép đo mới. Nguồn trạng thái hiện hành là tài liệu này; bản tổng hợp số liệu công khai nằm tại `EVALUATION_EVIDENCE.md`. Tài liệu ôn tập, kịch bản và Q&A cá nhân được giữ ngoài repository tại `D:\aura\phan_bien`; chúng không phải evidence đánh giá hay hồ sơ nộp.
