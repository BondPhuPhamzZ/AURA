# Trạng thái hiện hành AURA ngày 23/10/2026

> **Đối chứng model local mới nhất (23/10): vẫn NO-GO publish/scale.** Trên cùng
> 15 ca synthetic đã biết và code quyết định mới, Qwen3.6 Flash đạt 15/15
> quyết định, 0 system error; Qwen3.8 Flash đạt 13/15, một schema mismatch
> và một over-escalation. Trước khi bổ sung gate mô tả hàng hóa chung chung,
> Qwen3.6 đã bỏ lọt một ca FACT do dòng tổng bị mờ nhưng model dựng evidence
> hợp lý từ số dòng hàng. Gate mới chuyển ca đó cho người kiểm tra, **không**
> giải quyết tổng quát việc model tưởng tượng pixel. Adapter đã chặn response
> provider quá 2 MiB. Release/Debug 268/268 và LocalDB opt-in 5/5 sau
> test reclaim lease và SQL application lock xuyên coordinator;
> build sạch và EF không đổi sau guard.
> Không đổi default model hay
> SmartASP; chưa có holdout mù/ảnh thật được phép dùng với model thay thế.
> Xem [đối chứng route và giới hạn](MODEL_ROUTE_COMPARISON_2026-10-23.md).

> **Gate strict-schema trước đối chứng model (23/10): NO-GO publish/scale.** Parser v2 đã bắt buộc đủ
> shape; log/audit Semantic không còn ghi nguyên văn issue có thể chứa dữ
> liệu hóa đơn. Release và Debug 253/253 test pass; LocalDB opt-in 2/2 pass
> trên DB tạm; Release build 0 warning/error, EF không có pending model change.
> Hai lượt 5 ảnh synthetic khớp nhãn 3/5 và 4/5. Lượt 15 ảnh thật đã dùng
> trước chỉ 13/15 completed, hai phản hồi provider HTTP 200 nhưng không là
> JSON object sau retry. Không có benchmark accuracy mới; chưa publish.
> Xem [evidence strict-schema](STRICT_SCHEMA_REGRESSION_2026-10-23.md).

> **Chẩn đoán tiếp theo (23/10):** đã bổ sung mã lý do schema an toàn,
> không ghi phản hồi model thô hoặc giá trị hóa đơn. Ca 13 của bộ 15 ảnh cũ
> chạy lại riêng 3/3 completed nên chưa tái hiện được lỗi trước đó; không
> được xem là cải thiện accuracy. Default suite 248/248 pass (hai ca LocalDB
> integration opt-in không nằm trong lượt này). Vẫn **NO-GO** cho publish/
> scale. Đã xác định thêm khoảng trống code: với version v2 hiện diện,
> parser chưa bắt buộc đủ mọi trường required; cần fix và chạy regression
> trước khi tiếp tục. Xem [evidence chẩn đoán](SCHEMA_FAILURE_DIAGNOSTIC_2026-10-23.md).

Đánh giá về mức supervised MVP, yêu cầu Track A, phạm vi nghiệp vụ, UX reload
và điều phối nhiều máy chủ nằm tại
[readiness and scale assessment](READINESS_AND_SCALE_ASSESSMENT_2026-10-23.md).
Các artifact cuộc thi được giữ nguyên; xem [addendum hậu cuộc thi](../submission/AURA_POST_CONTEST_ADDENDUM_2026-10-23.md).

> **Gate mới nhất sau chỉnh prompt, 23/10:** 234/234 Release tests pass;
> smoke local 5 synthetic chỉ 3/5 completed và khớp nhãn, hai ca
> `AI_SCHEMA_MISMATCH`. Lặp lại 15 ảnh thật đã dùng trước: 14/15 completed,
> một `AI_RATE_LIMIT`; 10/15 khớp nhãn khóa và 11/15 quyết định trùng lần
> trước nếu tính ca fail là không khớp. Đây là **NO-GO cho benchmark mới và
> mở rộng worker**. Xem [evidence và giới hạn](EVALUATION_EVIDENCE.md).

