# AURA project audit and Sprint 1 feedback closure — 12/10/2026

> **Addendum cùng ngày:** Candidate Qwen3.8 Flash/Alibaba với reasoning `none` đã pass real-receipt post-holdout 15/15, 0 missed/over/system error và concurrency upload 5/5 với E2E P95 41,079 giây. Vì vậy P1 “provider/reliability dưới 90 giây” đã có evidence pass cho candidate trên một process; các mô tả 429/Parasail bên dưới vẫn là forensic history của model 8B cũ. Source default đã đổi, còn Live URL phải publish/recycle/health/smoke trước khi đóng cổng deploy. Xem `QWEN38_FLASH_BENCHMARK_2026-10-12.md`.

## Kết luận điều hành

Source hiện tại **không còn lỗi kiến trúc P1 “người thứ hai upload nhận HTTP 409”** được nêu ở Sprint 1. Upload kiểm file, lưu ảnh và record `PENDING`, ghi audit `AI_QUEUED`, trả HTTP 202 rồi để `ReceiptProcessingWorker` claim job từ SQL bằng atomic update và lease. Nhiều upload có thể được nhận nhanh; AI vẫn xử lý có kiểm soát theo capacity.

Không được đồng nhất ba khái niệm sau:

1. **Năm upload đồng thời**: năm request được nhận HTTP 202 và xếp hàng; đây là điều các smoke cũ 2/5 request đã chứng minh.
2. **Năm inference đồng thời**: không phải mục tiêu hiện tại; `Vision:MaxConcurrentRequests=1` chủ động giới hạn một lời gọi AI để tránh bão 429/VRAM.
3. **Một batch năm ca dưới 90 giây**: candidate Qwen3.8 Flash/Alibaba đã hoàn tất burst năm upload 5/5 với E2E P95 41,079 giây và AI concurrency 1. Verify UI cũ trên 8B/Parasail chỉ đạt 4/5 rồi 2/5 do upstream 429; hai phép đo khác cơ chế admission nên phải báo riêng, không gộp thành production SLA.

Không có P0 logic tái hiện được trong lần audit. Trạng thái đúng là **supervised demo candidate**, không phải production SLA.

## Vì sao trước đây 2–5 request chạy được

Evidence lịch sử dùng `Invoke-ConcurrentUploadSmoke.ps1`: client gửi 2 hoặc 5 upload gần đồng thời, endpoint nhận file và trả 202, sau đó worker xử lý lần lượt. Con số 5/5 nghĩa là năm job cuối cùng hoàn tất, không có nghĩa năm request cùng chiếm GPU/API một lúc.

Các lượt lịch sử chạy lúc upstream nhận request ổn định hơn. Hiện endpoint API của OpenRouter cho `qwen/qwen3-vl-8b-instruct` chỉ liệt kê Parasail; không còn endpoint thứ hai cùng model để provider-layer failover. Vì vậy một 429 Parasail có thể đi thẳng về ứng dụng. Dashboard còn credit, đổi API key hoặc tăng delay không tạo thêm provider capacity. Prompt dài có thể ảnh hưởng token/latency, nhưng evidence 429 vẫn xuất hiện với input ngắn hơn nên không đủ cơ sở quy nguyên nhân cho prompt.

Nguồn hiện hành: <https://openrouter.ai/api/v1/models/qwen/qwen3-vl-8b-instruct/endpoints>.

## Hai semaphore hiện tại có đúng không

Có, nhưng chúng giải quyết hai bài toán khác nhau:

- `WorkflowOperationGate` dùng `SemaphoreSlim(1,1)` **chỉ trong `VerifyController`** để chống hai người bấm cùng một batch năm ca và đốt quota trùng. Upload, forward và manager decision không dùng gate này.
- `VisionInvocationCoordinator` dùng semaphore cấu hình được để giới hạn tổng lời gọi Vision trong **một app process**. Verify và upload worker dùng chung permit; 429 tạo provider cooldown.

Cơ chế “hiện đại hơn” đã thay khóa upload cũ là:

