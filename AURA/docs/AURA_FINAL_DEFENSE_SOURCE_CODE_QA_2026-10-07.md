# AURA — Answer key bảo vệ source code, nghiệp vụ và Judge Q&A

Cập nhật: 07/10/2026. Tài liệu này là đáp án tra cứu khi ôn pitching và phản biện. Nội dung
được đối chiếu với source hiện tại; không dùng để che giấu giới hạn hoặc học thuộc máy móc.

## 1. Công thức trả lời BGK

Với câu hỏi kỹ thuật hoặc nghiệp vụ, trả lời theo năm lớp:

1. **Kết luận trước:** trả lời trực tiếp trong một câu.
2. **Lý do nghiệp vụ:** rủi ro nào cần kiểm soát.
3. **Cơ chế trong code:** nêu đúng class/file và luồng.
4. **Evidence:** test, live gate hoặc benchmark nào chứng minh.
5. **Ranh giới:** phần nào chưa có hoặc chưa đủ cơ sở để claim.

Mẫu:

> “FACT đứng trước POLICY và AUTHORITY vì policy chỉ được áp dụng trên dữ kiện đáng tin. Trong
> `PolicyDecisionEngine`, mọi validation issue, total/date/identifier không đủ evidence được gom
> trước; chỉ khi danh sách này rỗng mới xét prohibited item rồi hạn mức. Test
> `Fact_uncertainty_has_priority_over_policy_and_authority` chứng minh thứ tự. Giới hạn hiện tại là
> rule ưu tiên thuộc policy demo, muốn dùng thật phải được chủ sở hữu policy phê duyệt.”

Không bắt đầu bằng một đoạn giải thích dài. Không nói “100%”, “production-ready” hoặc “AI hiểu
được mọi hóa đơn”.

## 2. Câu trả lời 90 giây về toàn hệ thống

> “AURA là trợ lý thẩm định hoàn ứng theo mô hình bounded agentic workflow. Người dùng gửi một
> hóa đơn và số tiền khai báo; backend kiểm file, ghi hồ sơ PENDING vào SQL và trả HTTP 202.
> Worker có lease lấy job, VLM chỉ trích xuất JSON theo evidence contract v2. Backend kiểm chéo
> ngày, dòng tổng in trực tiếp, identifier, số học và line item; nếu mâu thuẫn thì cho model sửa đúng
> một lần, còn lỗi thì fail-safe. `PolicyDecisionEngine` C# mới có quyền phân tuyến theo
> FACT → POLICY → AUTHORITY → AUTO. Hồ sơ ngoại lệ được nhân viên chuyển cho quản lý YES/NO,
> có Undo và Audit. OpenRouter 8B là primary; live fallback đang tắt. Đây là supervised MVP:
> 142/142 code tests pass, real-receipt regression đạt 11/15 exact nhưng 0 missed escalation và
> 0 system error; vì vậy evidence hiện chứng minh safety tốt hơn, chưa chứng minh production
> accuracy.”

## 3. AURA giải quyết đúng vấn đề gì của Track VNG?

### Vì sao gọi là AI Agent chứ không chỉ OCR?

**Đáp án:** AURA không chỉ nhận dạng chữ. Nó thực hiện một workflow có trạng thái: tiếp nhận,
trích xuất có schema, tự kiểm tra semantic, thử sửa có giới hạn, áp dụng policy tất định, phân tuyến
cho người phù hợp, theo dõi trạng thái và ghi Audit. Đây là agent bị giới hạn quyền, không phải
general autonomous agent.

**Cơ sở code:** `ReceiptProcessingWorker`, hai vision adapter, `ReceiptSemanticValidator`,
`PolicyDecisionEngine`, `EscalationWorkflow`, repository và Audit.

**Không nói:** “AI tự quyết định hoàn tiền.” Model chỉ đọc facts; C# và con người giữ authority.

### Giá trị chính là gì nếu vẫn cần con người?

AURA giảm việc đọc lặp lại và đưa ca thường quy/ngoại lệ vào đúng luồng. Human-in-the-loop không
phải thất bại; nó là control cần thiết khi evidence không đủ hoặc policy cần ngoại lệ. Giá trị cần
đo bằng thời gian xử lý, missed/over-escalation và gánh nặng reviewer, chưa được phép suy ra ROI
production từ demo.

### Vì sao không dùng LLM quyết định luôn?

Layout và chữ trên hóa đơn biến thiên cao nên VLM phù hợp để đọc. Rule hoàn ứng cần ổn định,
test được, audit được và rollback được nên nằm trong C#. Tách extraction khỏi decision còn cho
phép đổi OpenRouter/Ollama mà không đổi policy authority.

## 4. Vì sao thứ tự là FACT → POLICY → AUTHORITY → AUTO?

### FACT đứng trước POLICY/AUTHORITY vì sao?

FACT là điều kiện tiền đề. Nếu total bị rách, ngày không rõ hoặc identifier mâu thuẫn, hệ thống
không có cơ sở đáng tin để kết luận món hàng, số tiền hay cấp thẩm quyền. Cho POLICY hoặc
AUTHORITY đi trước sẽ tạo cảm giác quyết định chắc chắn dựa trên facts không chắc chắn.

