# Architecture and Integration Report

## AURA Automated Underwriting and Reimbursement AI

Cập nhật ngày 28/09/2026. Tài liệu mô tả baseline sau feedback Sprint 1 và phần hardening chuẩn bị Sprint 2. Cách đánh giá chuẩn là clone và chạy localhost theo README. OpenRouter vẫn là provider chính; Ollama là fallback local có kiểm soát và mặc định chưa bật.

## 1. Sơ đồ thành phần

```text
AURA/
├── Program.cs                         # DI, middleware, SQL Server, /healthz
├── Controllers/
│   ├── ApplicantController.cs         # upload, xem ảnh, chuyển quản lý
│   ├── VerifyController.cs            # chạy 5 ca Verify qua cùng pipeline thật
│   ├── ReviewerController.cs          # quản lý đồng ý, từ chối, hoàn tác
│   └── HomeController.cs              # trang chính và các component đọc dữ liệu
├── Services/
│   ├── ConfiguredVisionExtractor.cs          # chọn provider bằng cấu hình
│   ├── OpenRouterVisionExtractorService.cs   # Qwen 8B hosted
│   ├── OllamaVisionExtractorService.cs       # Qwen 4B local tùy chọn
│   ├── ReceiptExtractionContract.cs          # prompt, schema và parser dùng chung
│   ├── ReceiptSemanticValidator.cs           # kiểm mâu thuẫn semantic và hướng dẫn repair
│   ├── PolicyDecisionEngine.cs               # quyết định nghiệp vụ tất định
│   ├── ReimbursementRepository.cs            # transaction hồ sơ và audit
│   ├── AuditLogger.cs                        # ghi sự kiện
│   ├── AuditTrailProjector.cs                # gom sự kiện thành timeline UI
│   ├── ReceiptProcessingWorker.cs             # worker DB-backed xử lý upload nền
│   ├── VisionCircuitBreaker.cs                # ngắt provider chính theo ngưỡng lỗi
│   └── WorkflowOperationGate.cs               # chỉ chống chạy chồng Verify Harness
├── Data/                              # EF Core DbContext và migrations
├── Models/                            # request, facts AI, audit, DTO
├── Views/                             # Razor UI nhân viên, quản lý, lịch sử
├── wwwroot/test_data/                 # 5 fixture được Verify chạy trực tiếp
├── test_kit/                          # ngân hàng 30 ca và gói BGK 15 ca
├── tests/AURA.Tests/                  # 80 kiểm thử tự động offline
├── docs/                              # runbook, deploy, test, checklist
├── submission/                        # 5 slide, Build Log Word, workflow
└── tools/                             # tái tạo fixture, chạy evaluator và sinh artifact
```

## 2. Kiến trúc triển khai

```text
Trình duyệt
    │ HTTPS
    ▼
SmarterASP.NET / IIS / ASP.NET Core 8
    ├── Razor UI và controller
    ├── DB-backed receipt queue + background worker
    ├── PolicyDecisionEngine
    ├── SQL Server: hồ sơ và audit event
    └── App_Data/receipts: ảnh riêng tư, tên ngẫu nhiên và SHA-256
             │
             ▼
    ConfiguredVisionExtractor
       ├── chính: OpenRouter → Qwen3-VL-8B-Instruct
       └── fallback có cờ bật: Ollama → Qwen3-VL-4B-Instruct
```

Qwen chỉ trích xuất dữ kiện nhìn thấy trong ảnh. Quyết định `AUTO_APPROVE` hay `ESCALATE_*` nằm trong C# policy engine. Sprint 1 chưa có đăng nhập theo vai trò, nên mọi bản demo công khai chỉ dùng dữ liệu tổng hợp và không phù hợp để nhận hóa đơn cá nhân thật.

## 3. Route và hợp đồng chính