`HTTP 202 → SQL durable job → Channel chỉ đánh thức → BackgroundService → atomic lease claim → RowVersion/audit`.

`System.Threading.Channels` không phải nguồn dữ liệu thật; SQL mới là durable source. Channel có thể drop signal khi đầy nhưng worker vẫn poll SQL nên không mất job.

Giới hạn cần nói rõ: semaphore, cooldown, circuit breaker và Verify gate đều in-memory theo process. IIS demo phải giữ `Maximum Worker Processes = 1`. Multi-instance thật cần distributed limiter/lock (SQL hoặc Redis).

## Đối chiếu feedback BTC/doanh nghiệp

| Feedback | Trạng thái 12/10 | Evidence/code | Kết luận |
|---|---|---|---|
| Bỏ global gate khỏi upload | Đã hoàn tất | `ApplicantController.UploadReceipt` không gọi gate | Không còn 409 do upload lock |
| Upload async, trả HTTP 202 | Đã hoàn tất | durable record + `AcceptedAtAction` + UI polling | Không giữ request chờ inference |
| Background queue sống qua restart | Đã hoàn tất | SQL `ProcessingState`, lease và worker reclaim | Channel chỉ là wake-up signal |
| Automatic OpenRouter → Ollama | Hoàn tất ở code, chưa bật hosted | allowlist + one-shot fallback + audit | SmarterASP không thể gọi loopback laptop |
| Lưu người nộp | Đã hoàn tất | code/tên/phòng ban lưu DB và audit | Demo vẫn chưa có authentication/RBAC thật |
| Đo over-escalation | Đã hoàn tất | real regression 11/15, 0 missed, 4 over | Có baseline nhưng chưa phải production accuracy |
| Index `AuditLog.RequestId` | Đã hoàn tất | composite index `(RequestId, Timestamp)` | Migration/model đã đồng bộ |
| Load test 5–10 upload | Đã hoàn tất phạm vi demo | runner 1–10; smoke lịch sử 2/5 | Chứng minh admission/queue, không phải AI parallel SLA |
| Verify N ca linh hoạt | Có runner riêng; UI cố ý giữ đúng 5 | `Invoke-ExtendedDatasetEvaluation.ps1` | Phù hợp track, không làm rối dashboard |
| Delay 4 giây hard-code | Đã chuyển thành config | `Verify:InterCaseDelayMs` 0–30000 | Chưa được giảm vì upstream đang 429 |
| DB integration/rollback test thật | Chưa có | suite hiện là unit/contract/UI integrity | P2 sau cuộc thi |
| Persistent object storage | Một phần | storage private, configurable absolute path | Chưa có object storage/backup production |
| Docker Compose | Chưa có | có Dockerfile + hướng dẫn SQL | P3, không chặn demo Windows/SmarterASP |
| Resize ảnh | Chưa làm có chủ đích | gửi byte ảnh gốc | Chỉ thêm sau benchmark; resize có thể làm mất evidence OCR |
| PDF/XML | Chưa có | chỉ JPG/PNG + magic bytes | Scope mở rộng, không phải thiếu requirement tối thiểu |

## Audit source, folder và artifact

### Đã đạt

- Release build: 0 warning, 0 error.
- Automated tests: 159/159 pass.
- EF Core: không có model change chưa migration.
- NuGet app và test: không có advisory theo nguồn NuGet hiện tại.
- C# analyzer: không có diagnostic.
- Toàn bộ `tools/*.ps1` parse thành công.
- Secret scan trên file tracked không thấy OpenRouter key thật.
- `.tmp`, `bin`, `obj`, `publish`, receipt runtime, evidence cá nhân và `phan_bien` không được track.
- PPTX đúng năm slide; DOCX đúng một trang; render không có clipping/overlap.

### Không phải lỗi logic nhưng cần giữ sạch