Trong code, `PolicyDecisionEngine.Evaluate` gom toàn bộ `factProblems` trước. Nếu có lỗi, nó trả
`ESCALATE_FACT`; prohibited item nhìn thấy vẫn được ghi kèm trong reason như finding thứ cấp.
Chỉ khi FACT sạch mới xét POLICY, rồi tổng trên 1.000.000 VND mới đi AUTHORITY.

**Evidence:**

- `Fact_uncertainty_has_priority_over_policy_and_authority`.
- BH-07: dòng total bị rách hiện đi FACT thay vì suy total rồi AUTO.
- Real-receipt regression hiện tại: 0/10 missed escalation.

### Nếu vừa có bia vừa trên một triệu thì sao?

Nếu facts đáng tin: POLICY trước AUTHORITY, vì loại chi có thể bị cấm dù người duyệt có đủ thẩm
quyền. Nếu facts còn lỗi: FACT trước cả hai. Đây là priority của policy demo, không phải quy luật
kế toán phổ quát.

### Vì sao cuối tuần hoặc ngoài 06:00–22:00 là FACT?

Đây là anomaly cần người xác minh theo business rule hiện tại, không phải khẳng định hóa đơn giả.
BGK hỏi rule có cứng không thì trả lời: hiện rule nằm trong C# để demo tính tất định; production
nên đưa calendar/time-window vào policy configuration do doanh nghiệp sở hữu.

### Rau, củ, quả, nước mắm có bị từ chối không?

Không tự động chỉ vì là đồ bếp. Policy hiện chỉ match các nhóm cấm đã khai báo. Nếu evidence đủ,
claim khớp, ngày hợp lệ và tổng không vượt quyền thì có thể AUTO. Tuy nhiên mục đích công tác và
cost center chưa được mô hình hóa; production phải bổ sung policy theo doanh nghiệp.

## 5. Năm trạng thái phải giải thích được

| Trạng thái | Ý nghĩa | Ai xử lý tiếp | Không được diễn giải thành |
|---|---|---|---|
| `AUTO_APPROVE` | Evidence đủ và pass policy demo | Workflow tiếp theo của doanh nghiệp | Tiền đã được chi trả |
| `ESCALATE_FACT` | Thiếu/mâu thuẫn evidence | Người kiểm tra ảnh/facts | Gian lận |
| `ESCALATE_POLICY` | Có item ngoài policy trên facts đáng tin | Quản lý duyệt ngoại lệ/từ chối | AI kết án người dùng |
| `ESCALATE_AUTHORITY` | Facts/policy sạch nhưng vượt 1 triệu | Cấp có thẩm quyền | Khoản chi sai |
| `ESCALATE_SYSTEM_ERROR` | Provider/hạ tầng/contract thất bại | Manual review hoặc retry có kiểm soát | Quyết định nghiệp vụ |

## 6. Trace một request từ đầu đến cuối

1. `Views/Home/Index.cshtml` gửi một `receiptFile`, `claimedAmount` và metadata nhân viên.
2. `ApplicantController.UploadReceipt` kiểm size, extension, MIME và magic bytes.
3. File được đổi sang tên GUID; SHA-256 dùng phát hiện byte-identical duplicate.
4. Request và audit `AI_QUEUED` được ghi SQL trước khi worker được đánh thức.
5. API trả HTTP 202 + `statusUrl`; JavaScript poll thay vì giữ request HTTP lâu.
6. `ReceiptProcessingWorker` tìm job PENDING hoặc lease hết hạn, claim bằng atomic update.
7. `ConfiguredVisionExtractor` gọi primary hoặc fallback đủ điều kiện.
8. Provider dùng `ReceiptExtractionContract` để gửi ảnh và nhận JSON schema v2.
9. `ReceiptSemanticValidator` kiểm meaning; provider repair tối đa một lần.
10. `PolicyDecisionEngine` ra nhánh quyết định; worker lưu facts, reason, provider và Audit.
11. UI nhận trạng thái hoàn tất; escalation nằm ở hàng đợi nhân viên.
12. Nhân viên chỉ xác nhận chuyển tiếp; quản lý YES/NO; `ReviewerController` ghi outcome.
13. Undo khôi phục nhánh escalation gốc và tạo audit mới, không xóa lịch sử cũ.

## 7. Source-code defense map

Mỗi mục dưới đây trả lời bốn câu: chịu trách nhiệm gì, fail-safe ra sao, evidence nào chứng minh,
và nên nói gì trước BGK.

### `Program.cs`

- **Trách nhiệm:** bind/validate Options, DI, SQL Server, HTTP clients, worker, storage root,
  migration switch, middleware, route và `/healthz`.
- **Fail-safe:** config provider/URL/model sai làm startup validation fail; health trả 503
  `degraded` nếu policy/key/DB/migration/storage chưa sẵn sàng.
- **Evidence:** live `/healthz` đã trả `ok`, DB/storage true, pending migration 0; publish gate đã
  thực hiện sau recycle.
