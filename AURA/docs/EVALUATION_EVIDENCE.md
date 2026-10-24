# Tổng hợp bằng chứng đánh giá AURA

**Mốc baseline nộp bài:** 14/10/2026. **Cập nhật trạng thái source:** 23/10/2026.

**Mục đích:** tạo một chỉ mục công khai duy nhất cho các phép đo được dùng trong proposal, slide, Build Log, README và gói Demo Day. Source snapshot mới nhất được đối chiếu 24/10/2026.

## Evidence hậu cuộc thi, không thay thế baseline nộp bài

| Phép kiểm | Kết quả | Không được suy ra |
|---|---|---|
| Release test/build/EF 23/10 | 231/231 test pass; build 0 warning/error; EF không có pending model change | Production safety hoặc 231 hóa đơn đúng |
| Drive local Development 5/20/100 | 100 file tạo đúng 100 job sau fix; 98 completed, 2 fail-safe; 30 nội dung ảnh khác nhau | 98% accuracy, 100 ca độc lập hoặc connector production |
| 15 ảnh thật có consent | 15/15 hoàn tất qua manual localhost; 15 hash khác nhau | Accuracy, blind holdout hay Drive nhận ảnh thật |
| Storage/dataset gate 23/10 | Chặn receipt storage dưới `wwwroot`; bộ 100 bị chặn khi yêu cầu 100 hash độc lập | RBAC, scanner hoặc bảo đảm privacy toàn diện |
| Post-feedback local smoke 23/10 | 5 synthetic: 3/5 completed và khớp nhãn; 2 `AI_SCHEMA_MISMATCH`. Lặp 15 ảnh thật đã dùng: 14/15 completed, 1 `AI_RATE_LIMIT`; 10/15 khớp nhãn khóa, 11/15 quyết định trùng lần chạy trước (tính ca fail là không khớp) | Cải thiện accuracy, blind holdout mới, provider ổn định hoặc sẵn sàng scale |
| Committed local candidate `0568b5a` (24/10) | Debug/Release 333/333 mỗi bản, LocalDB integration bật; privacy kit 4 ca × 3 lượt: 12/12 decisions, 21/21 category; v3.1 một lượt 15/15 decisions, 143/146 facts | Không chứng minh privacy detector tổng quát, pre-inference redaction, độ chính xác trên dữ liệu thật, hosting restore hoặc production readiness |
| Local single-host rerun (24/10) | Debug/Release 333/333, không skip; isolated SQL + local-file backup/restore test 1/1; Release build 0 warning/error; EF không pending model changes; một synthetic R3-01 đi hết durable queue tới `COMPLETED / AUTO_APPROVE` trong 5.928 ms trên DB/storage mới cô lập | Một ca smoke không phải accuracy sample; không chứng minh hosting restore, nguyên tử snapshot SQL/filesystem, RPO/RTO hoặc provider contractual retention |

Chi tiết và các giới hạn nằm ở [MVP validation](DRIVE_MVP_VALIDATION_2026-10-22.md), [storage/dataset gate](STORAGE_AND_DATASET_GATE_2026-10-23.md) và [readiness assessment](READINESS_AND_SCALE_ASSESSMENT_2026-10-23.md). Source mới chưa được xác minh là đã publish lên host công khai. Test no-restore có cảnh báo `NU1900` do advisory feed không truy cập được trong invocation đó; không được diễn giải invocation này là một fresh dependency audit.

### Gate sau chỉnh prompt ngày 23/10: chưa đạt

Chạy trên AURA Development loopback, LocalDB và OpenRouter `qwen/qwen3.8-flash`, AI concurrency 1; `/healthz` trước test là HTTP 200, database up to date. Provider route đặt `data_collection=deny`, nhưng Strict ZDR chưa được xác nhận (`false`). Không dùng Drive hoặc host public trong phép đo này. Bộ 5 synthetic có hai phản hồi HTTP 200 từ provider nhưng không qua schema sau retry; hệ thống chuyển sang `ESCALATE_SYSTEM_ERROR`, không tự duyệt. Bộ 15 ảnh đã từng sử dụng có cùng 15 hash với lần trước: 14 hoàn tất, một ca cuối gặp provider rate limit và chuyển lỗi hệ thống. Trong 14 ca hoàn tất, 10 khớp nhãn khóa (10/15 nếu tính toàn batch); 11 quyết định khớp lần chạy trước (11/15 toàn batch). Median E2E của batch là 9.156 ms; mẫu nhỏ và có retry/provider throttling nên không suy ra SLA. Lần chạy trước đó đạt 15/15 completed, 8/15 khớp nhãn; kết quả hiện tại không chứng minh cải thiện bền vững vì một ca lỗi và quyết định biến thiên.

