# AURA - AI thẩm định hoàn ứng có kiểm soát

AURA là ứng dụng ASP.NET Core hỗ trợ bước thẩm định đầu tiên trong quy trình hoàn ứng của nhân viên. Vision model đọc dữ kiện trên hóa đơn, nhưng policy engine C# tất định mới quyết định hướng xử lý: tự động đi tiếp với ca thường quy rõ ràng hoặc chuyển các ca bất định, ngoài chính sách, có tín hiệu dữ liệu cá nhân và vượt thẩm quyền cho người duyệt.

## Hồ sơ nộp bài

| Hạng mục | Liên kết |
|---|---|
| Ứng dụng live | [AURA trên SmarterASP.NET](https://bondphupham-001-site1.ltempurl.com/) |
| Thư mục nộp video Sprint 1 và evaluation key chỉ dành cho BGK | [Team Drive](https://drive.google.com/drive/folders/1_EHs9-KghK2JWpQGRAu_jLWl9WmBkMLc?usp=sharing) |
| Slide chung kết sau feedback | [AURA 5 Slides](https://raw.githubusercontent.com/BondPhuPhamzZ/AURA/master/AURA/submission/AURA_5_SLIDES_AFTER_FEEDBACK_FINAL.pptx) |
| Proposal tiếng Việt | [AURA Proposal VI](https://raw.githubusercontent.com/BondPhuPhamzZ/AURA/master/AURA/submission/AURA_PROPOSAL_VI.pdf) |
| Proposal tiếng Anh | [AURA Proposal EN](https://raw.githubusercontent.com/BondPhuPhamzZ/AURA/master/AURA/submission/AURA_PROPOSAL_EN.pdf) |
| Build Log | [AURA Build Log](https://raw.githubusercontent.com/BondPhuPhamzZ/AURA/master/AURA/submission/AURA_BUILD_LOG.pdf) |
| Video demo chung kết | [Thư mục final video trên Google Drive](https://drive.google.com/drive/folders/1-v_I_FpoobrhNGtE3szcpjnNS6Hy5KA4?usp=sharing) |
| Health/readiness | [Live `/healthz`](https://bondphupham-001-site1.ltempurl.com/healthz) |

Nếu BGK cần evaluation key, hãy chia sẻ bằng kênh riêng. Không commit key, không ghi vào source/config và không để xuất hiện trong ảnh hoặc video. Lưu key bằng .NET User Secrets và thu hồi hoặc xoay key sau đánh giá.

## Luồng xử lý cốt lõi

```text
Upload một hoặc nhiều hóa đơn JPG/PNG
-> lưu hồ sơ và trả HTTP 202
-> worker dùng database gọi Qwen Vision
-> semantic validation và tối đa một lần repair
-> suy ra tổng tiền từ hóa đơn nếu người dùng không nhập
-> privacy triage chỉ ghi category, không lặp lại giá trị nhạy cảm
-> policy C# tất định quyết định
-> AUTO_APPROVE hoặc ESCALATE_FACT/POLICY/PRIVACY/AUTHORITY/SYSTEM_ERROR
-> nhân viên chuyển tiếp ngoại lệ
-> quản lý duyệt, từ chối hoặc hoàn tác
-> audit trail ghi lại toàn bộ vòng đời
```

Khả năng chính:

- OpenRouter/Qwen3.8 Flash qua Alibaba, với reasoning tắt để extraction ổn định, là bộ đọc hóa đơn mặc định; Ollama/Qwen3-VL-4B là phương án local tùy chọn.
- Xử lý nền dựa trên SQL vẫn tiếp tục sau refresh hoặc restart và không giữ request upload trong lúc chờ AI.
- Một lượt chọn tối đa 10 ảnh được tiếp nhận tuần tự qua cùng upload boundary; mỗi ảnh vẫn có case ID, hash, row SQL và status URL riêng. Candidate chưa có aggregate `InvoiceBatch` hoặc `batchId` phía server.
- Giao diện không yêu cầu người dùng nhập số tiền. Worker chỉ dùng `totalAmount` AI đọc từ dòng tổng thanh toán cuối sau schema và semantic validation; nếu thiếu bằng chứng thì fail-safe sang người kiểm tra. Backend vẫn nhận claimed amount cho test kit/API lịch sử, nhưng luồng demo không gửi trường này.
- Privacy triage phát hiện category cần xem xét, không hiển thị lại giá trị gốc; hồ sơ được fail-safe sang `ESCALATE_PRIVACY`. Request OpenRouter mặc định đặt `provider.data_collection=deny`.
- Tab **Tổng quan** tổng hợp số hồ sơ, route, privacy review, lỗi, giá trị và độ trễ theo tháng/ngày từ SQL; quản lý có thể tải CSV tháng hiện tại mà không xuất raw AI facts.
- Model chỉ trích xuất facts có cấu trúc; model không có quyền phê duyệt cuối.
- Evidence thiếu, mâu thuẫn hoặc không đọc được luôn fail-safe về người duyệt.
- Verify Harness chạy năm ca chính thức trong một thao tác: ba ca duyệt và hai ca chuyển tiếp.
- Danh tính người gửi, thao tác quản lý, provider metadata, lý do và Undo đều có thể audit.

## Business rules đang áp dụng

| Bản | Vai trò |
|---|---|
| [BUSINESS_RULES.md - English](AURA/BUSINESS_RULES.md) | Bản chuẩn đang áp dụng cho contract của model Qwen3.8 Flash và policy hiện hành |
| [BUSINESS_RULES_VI.md - Tiếng Việt](AURA/docs/BUSINESS_RULES_VI.md) | Bản dịch công khai để BGK rà soát nhanh; bị loại rõ ràng khỏi build output và gói publish |

Runtime chỉ đọc đúng đường dẫn `BUSINESS_RULES.md` được cấu hình và chèn bản tiếng Anh này vào system prompt để model trích xuất dữ kiện theo schema. Quyết định cuối được thực thi tất định trong `PolicyDecisionEngine.cs`, nên model không có quyền tự thay policy phê duyệt. Bản tiếng Việt chỉ phục vụ tra cứu và không được đưa vào build output hoặc gói publish.

## Baseline đã xác minh

Cập nhật kỹ thuật gần nhất: 17/10/2026. Health live và gate Verify Qwen3.8 Flash ngày 14/10 vẫn là baseline live đã đo; các cải tiến sau feedback doanh nghiệp bên dưới mới được kiểm thử offline và cần publish/smoke lại trước khi gọi là live.

- Build: **0 warning, 0 error**.
- Test tự động sau multi-file intake, AI-derived amount, privacy triage, dashboard và CSV report: **181/181 pass**; test offline không gọi AI API trả phí.
- Verify Harness chính thức: **5/5** với đúng 3 approve và 2 escalate.
- Mini-holdout 10 receipt mới do chủ dự án cung cấp, redaction và khóa trước đạt **8/10 exact**, **0 missed, 2 fail-safe over, 0 lỗi system/provider/429 và 28/30 field date/identifier/total cốt lõi**. Năm request đầu về terminal trong 64,763 giây và cả mười trong 108,803 giây ở AI concurrency 1. Bộ này chỉ chạy một lần; tương tác redaction được công bố nên không được gọi là production accuracy.
- Post-holdout regression sau đó gửi cùng 12 **source bytes nguyên bản**, không snapshot/resize/transform: **10/12 exact, 0 missed, 2 over, 0 lỗi system/provider/429, 35/36 critical field**. Năm upload khác nhau đồng thời hoàn tất 5/5 với E2E P95 **93,684 giây**. Lần này chỉ kiểm queue integrity, không phải yêu cầu latency Verify.
- Lần lặp tuần tự sau trên cùng known bytes đạt **11/12 exact**, **0 missed, 1 over, 0 lỗi system/provider/429 và E2E P95 32,903 giây**. GS25_2 trở lại AUTO còn FAHASA vẫn fail-safe. Đây là stability evidence, không phải holdout mới hay phép đo accuracy độc lập.
- Sau publish/recycle, Verify Harness Qwen3.8 Flash trên live đạt **5/5 trong 65,004 giây**, không có system/provider error. Năm client bấm đồng thời tạo một HTTP 200 và bốn HTTP 409, nên click trùng không tạo thêm AI batch trên deployment một process.

Chi tiết các phép đo lịch sử, manifest và ranh giới claim nằm trong [tổng hợp evidence](AURA/docs/EVALUATION_EVIDENCE.md), tránh biến README thành nhật ký benchmark.

Các kết quả trên chỉ hỗ trợ demo có giám sát, không hỗ trợ claim production accuracy. Hai tập receipt thật đã trở thành known data và không thể tái sử dụng cho một holdout mới. Accuracy claim và rollout không giám sát vẫn NO-GO. Test 4G/5G trên điện thoại thật đã pass; các gate còn lại là ba phiên người dùng có giám sát và rehearsal có bấm giờ trên commit đóng băng.

## Chạy tại máy local

### Yêu cầu

- Windows 10/11.
- .NET 8 SDK.
- SQL Server Express LocalDB (`MSSQLLocalDB`).
- PowerShell.
- Evaluation key OpenRouter lấy từ thư mục nộp bài được chia sẻ riêng.

Ollama không bắt buộc cho cấu hình mặc định.

### 1. Clone và restore tool

```powershell
git clone https://github.com/BondPhuPhamzZ/AURA.git
cd AURA\AURA

dotnet tool restore
dotnet restore
```

### 2. Cấu hình secret local

Thay `OPENROUTER_EVALUATION_KEY` bằng evaluation key. User Secrets lưu giá trị này ngoài Git.

```powershell
dotnet user-secrets set "OpenRouter:ApiKey" "OPENROUTER_EVALUATION_KEY"
dotnet user-secrets set "OpenRouter:Model" "qwen/qwen3.8-flash"
dotnet user-secrets set "OpenRouter:ReasoningEffort" "none"
dotnet user-secrets set "Vision:Provider" "OpenRouter"
dotnet user-secrets set "Vision:FallbackEnabled" "false"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\mssqllocaldb;Database=AuraDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Connect Timeout=5;ConnectRetryCount=0"
```

Không chạy `dotnet user-secrets list` khi chia sẻ hoặc quay màn hình vì lệnh có thể in key.

### 3. Khởi động LocalDB và cập nhật database

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Test-DemoReadiness.ps1" -StartLocalDb -SkipHttp -DiagnoseLocalDb
dotnet ef database update
```

Pre-app check phải báo `READY: 0 failures`. Warning do chủ động dùng `-SkipHttp` là bình thường. Warning kiểm tra registry LocalDB chỉ mang tính chẩn đoán; không xóa registry key, LocalDB instance hoặc MDF chỉ để làm warning biến mất.

### 4. Chạy AURA

```powershell
dotnet run
```

Mở `http://localhost:5000` hoặc URL mà .NET in ra.

Trong cửa sổ PowerShell thứ hai, trở lại thư mục có `AURA.csproj` rồi kiểm ứng dụng đang chạy:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Test-DemoReadiness.ps1" -DiagnoseLocalDb
```

Kỳ vọng: `READY: 0 failures`, health `status=ok`, database và storage available, không có pending migration, provider `OpenRouter`, `fallbackEnabled=false`.

### 5. Dùng ứng dụng

1. Chọn **Chạy Verify Harness (90s)** để chạy năm ca chính thức.
2. Hoặc upload tối đa 10 receipt JPG/PNG. Claimed amount chỉ cần nhập cho một ảnh; nếu bỏ trống, AURA dùng tổng tiền AI đọc được sau validation.
3. Với kết quả `ESCALATE_*`, chọn **Chuyển tiếp**.
4. Trong **Quản lý**, duyệt hoặc từ chối hồ sơ.
5. Mở **Lịch sử hành vi** để xem audit trail hoặc Undo quyết định quản lý gần nhất.

### 6. Chạy test offline

```powershell
dotnet build AURA.csproj --no-restore
dotnet test tests\AURA.Tests\AURA.Tests.csproj --no-restore
dotnet ef migrations has-pending-model-changes --no-build
```

Kỳ vọng cho candidate hiện hành: build 0 warning/error, 181/181 test pass và không có model change cần migration.

Dừng app bằng `Ctrl+C`. Không cần reset database hoặc xóa LocalDB.

## Tài liệu quan trọng

- [Chỉ mục gói nộp bài](AURA/submission/README.md)
- [Chỉ mục tài liệu kỹ thuật](AURA/docs/README.md)
- [Hướng dẫn BGK chạy local và xử lý lỗi](AURA/docs/JUDGE_LOCAL_SETUP.md)
- [Kiến trúc và tích hợp](AURA/ARCHITECTURE_AND_INTEGRATION_REPORT.md)
- [Business rules tiếng Anh - bản chuẩn hiện hành](AURA/BUSINESS_RULES.md)
- [Business rules tiếng Việt - bản dịch dành cho BGK](AURA/docs/BUSINESS_RULES_VI.md)
- [Đặc tả workflow](AURA/submission/AURA_WORKFLOW_SPEC.md)
- [Hướng dẫn dataset và đánh giá mở rộng](AURA/docs/DATASET_EVALUATION_GUIDE.md)
- [Trạng thái đã xác minh hiện hành](AURA/docs/CURRENT_PROJECT_STATUS.md)
- [Checklist kiến trúc sau feedback và recommend check](AURA/docs/AFTER_FEEDBACK_ARCHITECTURE_CHECKLIST.md)
- [Workflow mailbox an toàn và thứ tự triển khai](AURA/docs/RECOMMENDED_INGESTION_WORKFLOW.md)
- [Tổng hợp bằng chứng đánh giá](AURA/docs/EVALUATION_EVIDENCE.md)
- [Kế hoạch khoanh vùng evidence](AURA/docs/EVIDENCE_LOCALIZATION_PLAN.md)
- [Protocol đánh giá ba người dùng](AURA/docs/USER_STUDY_PROTOCOL.md)
- [Checklist nộp bài](AURA/docs/SUBMISSION_CHECKLIST.md)
- [Thiết lập Ollama local tùy chọn](AURA/docs/LOCAL_OLLAMA.md)

## Giới hạn hiện tại

- Mỗi batch nhận tối đa 10 receipt JPG/PNG, tối đa 5 MB mỗi ảnh; chưa hỗ trợ PDF hoặc tài liệu nhiều trang.
- Chưa có connector email/folder tự động. Đây là bước tiếp theo cần OAuth/OIDC, least privilege, consent, idempotency, backfill và audit trước khi nối vào cùng ingestion boundary.
- Privacy triage là tín hiệu vận hành, không phải kết luận tuân thủ pháp luật. `RequireZeroDataRetention` mặc định tắt cho tới khi route/provider live được xác nhận tương thích.
- OpenRouter cần Internet và provider credit khả dụng.
- Ollama fallback là opt-in và không phải đường capacity chính thức cho concurrency.
- Production vẫn cần authentication/RBAC, receipt storage quản lý tập trung, chính sách retention/privacy, backup/restore, monitoring và malware scanning.

## Tác giả

**Phạm Gia Phú** - The Deciders, VNG Track, MLAI Hackathon 2026.