- **Giới hạn phải nói:** health chỉ kiểm cấu hình/key presence và DB/storage/policy, không thực
  hiện một inference thật; khi primary là Ollama, `aiConfigured=true` không chứng minh Ollama đang
  reachable. App có `UseAuthorization` nhưng chưa có authentication/RBAC production.

### `Options/*.cs` và `appsettings.json`

- `VisionOptions`: primary/fallback và circuit breaker.
- `OpenRouterOptions`: endpoint HTTPS, Qwen model, timeout 90s, output cap 4096.
- `OllamaOptions`: chỉ HTTPS hoặc HTTP loopback, Qwen3-VL, context/output/keep-alive.
- `ReceiptStorageOptions`: thư mục và giới hạn 5 MB.
- `ReceiptProcessingOptions`: poll, lease 600s, failure backoff.
- `DecisionPolicyOptions`: có bật escalation duplicate hay không.
- `VerifyOptions`: khoảng nghỉ giữa ca để bảo vệ quota.
- **Fail-safe:** `ValidateOnStart` chặn cấu hình quan trọng sai; API key/connection string production
  dùng environment variables, không commit.
- **Giới hạn:** một số business rule như 1 triệu, 90 ngày, cuối tuần và giờ vẫn hard-coded trong
  `PolicyDecisionEngine`; production nên version hóa policy riêng.

### `Controllers/ApplicantController.cs`

- **Trách nhiệm:** upload, phục vụ ảnh riêng qua ID, status polling, forward một/tất cả escalation.
- **Fail-safe:** chỉ JPG/PNG; kiểm extension + MIME + magic bytes; size/amount; basename/GUID chống
  traversal; SHA-256 phát hiện duplicate; ghi DB trước signal; DB lỗi thì xóa file vừa ghi;
  antiforgery trên mutation; RowVersion conflict trả 409.
- **Evidence:** security negative tests/evidence, live upload/status/workflow, 4G/5G gate.
- **Giới hạn:** chưa có antivirus/CDR, auth/RBAC, object storage, retention và signed URL. Receipt
  endpoint hiện dựa vào unguessable ID chứ chưa phải authorization theo user.
- **Câu trả lời một-file:** một request chỉ có một receipt vì claim/facts/decision/Audit phải map
  1–1. Batch runner gửi nhiều request độc lập; UI chưa hỗ trợ multi-file batch.

### `Controllers/VerifyController.cs`

- **Trách nhiệm:** chạy đúng năm fixture có expected result, lưu từng kết quả/Audit vào workflow.
- **Fail-safe:** `WorkflowOperationGate` chặn hai Verify chạy chồng; manifest phải đúng năm ca;
  provider failure nghiêm trọng làm các ca sau ngừng gọi AI để bảo vệ quota; lỗi thành SYSTEM_ERROR.
- **Evidence:** live Verify 5/5; `TestKitIntegrityTests`; UI selectable facts.
- **Giới hạn:** Verify là regression/demo fixture, không phải accuracy dataset hoặc blind holdout.

### `Controllers/ReviewerController.cs`

- **Trách nhiệm:** quản lý YES/NO/UNDO trên hồ sơ đã được nhân viên forward.
- **Fail-safe:** antiforgery; kiểm trạng thái/forward; outcome mapping tất định; RowVersion conflict
  trả 409; Undo chỉ cho resolved status và khôi phục escalation từ Audit AI/Verify.
- **Evidence:** live FACT → forward → accept → Undo → FACT; `PolicyDecisionEngineTests` về outcome.
- **Giới hạn:** chưa có identity/role thật nên “nhân viên” và “quản lý” mới là workflow demo,
  không phải RBAC production.

### `Controllers/HomeController.cs`

- **Trách nhiệm:** chọn tab an toàn và trả các ViewComponent queue/audit không cache.
- **Fail-safe:** tab lạ về applicant; partial load lỗi hiển thị trạng thái lỗi thay vì làm sập trang.
- **Evidence:** `HomeControllerTests.IndexSelectsSafeInitialTab`, UI smoke desktop/mobile.

### `Interfaces/*.cs`

- `IVisionExtractor`: một contract extraction chung cho OpenRouter/Ollama.
- `IReimbursementRepository`: tách persistence khỏi controller/workflow.
- `IAuditLogger`: truy vấn/ghi Audit.
- **Ý nghĩa thiết kế:** dependency inversion giúp đổi provider và test bằng stub mà không đổi policy.
- **Giới hạn:** interface không tự tạo security; implementation và deployment vẫn phải kiểm soát.

### `Models/ReceiptExtractionDto.cs`

- **Trách nhiệm:** canonical facts, line items, missing/warning/suspicious, evidence contract v2,
  validation/repair observability.
- **Fail-safe:** `TotalAmountSource` chỉ `PRINTED_FINAL_TOTAL` mới đủ cho auto; evidence date/total
  giữ provenance; legacy facts version 0 vẫn đọc được nhưng response mới bắt buộc version 2.
- **Evidence:** validator/provider tests và BH-07.

### `Models/ReimbursementRequest.cs`

- **Trách nhiệm:** aggregate bền vững của một hồ sơ: input hash, processing lease, provider,
  decision, handoff, manager answer, latency và RowVersion.