> **Candidate mới nhất:** runner ghi checkpoint; retry nhắc schema/v2; 429
> quay về queue tối đa ba lượt. Parser chỉ gắn version v2 khi provider bỏ sót
> đúng trường protocol đó và mọi trường evidence còn lại hiện diện, có cờ
> audit `EvidenceContractVersionInferred`. Suite báo 244/244; hai ca LocalDB
> opt-in chạy riêng 2/2 pass, DB tạm đã dọn. Hai lượt synthetic liên tiếp
> 5/5, nhưng lặp 15 ảnh thật đã dùng vẫn chỉ 14/15 completed (một
> `AI_SCHEMA_MISMATCH`), 10/15 khớp nhãn và 11/15 lặp quyết định cũ.
> Audit: 4/15 Semantic Repair, 0/15 inferred version. **Chưa publish hoặc
> tăng worker**; đây chưa phải bằng chứng production accuracy.

> **Source hiện hành:** **231/231** Release tests pass, Release build 0
> warning/error với `--no-restore`, EF không có pending model changes. Drive
> synthetic sandbox vẫn chỉ chạy Development/loopback và tắt mặc định. Batch
> 5/20/100 đã chạy qua Google Drive API, SQL queue, OpenRouter và policy.
> Bản 100 đầu sinh 104 job từ 100 file do metadata version đổi; sau sửa
> identity theo folder ID + file ID + MD5 nội dung, DB thử mới sinh đúng
> 100 job, 98 completed và 2 fail-safe. Poll và restart không tạo thêm job.
> 15 ảnh thật có consent chạy qua **manual intake local**, 15/15 completed;
> không upload lên Drive và không có ground truth khớp hash để tính accuracy.
> Xem [báo cáo đo MVP](DRIVE_MVP_VALIDATION_2026-10-22.md). Đây không phải
> bằng chứng production-ready hay cho phép publish Drive connector.

> **Hardening 23/10:** storage đặt dưới `wwwroot` bị từ chối lúc startup;
> tên file đã lưu không được chứa path separator. Dashboard trả `no-store`.
> Smoke local trên DB thử riêng đạt `/healthz=ok`; bộ 100 hiện có bị chặn bởi
> gate hash duy nhất vì có 70 bản sao. 15 ảnh thật có 15 hash khác nhau nhưng
> chưa có nhãn accuracy. Xem [evidence và giới hạn](STORAGE_AND_DATASET_GATE_2026-10-23.md).

> **Mốc lịch sử trước batch:** Source 22/10 có Drive sandbox Development-only, tắt mặc định,
> không thêm EF migration. Manual upload và Drive staging cùng dùng
> ReceiptIntakeService; Drive chỉ đọc file con trực tiếp của folder test riêng,
> chống trùng bằng request ID tất định theo file ID + version lúc đó. **E2E local thật**
> đã đọc Google Drive qua service account, tạo SQL job và gọi OpenRouter:
> 3/3 lần xử lý job synthetic hoàn tất (2 fixture khác nhau, một fixture chạy
> lại để xác nhận sửa lỗi), một file `.txt` bị chặn, restart không tạo trùng.
> Fixture thứ hai lộ false positive `TaxId` giống PHONE; backend đã sửa và
> chạy lại bằng file ID mới ra `AUTO_APPROVE`, vẫn giữ audit cũ.
> `/healthz` local `status=ok`, migration 0, dashboard 200. Connector đã tắt
> lại sau test; **bản live chưa publish lại, không nhận dữ liệu thật**.
> Rejection log được hardening để cùng source-version/reason chỉ ghi một lần;
> vượt giới hạn scan 100 entries cũng fail-closed, không intake một phần.
> Tại mốc đó **216/216** Release tests pass, build 0 warning/error, EF không pending
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

Các gói evidence có timestamp ngoài repository là bằng chứng lịch sử bất biến, không được sửa ngày để tạo cảm giác đây là phép đo mới. Nguồn trạng thái hiện hành là tài liệu này; bản tổng hợp số liệu công khai nằm tại `EVALUATION_EVIDENCE.md`. Tài liệu ôn tập, kịch bản và Q&A cá nhân được giữ ngoài repository; chúng không phải evidence đánh giá hay hồ sơ nộp.