Release tests sau phép đo: 234/234 pass. Invocation `--no-restore` có cảnh báo `NU1900` do không truy cập được NuGet advisory feed và các retry copy apphost vì web local còn giữ executable khi test bắt đầu; không xem đây là dependency audit sạch. Raw results, input hashes, opaque case IDs và đối chiếu theo từng ảnh nằm trong `test_kit/results/` đã Git-ignore; không đưa ảnh, filename cá nhân, request ID hoặc model payload lên GitHub. Kết luận hiện tại: **NO-GO cho benchmark mới/scale worker**; cần xử lý schema compliance và rate limit, sau đó chạy lại tập khóa với tiêu chí pass định trước.

Thử nghiệm trung gian cùng ngày: retry cho schema-invalid gửi nhắc lại hợp đồng JSON/v2; `AI_RATE_LIMIT` tạm đưa job về `PENDING` tối đa ba lượt trước khi fail-safe. Release suite báo 241/241 pass; trong đó hai ca LocalDB chỉ thực thi khi `AURA_RUN_LOCALDB_INTEGRATION=1`, nên lượt chạy mặc định thực thi 239 ca. Lượt chạy riêng với biến này có **2/2 integration pass** trên DB tạm tên ngẫu nhiên: job 429 còn `PENDING` qua worker mới rồi hoàn tất một lần ở lượt thành công; ba lần 429 thì fail-safe. Không còn DB tạm sau test. Đây là test worker/DB với extractor giả lập, chưa kiểm tra cooldown thời gian thực qua OpenRouter. Smoke model thật trên cùng 5 fixture đạt 4/5 completed và đúng nhãn; một fixture khác vẫn `AI_SCHEMA_MISMATCH` sau retry. Đây là bằng chứng lỗi phản hồi chưa ổn định, không xác nhận bản sửa đã giải quyết tận gốc. Giữ **NO-GO**, chưa publish hoặc tăng worker. Raw synthetic result ở `test_kit/results/synthetic-post-retry-20261023/results.json` (Git-ignore).

`AI_SCHEMA_MISMATCH` phát sinh trong `ReceiptExtractionContract.ParseFacts`: JSON không tạo được DTO hợp lệ (ví dụ thiếu `evidenceContractVersion=2`). Lượt retry schema chạy trước Semantic Validator; nếu cả hai lần sai schema thì không có DTO để Semantic Repair sửa. Ngược lại, khi DTO parse được nhưng dữ kiện mâu thuẫn, `ReceiptSemanticValidator.Validate` tạo các issue, hướng dẫn model đọc lại ảnh một lần, rồi đánh dấu unresolved để chuyển người kiểm tra nếu vẫn sai. Các test Semantic Repair hiện có xác nhận nhánh này; nó không phải bộ sửa JSON tùy ý.

Đối chiếu audit chỉ lấy số đếm của smoke 5 fixture sau chỉnh retry (từ 14:35 UTC): 5 action cuối gồm 2 `AUTO_APPROVE`, 1 `ESCALATE_FACT`, 1 `ESCALATE_POLICY`, 1 `ESCALATE_SYSTEM_ERROR`; `SemanticRepairApplied=True` là **0/5**. Với ca schema lỗi, số 0 là do chưa có DTO, không phải Semantic Repair đã chạy và thất bại. Không đọc hoặc xuất raw extracted facts trong phép đối chiếu này.

### Candidate schema recovery và regression tiếp theo

Parser chỉ điền `evidenceContractVersion=2` nếu model thiếu riêng trường protocol này và đủ mọi trường evidence bắt buộc (kể cả key của line item). Backend lưu `EvidenceContractVersionInferred`; thiếu dữ kiện khác hoặc version sai vẫn bị chặn. Test contract mới: 3/3 pass; Release suite báo 244/244 (hai integration LocalDB là opt-in và đã chạy riêng 2/2). Đây là recovery protocol hẹp, không sửa số tiền hay các dữ kiện OCR.

Hai lượt synthetic tuần tự trên cùng năm fixture đều 5/5 completed và khớp nhãn, thời gian batch lần lượt 41.828 ms và 49.000 ms. Lượt 15 ảnh thật đã dùng trước: 15/15 hash trùng inventory cũ, 14/15 completed, một `AI_SCHEMA_MISMATCH` ở ca 13; 10/15 khớp nhãn khóa, 11/15 quyết định trùng lần trước. Audit aggregate của lượt 15: 4 có `SemanticRepairApplied=True`, 0 có `EvidenceContractVersionInferred=True`. Nhánh inference mới chỉ được chứng minh offline, không phải nguyên nhân trực tiếp cho 5/5 synthetic. Raw results nằm trong `test_kit/results/synthetic-schema-recovery-20261023/`, `synthetic-schema-recovery-repeat-20261023/` và `private-receipts-schema-recovery-20261023/` (đều Git-ignore). Vì real regression còn lỗi schema, **NO-GO cho publish/scale và accuracy claim mới**.