- **Fail-safe:** trạng thái processing tách khỏi business status; RowVersion chống lost update;
  provider/error/fallback được lưu để không che provenance.
- **Giới hạn:** record và file chưa có retention/encryption-at-rest policy ở tầng ứng dụng.

### `Models/ReimbursementResultDto.cs`

- **Trách nhiệm:** response ổn định cho UI/status API; deserialize facts legacy có bảo vệ.
- **Fail-safe:** chưa completed thì không công bố decision; JSON facts cũ hỏng không làm mất Audit.

### `Models/AuditLog.cs`, `ViewModels/AuditEntryViewModel.cs`

- **Trách nhiệm:** lưu sự kiện và project một timeline theo request.
- **Fail-safe:** Audit là append event trong luồng chính; timeline không ghi đè event cũ.
- **Giới hạn:** đây là application-level append-only behavior, chưa phải immutable/WORM hoặc
  cryptographically chained audit. DB admin vẫn có thể sửa dữ liệu.

### `ViewModels/ReimbursementViewModel.cs`

- Đây là view model cũ/không nằm trong critical flow hiện tại. UI thực tế dùng JSON DTO và các
  ViewComponent. Trả lời thẳng nếu BGK thấy file: “Đây là technical debt nhỏ; không dùng nó làm
  bằng chứng cho runtime behavior và có thể dọn sau submission.”

### `Data/AppDbContext.cs`

- **Trách nhiệm:** EF Core mapping, indexes status/hash/queue/audit và precision amount.
- **Fail-safe:** unique không được áp lên hash vì demo cho phép chạy lại fixture; duplicate policy
  là quyết định riêng. Pending migration được health kiểm.
- **Giới hạn:** chưa có backup/restore/HA benchmark trong evidence hiện tại.

### `Services/ReimbursementRepository.cs`

- **Trách nhiệm:** persistence và ghi request + audit trong cùng `SaveChanges`.
- **Fail-safe:** workflow mutation quan trọng dùng `UpdateRequestWithAuditAsync`; query read dùng
  `AsNoTracking`; hash query phát hiện duplicate.
- **Evidence:** live refresh/recycle persistence, workflow/Audit smoke.
- **Giới hạn:** `UpdateRequestAsync` và `AuditLogger.LogActionAsync` là extension point hiện không
  nằm trong critical path; production nên thu hẹp API để mọi mutation bắt buộc đi kèm Audit.

### `Services/ReceiptExtractionContract.cs` và `BUSINESS_RULES.md`

- **Trách nhiệm:** load nguyên byte ảnh + policy, prompt boundary, strict schema và parse response.
- **Fail-safe:** policy path phải nằm trong content root; contract version 2 bắt buộc; unknown
  extra fields bị bỏ nhưng required schema/provider kiểm shape; ngày và total cần raw provenance;
  prompt injection trong ảnh được xem là untrusted data.
- **Evidence:** provider contract tests, validator tests, Test Kit v3.1 và BH-07.
- **Giới hạn:** schema-valid không đồng nghĩa semantically correct, nên cần validator; Vision không
  xác minh tính pháp lý/tax authority thật.

### `Services/OpenRouterVisionExtractorService.cs`

- **Trách nhiệm:** gọi Qwen3-VL-8B qua OpenRouter, JSON Schema strict, temperature 0, usage log,
  transient HTTP retry và semantic repair.
- **Fail-safe:** thiếu key/timeout/HTTP/schema/truncation thành typed error; malformed output retry
  tối đa một lần; semantic inconsistency repair một lần, còn lỗi được mark unresolved để FACT.
- **Evidence:** năm nhóm OpenRouter service tests không gọi API ngoài; live OpenRouter regressions.
- **Giới hạn:** temperature 0 không bảo đảm tuyệt đối deterministic; provider/network/cost bên ngoài
  vẫn là dependency. Ảnh được gửi base64 đến SmarterASP/OpenRouter theo consent.

### `Services/OllamaVisionExtractorService.cs`

- **Trách nhiệm:** cùng schema/policy/repair nhưng gọi local Ollama `/api/chat`, log token/duration.
- **Fail-safe:** connection/model/HTTP/JSON/truncation thành typed error; repair thất bại không vứt
  first facts mà mark unresolved để FACT.
- **Evidence:** bảy nhóm Ollama service tests; fallback functional regression; 4B post-policy 13/15.
- **Giới hạn:** live SmarterASP không tự có Ollama; Ollama phải chạy cùng máy/network mà backend
  truy cập được. 4B chưa đạt safety gate, GPU BTC 8B chưa benchmark xong.

### `Services/ReceiptSemanticValidator.cs`

- **Trách nhiệm:** kiểm meaning sau schema: VND separator, document type, paper line items,
  placement/date evidence, printed-final-total, arithmetic/discount, identifier role/collision.
- **Fail-safe:** lỗi unresolved vào `ValidationIssues`; critical issue hạ confidence tối đa 0,69,
  thêm missing/warning; không tự sáng tác evidence. Canonicalization chỉ thực hiện phép biến đổi
  lossless như date từ raw evidence hoặc xóa exact duplicate misplaced field.