| Route | Phương thức | Vai trò |
|---|---|---|
| `/` | GET | Dashboard nhân viên, quản lý và lịch sử |
| `/healthz` | GET | Readiness: policy, cấu hình AI, kết nối DB và thư mục receipt; không gọi provider và không lộ API key |
| `/Applicant/UploadReceipt` | POST | Kiểm file, lưu hồ sơ `PENDING`, trả `202 Accepted` và đánh thức worker |
| `/Applicant/Status/{id}` | GET | Trả trạng thái `PENDING/PROCESSING/COMPLETED/FAILED` để UI poll |
| `/Applicant/Receipt/{id}` | GET | Đọc ảnh từ storage riêng tư theo mã hồ sơ |
| `/Applicant/ForwardToManager` | POST | Chuyển một hồ sơ cần người quyết định |
| `/Applicant/ForwardAllToManager` | POST | Chuyển toàn bộ hồ sơ đang chờ |
| `/Verify/RunHarness` | POST | Chạy tuần tự 5 fixture bằng pipeline production |
| `/Reviewer/EscalateAction` | POST | Quản lý chọn `YES`, `NO` hoặc `UNDO` |
| `/Home/EmployeeEscalations` | GET | Fragment hàng đợi nhân viên |
| `/Home/ReviewerQueue` | GET | Fragment hàng đợi quản lý |
| `/Home/AuditTrail` | GET | Fragment timeline audit |

Các POST thay đổi trạng thái đều kiểm antiforgery token. Upload chỉ nhận JPG/PNG tối đa 5 MB và server kiểm cả MIME, magic bytes lẫn kích thước.

## 4. Luồng xử lý một hóa đơn

1. Server xác thực file và số tiền đề nghị.
2. `ReceiptStorage` lưu ảnh ngoài `wwwroot`, đặt tên ngẫu nhiên và tính SHA-256.
3. Server ghi hồ sơ `PENDING` cùng audit `AI_QUEUED`, trả HTTP `202` và status URL. Request HTTP không giữ kết nối trong lúc model suy luận.
4. `ReceiptProcessingWorker` claim job bằng update có điều kiện, gắn lease và tăng số lần thử. Job tồn tại trong SQL Server nên app restart không làm mất hàng đợi; lease hết hạn cho phép worker lấy lại job bị gián đoạn.
5. `ConfiguredVisionExtractor` gọi provider chính. Khi bật fallback, chỉ lỗi hạ tầng nằm trong allowlist mới được chuyển một lần sang provider dự phòng. Hai adapter dùng chung prompt, JSON Schema và parser qua `ReceiptExtractionContract`.
6. `ReceiptSemanticValidator` kiểm ý nghĩa chéo: VND không được có phần thập phân, loại chứng từ phải phù hợp identifier, mã không được gán nhầm trường và line items phải đối chiếu được với tổng tiền khi không có discount.
7. Nếu JSON đúng schema nhưng mâu thuẫn semantic, provider đọc lại ảnh đúng một lần với danh sách lỗi cụ thể. Repair không nhận số tiền khai báo. Nếu vẫn mâu thuẫn hoặc repair lỗi, facts đầu tiên được đánh dấu và policy chuyển `ESCALATE_FACT`.
8. Worker chạy `PolicyDecisionEngine`, commit kết quả, provider thực sự phục vụ và audit. UI poll mỗi giây trong phiên trình duyệt, lưu status URL trong `sessionStorage` và tự nối lại sau refresh.
9. Nhân viên xác nhận chuyển tiếp. Quản lý đồng ý hoặc từ chối theo câu hỏi đã sinh; quyết định có thể hoàn tác.
10. UI gom các audit event của cùng hồ sơ thành một timeline, tránh hiển thị các dòng trùng nghĩa.

Upload, chuyển tiếp và quyết định quản lý không dùng khóa toàn cục. SQL Server `RowVersion` phát hiện hai thao tác cùng sửa một hồ sơ và trả conflict thay vì ghi đè. `WorkflowOperationGate` chỉ còn bảo vệ Verify Harness khỏi hai lượt 5 ca chạy chồng và phát sinh quota ngoài ý muốn.