## Baseline nộp bài ngày 14/10

| Nhóm evidence | Kết quả đã xác minh | Ranh giới claim |
|---|---|---|
| Release build | 0 warning, 0 error | Chỉ là gate build |
| Bộ test offline | 163/163 pass | Test code, không phải 163 hóa đơn |
| Verify Harness live | 5/5 quyết định kỳ vọng trong 65,004 giây | Đạt gate demo 5 ca dưới 90 giây trên lần đo này |
| Nhiều client bấm Verify trùng | 1 HTTP 200 + 4 HTTP 409 | Idempotency trong một process, không phải distributed locking |
| Candidate Qwen3.8 Flash | OpenRouter route tới Alibaba; reasoning `none`; fallback tắt | Candidate demo có giám sát đã đóng băng |
| Mini-holdout prospective | 8/10 exact, 0 missed, 2 over, 0 lỗi system/provider/429, 28/30 field trọng yếu | Một lần chạy nhỏ trên ảnh đã redaction, không phải production accuracy |
| Regression trên 12 source bytes | 10/12 exact, 0 missed, 2 over, 35/36 field trọng yếu | Evidence ổn định trên known data |
| Lặp tuần tự cùng 12 ảnh | 11/12 exact, 0 missed, 1 over, P95 32,903 giây | Không phải holdout mới |
| Năm upload khác nhau | 5/5 về trạng thái cuối; E2E P95 93,684 giây | Test queue integrity ở AI concurrency 1, không phải Verify SLA |
| Human workflow | Synthetic escalation, quyết định quản lý, Audit và Undo đều pass | Evidence workflow, không phải xác nhận từ người dùng thật |
| Evidence localization | Prototype overlay synthetic gắn với hash | Chưa nối extraction hoặc policy; chưa có benchmark annotation unseen |

## Bằng chứng lịch sử được giữ ngoài cây docs hiện hành

Các gói raw có timestamp vẫn nằm trong workspace evidence riêng tư và release history. Những phép đo lịch sử chính gồm:

- synthetic judge set đã khóa: OpenRouter 15/15 quyết định và Ollama 14/15;
- Test Kit v3 live: 11/15 quyết định, 142/145 field, 0 system error;
- Test Kit v3.1: 13/15 exact, 0 missed escalation, 0 system error;
- real holdout khóa một lần: raw 10/15 với một missed và ba over; phân tích adjudicated là 11/15 sau khi phát hiện một label sai mà không đổi raw evidence;
- benchmark Ollama 4B 16K cô lập: 15/15 quyết định synthetic nhưng processing P95 213,429 giây;
- so sánh Qwen3.8 Flash hậu chỉnh: 15/15 trên known receipts, sau đó mới chạy mini-holdout prospective độc lập 8/10 ở trên.

Các phép đo này không thể thay thế lẫn nhau. Regression sau không được ghi đè kết quả holdout trước.

## Các mismatch đã biết

- FAHASA vẫn là fail-safe over-escalation ổn định vì validator chưa xem nhãn `Thanh toán 91.500 đ` là evidence tổng cuối đủ chắc chắn.
- GS25_2 cho thấy extraction variance giữa các lần lặp known data.
- Three O'Clock cho thấy vùng privacy blackout cùng free-text warning có thể kích hoạt FACT review dù core fields đúng.

Dự án chủ động không nới final-total guard ngay trước ngày chấm vì việc đó có thể biến tiền khách đưa, cash received hoặc tiền thối thành payable amount được duyệt.

## Quy tắc toàn vẹn raw evidence

1. Giữ nguyên timestamp, SHA-256 manifest, expected label và raw outcome.
2. Không đưa hóa đơn, provider payload, secret, database hoặc ảnh API-key dashboard vào repository công khai.
3. Chỉ sửa lỗi hiển thị encoding/parser từ raw body đã giữ; ghi lại cả hai hash và không replay provider để ghi đè outcome.
4. Áp dụng yêu cầu dưới 90 giây cho Verify Harness 5 ca, không áp cho test concurrency năm người upload.
5. Không claim user evidence ba người cho tới khi có đủ record có consent, trích dẫn được xác nhận, một thay đổi truy vết được và một tác động tiêu cực.
6. Dùng `CURRENT_PROJECT_STATUS.md` cho trạng thái hiện tại; dùng Git history và release tag đóng băng cho báo cáo theo ngày.

## Bản đồ evidence riêng tư

Bản Demo Day đã tuyển chọn và gói nguồn được lưu trong các workspace riêng, ngoài repository công khai; ảnh hóa đơn thật và raw provider material bị loại khỏi bản tuyển chọn. Release tag và Git history giữ nguyên các snapshot công khai đã dọn khỏi cây hiện hành khi cần audit câu chữ cũ.