- **Evidence:** 22 nhóm validator tests, gồm Circle K total rách, PTT/Mã CQT, discount, date/time,
  duplicate identifier và no-line-item.
- **Câu hỏi “sao không cộng line item khi total rách?”:** cộng chỉ tạo inferred total, không chứng
  minh số cuối sau discount/tax/service charge. Với reimbursement, suy đúng số chưa đồng nghĩa có
  evidence đúng; hệ thống chuyển FACT.

### `Services/PolicyDecisionEngine.cs`

- **Trách nhiệm:** authority duy nhất cho quyết định tự động; kiểm confidence/status/duplicate,
  amount/merchant/line items/identifier/currency/date/time/warning, rồi policy và authority.
- **Fail-safe:** mọi uncertainty về fact chặn AUTO; policy phrase match theo token/diacritic;
  ngoại lệ hẹp cho “Bìa/Bia hồ sơ” tránh false positive nhưng không miễn bia đồ uống.
- **Evidence:** 27 nhóm policy tests; real/synthetic regressions.
- **Giới hạn:** prohibited-term matching là MVP taxonomy, chưa phải semantic product catalog hoặc
  accounting policy engine đầy đủ.

### `Services/ReceiptProcessingQueue.cs`

- **Trách nhiệm:** signal in-memory để worker thức sớm.
- **Fail-safe:** signal có thể drop nhưng không làm mất job vì SQL mới là source of truth; worker
  vẫn poll theo timeout.
- **Câu trả lời:** “Channel không phải durable queue; database row mới là durable queue.”

### `Services/ReceiptProcessingWorker.cs`

- **Trách nhiệm:** claim job, gọi extraction/policy, lưu result/provider/Audit và latency.
- **Fail-safe:** atomic lease claim; lease hết hạn được reclaim; cancellation giữ lease để worker
  sau nhận lại; typed/unexpected error thành SYSTEM_ERROR; vòng worker exponential backoff nhưng
  pending job vẫn ở DB.
- **Evidence:** recycle persistence, worker/backoff tests và live queue/status.
- **Giới hạn:** chưa có distributed queue/poison queue/dead-letter policy; lease/circuit metrics
  chưa đưa vào production monitoring.

### `Services/ReceiptProcessingBackoff.cs`

- **Trách nhiệm:** exponential delay có trần khi hạ tầng worker lỗi liên tiếp.
- **Fail-safe:** tránh tight retry loop làm quá tải DB/log/provider.
- **Evidence:** ba nhóm backoff tests.

### `Services/ConfiguredVisionExtractor.cs`

- **Trách nhiệm:** chọn primary/fallback, ghi execution context và dùng circuit breaker.
- **Fail-safe:** fallback chỉ chạy khi bật, provider khác primary và mã lỗi thuộc allowlist; chỉ thử
  fallback một lần; cả hai lỗi thành `AI_ALL_PROVIDERS_UNAVAILABLE`.
- **Evidence:** fallback policy/circuit tests và functional fallback regression.
- **Giới hạn:** circuit breaker là singleton trong một process, không chia sẻ giữa nhiều instance.

### `Services/VisionFallbackPolicy.cs`

- **Trách nhiệm:** allowlist lỗi hạ tầng như timeout, rate limit, auth/credit/model unavailable.
- **Fail-safe:** data/contract/semantic error không fallback, vì đổi model không được phép che một
  hồ sơ mâu thuẫn hoặc tạo two-model shopping.
- **Evidence:** `InfrastructureFailuresAreEligible` và `DataAndContractFailuresDoNotTriggerFallback`.

### `Services/VisionCircuitBreaker.cs`

- **Trách nhiệm:** mở circuit sau số lỗi đủ ngưỡng, bỏ qua primary trong cooldown, rồi cho probe.
- **Fail-safe:** success reset; lock bảo vệ state trong process.
- **Evidence:** hai circuit tests.
- **Giới hạn:** state mất khi restart và không đồng bộ multi-instance.

### `Services/VisionExecutionContext.cs`, `VisionExtractionException.cs`

- **Trách nhiệm:** giữ primary/served/fallback/error provenance theo scope và error code có ý nghĩa.
- **Fail-safe:** UI/Audit biết kết quả thực sự do provider nào phục vụ; không biến lỗi provider thành
  quyết định nghiệp vụ.

### `Services/EscalationWorkflow.cs`

- **Trách nhiệm:** mapping tất định từ escalation + YES/NO sang outcome; nhãn lựa chọn rõ nghĩa.
- **Fail-safe:** nhân viên chỉ handoff; manager mới answer; SYSTEM_ERROR không được mô tả là AI đã
  xác minh; Undo chỉ cho status resolved.
- **Evidence:** manager outcome/label/handoff tests và live workflow.

### `Services/AuditLogger.cs`, `AuditTrailProjector.cs`

- **Trách nhiệm:** truy vấn Audit và gom timeline một dòng/request.
- **Fail-safe:** `AsNoTracking`, limit 1–500, sort UTC; projector giữ toàn timeline.
- **Evidence:** `Projects_one_history_row_per_request_and_preserves_full_timeline`.

### `Services/WorkflowOperationGate.cs`