Nếu AI lỗi xác thực, rate limit, schema hoặc provider, AURA trả mã lỗi rõ ràng và chuyển `ESCALATE_SYSTEM_ERROR`. Hệ thống không dùng mock ngầm và không tạo PASS giả.

## 5. Ma trận quyết định

| Nhánh | Điều kiện chính | Kết quả |
|---|---|---|
| Dữ kiện đủ và hợp lệ | Identifier, ngày, tổng tiền, trạng thái và dòng hàng phù hợp | `AUTO_APPROVE` |
| Dữ kiện thiếu hoặc đáng ngờ | Thiếu identifier, ảnh mờ/crop, amount mismatch, draft/refund | `ESCALATE_FACT` |
| Ngoài chính sách | Hạng mục bị loại trừ theo `BUSINESS_RULES.md` | `ESCALATE_POLICY` |
| Vượt thẩm quyền | Số tiền vượt hạn mức được cấu hình | `ESCALATE_AUTHORITY` |
| AI hoặc hạ tầng lỗi | Auth, 429, 5xx sau retry giới hạn, JSON không hợp lệ | `ESCALATE_SYSTEM_ERROR` |

Khi một hồ sơ có nhiều vấn đề, precedence là `FACT > POLICY > AUTHORITY`. Duplicate byte luôn được ghi audit; cấu hình demo có thể không chuyển tiếp duplicate để BGK chạy lại cùng fixture.

## 6. Hợp đồng provider vision

- `Vision__Provider=OpenRouter` là mặc định và giữ nguyên bản deploy Sprint 1. Endpoint dùng OpenRouter Chat Completions qua HTTPS với `qwen/qwen3-vl-8b-instruct`.
- `Vision__Provider=Ollama` gọi REST local `http://127.0.0.1:11434/api/chat` với `qwen3-vl:4b-instruct`. HTTP chỉ được chấp nhận trên loopback; endpoint khác phải dùng HTTPS.
- Cả hai provider nhận cùng policy, JSON Schema và parser. Điều này cho phép benchmark model mà không đổi controller hoặc policy engine.
- OpenRouter không retry HTTP 429 và retry tối đa một lần cho 5xx. Cả hai adapter chỉ semantic-repair tối đa một lần khi đã có JSON hợp lệ nhưng tự mâu thuẫn; đây không phải retry mù lỗi mạng.
- `Vision:FallbackEnabled=false` theo mặc định. Khi bật, hệ thống chỉ fallback một lần cho timeout, 429, auth/credit, model/provider unavailable, HTTP hạ tầng hoặc Ollama local unavailable. Lỗi schema, semantic, request invalid, response truncated/empty không được fallback vì đổi model có thể che lỗi dữ liệu hoặc contract.
- Circuit breaker mở sau số lỗi hạ tầng liên tiếp được cấu hình, dùng fallback trong thời gian cooldown, rồi cho provider chính một lần probe để tự phục hồi. Audit ghi `PrimaryProvider`, `ServedProvider`, `FallbackUsed` và `ProviderErrorCode`.
- API key chỉ lưu trong user-secrets local hoặc Pool Manager production. Ollama local không cần API key và không được mở port ra Internet.

## 7. Database, storage và audit

EF Core dùng SQL Server. Migration `AddDurableReceiptProcessing` thêm trạng thái queue, lease, attempt, provider metadata, ngữ cảnh người gửi và composite index `(ProcessingState, QueuedAt)` cùng `(RequestId, Timestamp)`. `Database__ApplyMigrationsOnStartup` cho phép migration có kiểm soát khi khởi động. Ảnh nằm ở `ReceiptStorage__Directory`, không nằm trong static web root.

Audit được ghi theo sự kiện để không mất dấu hành động. Giao diện chiếu các sự kiện thành một hồ sơ duy nhất với trạng thái hiện tại, câu hỏi, câu trả lời quản lý và khả năng hoàn tác. Thiết kế này giữ lịch sử mà không tạo cảm giác lưu trùng ba bản ghi nghiệp vụ.