- `dotnet format --verify-no-changes` còn báo khoảng trắng ở một số file cũ. Analyzer/build/test đều sạch; không mass-format sát hạn vì tạo diff lớn không mang giá trị nghiệp vụ.
- `.tmp`, `bin`, `obj`, `publish` và `App_Data` chiếm dung lượng local nhưng đều bị ignore. Không xóa hàng loạt trước khi xác nhận evidence đã archive.
- Nhiều file `docs/*YYYY-MM-DD*` là lịch sử có chủ đích. Nguồn trạng thái hiện hành là `CURRENT_PROJECT_STATUS_2026-10-12.md`; không dùng số cũ làm claim hiện tại.

## Rủi ro còn mở theo ưu tiên

### P1 trước video/chung kết

1. OpenRouter chưa pass 5/5 reliability dưới 90 giây; endpoint hiện chỉ có Parasail.
2. Ollama 4B 16K an toàn trên regression đã chạy nhưng P95 một ca khoảng 213 giây synthetic và 384 giây E2E receipt thật; không phù hợp Verify năm ca dưới 90 giây trên laptop hiện tại.
3. Cần rehearsal một operator, kiểm link bằng thiết bị/mạng khác và có video/evidence dự phòng.
4. Nếu dùng GPU khác, AURA và Ollama nên cùng máy hoặc qua endpoint private/TLS/auth; warm model rồi gate đúng năm fixture trước khi dùng.

### P2 sau cuộc thi

1. Lease hiện không heartbeat; inference cực dài có thể hết lease nếu chạy nhiều worker process. Demo dùng một process và local slow profile đặt lease 1800 giây.
2. Verify vẫn là HTTP request dài; production nên biến thành durable batch job/polling.
3. Distributed rate limiter/circuit/cooldown cho multi-instance.
4. SQL integration test, object storage/backup, authentication/RBAC và retention policy.

### P3 mở rộng

PDF nhiều trang, XML/e-invoice lookup, antivirus, image preprocessing có benchmark, Docker Compose và observability/SLO.

## Có nên test tay ngay không

Có thể test tay **một fixture synthetic**, không chạy 15 receipt hay spam Verify:

1. Bảo đảm app đã chạy; `/healthz` phải HTTP 200, `status=ok`, migration 0, concurrency 1 và fallback đúng profile.
2. Chờ provider yên ít nhất hết cooldown; kiểm Activity/Credits nhưng không tạo key mới để né 429.
3. Không để upload/Verify khác chạy cùng lúc.
4. Upload một fixture, lưu status URL, provider/error code, latency và audit.
5. Nếu pass mới cân nhắc một Verify duy nhất; nếu 429 thì dừng, không retry dồn.

Tại thời điểm audit, localhost port 5000 không chạy, vì vậy chưa có live request mới nào được tạo trong bước rà soát này.

## Phương án latency tối ưu

- Không chọn Ollama 4B trên laptop hiện tại làm đường Verify chính.
- Không giảm delay về 0 trên OpenRouter hiện tại chỉ để săn số đẹp; điều đó tăng burst trong khi chỉ còn một upstream.
- Phương án tốt nhất cho live demo là endpoint GPU 8B dedicated đã warm và benchmark: năm ca phải hoàn tất đúng, không system error, tổng dưới 90 giây. Với bốn khoảng nghỉ mặc định tổng 16 giây, inference trung bình phải dưới khoảng 14,8 giây/ca; nếu endpoint đã chứng minh capacity ổn định mới thử delay 0 và chạy lại toàn bộ gate.
- Nếu chưa có endpoint đạt gate, quay video một lượt ổn định có giám sát, trình bày OpenRouter là dependency bên thứ ba và dùng evidence fail-safe; không hứa live SLA chưa đo.

## Quyết định freeze

Không đổi policy, prompt, schema, model hoặc provider routing ở checkpoint này. Không pin Alibaba: endpoint catalog hiện không liệt kê Alibaba cho model, nên cấu hình đó có thể làm request thất bại. Chỉ sửa P0 tái hiện được; các thay đổi trong audit này là đồng bộ mô tả, chỉ số và artifact nộp kèm.