- **Trách nhiệm:** semaphore một instance hiện được `VerifyController` dùng để ngăn hai lượt
  Verify chạy chồng; có thể tái sử dụng cho operation độc quyền khác.
- **Fail-safe:** lease `Dispose` luôn nhả gate; DB optimistic concurrency vẫn là guard cuối.
- **Giới hạn:** không phải distributed lock.

### `Services/VietnamTime.cs`

- **Trách nhiệm:** convert UTC DB sang UTC+7 ổn định trên Windows/Linux.
- **Fail-safe:** thử hai timezone ID, cuối cùng dùng fixed UTC+7; không phụ thuộc timezone host.
- **Evidence:** hai timezone tests và live Audit timestamp.

### `ViewComponents/*.cs`, `Views/Home/Index.cshtml`, `wwwroot/css/site.css`

- **Trách nhiệm:** ba queue applicant/reviewer/audit; upload/poll/render facts; responsive layout;
  AJAX giữ đúng tab và tránh flash sang trang khác.
- **Fail-safe:** component DB lỗi trả empty + message; HTML escape; mutation có antiforgery; session
  storage chỉ giữ pointer pending request, không phải durable state.
- **Evidence:** desktop/mobile 390×844, điện thoại thật 4G/5G, UI contract tests và console sạch.
- **Giới hạn:** accessibility/usability mới smoke/manual, chưa có nghiên cứu người dùng đủ mẫu.

### `tests/AURA.Tests/*`

- `PolicyDecisionEngineTests`: rule priority/outcome/edge case.
- `ReceiptSemanticValidatorTests`: cross-field/evidence/arithmetic/identifier.
- Provider tests: request/schema/parse/retry/repair bằng fake HTTP, không tốn API.
- Queue support tests: backoff, circuit, fallback, operation gate.
- Audit/time/UI tests: history, timezone, tab, responsive contract.
- `TestKitIntegrityTests`: manifest/images/hash/runner invariant, không đo model accuracy.
- **Giải thích 142:** 142 là số test cases sau mở rộng Theory/InlineData, không phải 142 receipt.

## 8. Câu hỏi về evidence và benchmark

### Kết quả hiện tại chính xác là gì?

- 142/142 code tests pass.
- Verify live 5/5 regression fixture.
- Official real holdout raw 10/15; adjudicated 11/15; raw không sửa.
- Post-holdout real regression 11/15, 0 missed, 4 over, 0 system error.
- Synthetic v3.1 13/15, 0 missed/system error, field 140/146.
- V2 hậu-contract 14/15 nhưng missed TK-12 blur cũ.

### Vì sao 11/15 vẫn gọi là safety improvement?

Exact không tăng so với adjudicated view, nhưng BH-07 từ AUTO sai thành FACT và missed giảm 1→0.
Đổi lại routine over-escalation tăng 2→4. Kết luận đúng là “an toàn hơn, automation thấp hơn”,
không phải “model chính xác hơn”.

### Vì sao không sửa expected để đạt điểm cao?

Ground truth/hash phải khóa trước request. Official raw bất biến; BH-04 được trình bày ở một
adjudicated view riêng vì nhãn người về Chủ nhật sai. Sửa nhãn sau khi xem output mà không giữ raw
sẽ phá tính minh bạch.

### Vì sao không chạy lại nhiều lần và chọn kết quả đẹp nhất?

Đó là cherry-picking và làm mất ý nghĩa holdout. Lượt đầu mới là blind; các lượt sau chỉ là
regression/stability. Candidate mới phải được đo trên receipt unseen mới.

### Field 28/30 có nghĩa gì?

Chỉ đo `currency` và `totalAmount` của 15 ca, không phải full-schema extraction accuracy. Không
được dùng 93,33% đó để nói “AI đọc hóa đơn chính xác 93,33%”.

### Vì sao over-escalation cao?

Các mismatch hiện chủ yếu từ money-role/discount và duplicate identifier. Guard chọn đưa người
duyệt thay vì tự đoán. Đây là chi phí vận hành thật cần cải thiện bằng dataset/role-aware
extraction, nhưng không nên nới trước hạn nếu chưa có test chứng minh không tăng fail-open.

## 9. Câu hỏi về OpenRouter, Ollama và offload

### Live hiện chạy provider nào?

OpenRouter/Qwen3-VL-8B. `FallbackEnabled=false`; `configuredFallbackProvider=Ollama` chỉ là tên
cấu hình, không chứng minh Ollama đang chạy trên SmarterASP.

### 8B tốt hơn 4B có chắc không?

8B có năng lực cao hơn về tổng quát, nhưng không kết luận chỉ từ parameter count. Evidence hiện
tại cho thấy OpenRouter 8B ổn định hơn Ollama 4B trên các bộ đã đo; so sánh hợp lệ phải cùng ảnh,
schema, policy, expected, context và cấu hình lượng tử hóa.

### Offload GPU/CPU có làm giảm chất lượng không?

Offload tự nó chủ yếu ảnh hưởng latency và memory placement. Chất lượng giảm khi để vừa tài nguyên
ta phải hạ model/quantization, context, image preprocessing hoặc output cap; `num_ctx`/`num_predict`
quá thấp còn có thể truncate/mất schema. Vì vậy benchmark phải ghi model tag, quantization,
context/output token và token usage, không suy từ latency.