## 8. Dữ liệu kiểm thử

| Vị trí | Mục đích | Có được chạy tự động bởi Verify không |
|---|---|---|
| `wwwroot/test_data/` | 5 ảnh và expected result của Verify Harness | Có |
| `test_kit/judge-manifest.json` | 15 ca challenge để BGK đọc, tạo biến thể hoặc chạy qua runner ngoài UI | Không chạy bởi Verify |
| `test_kit/manifest.json` và `test_kit/images/` | Ngân hàng 30 ca tổng hợp cho benchmark có kiểm soát | Không chạy bởi Verify |
| `test_kit/local_real/` | Ảnh thật đã được phép dùng và ẩn danh, chỉ test thủ công | Không và ảnh bị Git ignore |
| `publish/` | Output build cục bộ, bị Git ignore; có thể chứa bản fixture cũ | Không phải nguồn dữ liệu chuẩn |

`tools/generate_verify_receipts.py` tạo ngân hàng 30 ca với seed cố định, sau đó sao chép 5 ca đầu sang `wwwroot/test_data`. `tools/Invoke-ExtendedDatasetEvaluation.ps1` chạy 15/30 ca qua đúng upload endpoint và xuất CSV/JSON evidence mà không thêm nút vào dashboard. Bản trong `publish/` chỉ là output cũ; Web Deploy phải tạo package mới từ source thay vì tải thủ công folder này.

## 9. Biến môi trường production

| Biến | Chức năng |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | Chọn cấu hình runtime |
| `Vision__Provider` | `OpenRouter` mặc định hoặc `Ollama` local |
| `Vision__FallbackEnabled` | Bật fallback có kiểm soát; mặc định `false` |
| `Vision__FallbackProvider` | Provider dự phòng, mặc định `Ollama` |
| `Vision__CircuitBreakFailureThreshold` | Số lỗi hạ tầng liên tiếp trước khi mở circuit |
| `Vision__CircuitBreakSeconds` | Thời gian cooldown trước primary probe |
| `OpenRouter__ApiKey` | API key bí mật |
| `OpenRouter__Model` | Model Qwen |
| `Ollama__BaseUrl` | Endpoint local; HTTP chỉ cho loopback |
| `Ollama__Model` | Tag model local Qwen3-VL |
| `ReceiptStorage__Directory` | Folder lưu ảnh riêng tư |
| `ReceiptStorage__MaxFileSizeMb` | Giới hạn upload |
| `ConnectionStrings__DefaultConnection` | SQL Server production |
| `Database__ApplyMigrationsOnStartup` | Bật/tắt migration khi khởi động |
| `ReceiptProcessing__PollIntervalMs` | Chu kỳ DB recovery poll; signal nội bộ đánh thức worker ngay |
| `ReceiptProcessing__LeaseSeconds` | Thời gian lease trước khi job gián đoạn được reclaim |
| `Verify__InterCaseDelayMs` | Delay cấu hình giữa các fixture Verify, mặc định 4000 ms |

Recycle application pool chỉ nạp lại biến môi trường hiện có. Không cần tải lại publish XML hoặc publish lại code nếu chỉ thay giá trị Pool Manager. SmarterASP.NET tiếp tục đặt `Vision__Provider=OpenRouter`; Ollama dành cho máy local.

## 10. Bằng chứng xác minh hiện tại