### Vì sao fallback không chạy cho schema/semantic error?

Fallback dành cho availability. Nếu model đã trả data mâu thuẫn, gọi model khác có thể tạo quyết
định thuận tiện hơn nhưng không chứng minh model nào đúng. Hệ thống chuyển FACT để con người xem.

### Tại sao repair chỉ một lần?

Một lần cho model cơ hội sửa lỗi cụ thể trong khi giữ latency/cost hữu hạn. Lặp vô hạn có thể
hallucinate/cherry-pick và gây timeout. Nếu vẫn lỗi, `MarkUnresolved` buộc FACT.

## 10. Câu hỏi về bảo mật và dữ liệu

### File upload được bảo vệ gì?

Giới hạn 5 MB; JPG/PNG; extension + MIME + magic bytes; tên lưu GUID; basename/path containment;
antiforgery; SHA-256; ảnh không đặt trong `wwwroot`.

### Còn thiếu gì để production?

Authentication/RBAC, antivirus/CDR, authorization ảnh theo owner, encryption/retention/object
storage, rate limiting, secret rotation, monitoring/SLO, backup/restore, WORM audit, privacy/DPA,
multi-instance load/security test.

### SHA-256 chứng minh gì?

Chứng minh byte integrity/duplicate exact file trong phạm vi hash đã khóa. Nó không chứng minh
ảnh hợp pháp, không phát hiện cùng nội dung đã resize/chụp lại và không ẩn PII.

### Prompt injection trong hóa đơn thì sao?

System prompt quy định mọi chữ trong ảnh là untrusted data; model không có tool/approval authority;
instruction nhìn thấy phải vào suspicious signal. Dù model sai, backend validator/policy vẫn là
control độc lập. Chưa được claim chống mọi prompt injection nếu chưa có security evaluation lớn.

## 11. Câu hỏi về reliability và scale

### Tại sao trả HTTP 202?

Inference có thể kéo dài hàng chục giây. 202 xác nhận đã nhận hồ sơ, worker xử lý nền và client
poll `statusUrl`; tránh giữ request/upload connection lâu và cho phép refresh/restart workflow.

### Channel có làm mất job khi recycle không?

Channel chỉ là wake-up signal. Request PENDING nằm trong SQL trước khi signal. Sau restart, worker
poll DB và xử lý lại; lease hết hạn cho phép reclaim.

### Có xử lý đồng thời nhiều user không?

Có smoke nhiều request và optimistic concurrency, nhưng worker hiện xử lý tuần tự trong một
instance; chưa có capacity/load/SLA multi-instance. Không gọi smoke test là production scale.

### Vì sao `ApplyMigrationsOnStartup=false`?

Tránh mỗi app startup tự sửa production schema và gây startup failure/lock ngoài kiểm soát.
Migration được áp có chủ đích, health kiểm pending migration. Trong môi trường CI/CD hoàn chỉnh,
migration nên là deployment step riêng có backup/rollback.

### `/healthz=ok` có chứng minh hệ thống xử lý được ảnh không?

Không hoàn toàn. Nó chứng minh readiness cấu hình/policy/DB/migration/storage; inference thật cần
smoke/Verify riêng. Đây là lý do publish gate gồm health rồi mới Verify/workflow.

## 12. Câu hỏi về human workflow

### Nhân viên và quản lý khác nhau thế nào?

Nhân viên upload và xác nhận chuyển tiếp, không trả lời câu hỏi approval. Quản lý chỉ thấy hồ sơ
đã forward và chọn YES/NO. Hiện đây là separation trong workflow/UI, chưa có identity/RBAC thật.

### YES cho FACT có nghĩa đã hoàn ứng chưa?

Không. Outcome là `MANUAL_REVIEW_ACCEPTED`: đồng ý tiếp nhận kiểm tra thủ công. YES cho POLICY là
duyệt ngoại lệ; YES cho AUTHORITY là chuyển cấp thẩm quyền. Nhãn khác nhau để không đánh đồng.

### Undo có xóa lịch sử không?

Không. Nó khôi phục escalation status gốc và thêm `MANAGER_UNDO`; event YES/NO cũ vẫn còn trong
Audit timeline.

## 13. Câu hỏi về PDF, batch và roadmap

### Có thể nhận PDF không?

Có về mặt kỹ thuật, nhưng không chỉ thêm `.pdf` vào accept list. Minimum safe slice gồm signature/
MIME, page/size limit, malware scan, sandbox rasterization, page-level provenance, multi-page
aggregation rule, retention và benchmark lại. Onsite bốn giờ nên giới hạn PDF một trang trước.

### Có thể upload 15 ảnh một lần không?

UI hiện không. Batch an toàn phải tạo 15 request/claim/Audit riêng, hiển thị progress và kết quả
từng receipt. Không gộp nhiều ảnh vào một prompt vì mất mapping và provenance.

### Ba ưu tiên tiếp theo là gì?