- Build .NET 8 sạch, 0 warning và 0 error tại lần kiểm tra gần nhất.
- 80 automated tests pass, gồm policy, workflow, audit, Verify/Test Kit integrity, hợp đồng OpenRouter/Ollama, semantic validation/repair và fallback/circuit breaker.
- Build sau hardening: 0 warning, 0 error; EF báo không có model change chưa migration. LocalDB đã áp migration thành công.
- EF Core SQL Server/Tools và local `dotnet-ef` đã được vá đồng bộ lên 8.0.31; NuGet vulnerability scan không còn advisory trong app và test project tại thời điểm kiểm tra.
- Runner đánh giá mở rộng đã qua kiểm tra cú pháp; chưa gọi 15/30 request thật sau thay đổi để tránh tiêu quota trước khi người vận hành chốt phiên đo.
- Build cuối với Ollama/Qwen3-VL-4B Q4_K_M đạt 25/25 qua năm lượt Verify liên tiếp trên 5 fixture tổng hợp; ba lượt có đo mất khoảng 303–304 giây/batch trên RTX 3050 Laptop 4 GB. OpenRouter/Qwen3-VL-8B cùng build đạt 15/15 qua ba batch và upload `HoaDon1.jpg` 3/3; adjusted latency median 4,371 giây, P95 11,317 giây. Exact match năm field khóa là 72/75 do TC-04 lệch taxonomy nhưng không làm sai nhánh policy. Đây không phải accuracy trên dữ liệu thực; xem `docs/OPENROUTER_BENCHMARK_2026-09-27.md`.
- Người dùng xác nhận production upload, AI extraction và audit hoạt động đúng sau khi cập nhật API key ở Pool Manager.
- Video demo dưới ba phút đã được liên kết từ README; đường đánh giá tái lập cho BGK vẫn là localhost cùng test key được cấp riêng.

Kết quả fixture tổng hợp không phải accuracy trên tập hóa đơn độc lập.

## 11. Quyết định kiến trúc và giới hạn

| Quyết định | Lý do |
|---|---|
| Tách extraction và policy | Có thể đổi model mà không đổi quy tắc duyệt; policy unit-test được |
| Hosted Qwen 8B cho Sprint 1 | Triển khai nhanh và giữ baseline đang được chấm |
| Adapter Ollama 4B tắt mặc định | Đã đạt cổng Verify tổng hợp 25/25 nhưng chậm trên RTX 3050 4 GB; OpenRouter cùng build nhanh hơn rõ rệt. Cả hai chưa có benchmark hóa đơn thực tế độc lập |
| Semantic validation + một repair | Sửa lỗi đọc dấu hàng nghìn/gán nhầm identifier mà không nới policy hoặc dùng claimed amount để dẫn dắt OCR |
| Fallback mặc định tắt, có allowlist và circuit breaker | Có đường dự phòng khi demo nhưng không đổi model cho lỗi semantic/contract; audit chỉ rõ provider quyết định |
| Upload xử lý nền bằng DB state + lease | Request HTTP ngắn, chịu được refresh/restart và không khóa 4-5 người quan sát/thao tác |
| Verify vẫn đúng 5 ca và có gate riêng | Giữ baseline BTC, tránh hai batch chạy chồng và bảo vệ quota |
| Evaluator 15/30 ca nằm ngoài UI | Giữ dashboard demo gọn, vẫn chạy đúng production endpoint và bảo toàn mapping ảnh/claim/ground truth |
| Fail-safe thay cho mock ngầm | Lỗi provider cần chuyển người xử lý, không được che bằng kết quả giả |
| Audit event và timeline projection | Giữ dấu vết đầy đủ nhưng UI chỉ hiển thị một hồ sơ nhất quán |
| Fixture tổng hợp tái lập | BGK có expected result rõ ràng mà không nhận dữ liệu cá nhân |

Baseline chưa hỗ trợ PDF/nhiều trang, antivirus, tra cứu MST/e-invoice, tỷ giá, object storage hoặc authentication thực. Ngữ cảnh người gửi hiện là metadata demo, chưa phải danh tính đã xác thực. Image resize chưa bật vì cần benchmark lại độ chính xác OCR trước khi thay đổi pixel đầu vào. Xem thêm [workflow đầy đủ](submission/AURA_WORKFLOW_SPEC.md), [runbook](docs/RUNBOOK.md), [tiến độ hardening](docs/SPRINT2_IMPLEMENTATION_PROGRESS_2026-09-28.md) và [hướng dẫn deploy](docs/DEPLOYMENT.md).