1. Benchmark Ollama 8B trên GPU BTC bằng v3.1 và giữ fallback off nếu chưa pass safety gate.
2. Ba user sessions + rehearsal trên commit freeze.
3. Sau submission: auth/RBAC, PDF safe slice, storage/retention/monitoring và blind holdout v2.

## 14. Những câu hỏi “vặn” và cách trả lời trung thực

### “11/15 thấp như vậy sao dùng được?”

> “Nếu yêu cầu unattended production thì chưa dùng được, nên team ghi NO-GO cho accuracy claim.
> Giá trị candidate hiện tại là supervised triage: 0 missed trên regression hiện hành, error tách
> riêng và mọi escalation có human workflow. Bước tiếp theo là giảm over-escalation trên unseen
> data mà không đánh đổi fail-open.”

### “Các em đang che lỗi bằng cách escalate hết?”

> “Không, vì chúng em báo cả over-escalation 4/5 và coi đó là chi phí cần giảm. Safety gate chỉ là
> một chiều; go-live còn cần automation/usability/capacity gate. Chúng em không gọi 0 missed là
> production accuracy.”

### “Vậy AI có cần thiết không?”

> “Có cho extraction trên layout biến thiên và tạo facts/evidence để con người không đọc từ đầu.
> Nhưng evidence hiện cho thấy mức tự động hóa chưa đủ production. Kiến trúc cố ý cho phép cải
> thiện model mà không giao authority cho model.”

### “Tax code và invoice number thiếu là reject hết à?”

> “Không. Tax ID bắt buộc với VAT invoice trong MVP; numbered retail/restaurant receipt có thể
> dùng receipt/transaction reference. E-commerce dùng order/booking/tracking/reference cùng
> completed/paid evidence. Supporting fields như POS No hoặc Mã CQT không tự thay thế canonical
> traceable identifier.”

### “Model có biết hóa đơn thật hay giả không?”

> “Không. Nó chỉ báo visible facts/anomalies. Xác minh pháp lý cần tax-authority integration,
> signature verification hoặc issuer lookup, chưa có trong MVP.”

### “Tại sao claimed amount không đưa vào prompt để model dễ so?”

> “Để tránh anchoring/leakage. Model đọc ảnh độc lập; backend sau đó so extracted printed total
> với claim. Như vậy mismatch là evidence, không bị model sửa câu trả lời theo số người dùng khai.”

### “Tại sao total rách không cộng các dòng?”

> “Arithmetic là supporting check, không phải provenance của số phải trả cuối. Discount, VAT,
> service charge hoặc trang thiếu có thể làm tổng dòng khác số phải trả. Contract cho phép ghi
> INFERRED nhưng không dùng inferred total để AUTO.”

### “Nếu OpenRouter chết giữa demo?”

> “Hệ thống trả SYSTEM_ERROR, không giả business decision. Demo có localhost/video/evidence dự
> phòng. Ollama chỉ bật khi môi trường local được cấu hình và đã pass gate; live SmartASP hiện
> fallback tắt.”

### “Vì sao không triển khai local model luôn để bảo mật?”

> “Local giảm data egress nhưng không tự bảo đảm chất lượng/capacity. Ollama 4B hiện chưa pass
> safety gate; team sẽ đo 8B cùng manifest trên GPU BTC trước khi đề xuất. Privacy và accuracy
> phải được đánh giá song song.”

### “Audit có bất biến không?”

> “Ở tầng ứng dụng, sự kiện được append và Undo không xóa history. Nhưng chưa phải immutable audit
> chuẩn compliance; production cần append-only permission, WORM/hash chain và external retention.”

## 15. Checklist trước khi trả lời bất kỳ câu nào

1. Đây là fact, policy, authority hay infrastructure?
2. Ai có authority cuối cùng: model, C#, nhân viên hay quản lý?
3. File/class nào chịu trách nhiệm?
4. Failure có trở thành AUTO không? Nếu có thì đó là bug nghiêm trọng.
5. Test/live evidence nào chứng minh?
6. Evidence đo code correctness, model quality, usability hay availability?
7. Có đang biến smoke/fixture/regression thành production claim không?
8. Giới hạn nào cần nói ngay để câu trả lời đáng tin?

## 16. Cách ôn tài liệu này mà không học thuộc

Mỗi ngày chọn hai module. Với mỗi module, nói thành tiếng trong hai phút:

1. Trách nhiệm.
2. Happy path.
3. Failure path/fail-safe.
4. Test/evidence.
5. Limitation.

Sau đó mở source và tự tìm đúng method trong tối đa 60 giây. Nếu không tìm được, đánh dấu module
đó để ôn lại; không đọc lại toàn bộ tài liệu. Mục tiêu là biết điều hướng và lập luận, không phải
nhớ từng dòng.

## 17. Câu kết pitching

> “AURA không cố thay người phê duyệt. Sản phẩm biến ảnh hóa đơn thành facts có provenance, dùng
> rule tất định để phân tuyến, fail-safe khi evidence không đủ và giữ con người cùng Audit trong
> vòng quyết định. Evidence hiện tại chứng minh kiến trúc safety của supervised MVP; team công bố
> thẳng automation gap và có roadmap đo lường để tiến tới pilot an toàn.”
