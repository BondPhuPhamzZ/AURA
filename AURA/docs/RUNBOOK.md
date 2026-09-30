# AURA Runbook

## 1. Phụ thuộc

- .NET 8 SDK; bản 8.0.425 được khuyến nghị cho baseline hiện tại
- SQL Server LocalDB trên Windows hoặc SQL Server/Azure SQL có thể truy cập
- Local `dotnet-ef` 8.0.31 qua tool manifest
- OpenRouter API key có quyền gọi model cấu hình trong `OpenRouter:Model`

OpenRouter là provider chính cho cấu hình demo hiện tại. Cấu hình Ollama local tùy chọn nằm tại [`docs/LOCAL_OLLAMA.md`](LOCAL_OLLAMA.md); không cần cài Ollama để chạy baseline của BGK và không bật auto-fallback khi benchmark.

## 2. Clone và cấu hình local

```powershell
git clone https://github.com/BondPhuPhamzZ/AURA.git
cd AURA/AURA
dotnet tool restore
dotnet restore
dotnet user-secrets set "Vision:Provider" "OpenRouter"
dotnet user-secrets set "OpenRouter:ApiKey" "YOUR_OPENROUTER_KEY"
dotnet user-secrets set "OpenRouter:Model" "qwen/qwen3-vl-8b-instruct"
dotnet user-secrets set "Vision:FallbackEnabled" "false"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\mssqllocaldb;Database=AuraDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Connect Timeout=5;ConnectRetryCount=0"
dotnet ef database update
```

Không dùng `dotnet user-secrets list` khi quay video/chia sẻ màn hình vì lệnh có thể hiển thị secret.

## 3. Build, test và chạy

```powershell
dotnet build ../AURA.sln --no-restore
dotnet test tests/AURA.Tests/AURA.Tests.csproj --no-restore
dotnet run
```

Không thêm `--no-build` vào lệnh test trừ khi test project vừa được build đúng configuration; nếu không, CLI có thể chạy assembly cũ và báo sai số lượng test.

Trước khi mở AURA, kiểm tra cấu hình mà chưa gọi API AI:

```powershell
.\tools\Test-DemoReadiness.ps1 -StartLocalDb -SkipHttp
```

Sau khi `dotnet run` đã lắng nghe ở cổng 5000, mở PowerShell thứ hai và chạy cổng kiểm tra đầy đủ:

```powershell
.\tools\Test-DemoReadiness.ps1
```

Kết quả hợp lệ là `READY`, đúng database `AuraDb`, LocalDB running, health `ok`, `databaseUpToDate=true`, `pendingMigrationCount=0` và fallback tắt. Script không in API key hoặc toàn bộ connection string.

Nếu cần ghi bằng chứng LocalDB hoặc lỗi xuất hiện không ổn định, chạy lệnh read-only sau trong đúng Windows account dùng để demo:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Test-DemoReadiness.ps1" -StartLocalDb -SkipHttp -DiagnoseLocalDb
```

`sqllocaldb` đôi khi trả exit code 0 nhưng nội dung vẫn báo `registry configuration error`; readiness vì vậy kiểm cả code và text. Nếu gặp lỗi này, đóng AURA/Visual Studio và các phiên test, reboot một lần rồi chạy lại lệnh trên. Việc app vừa chạy bình thường chỉ chứng minh phiên đó kết nối được, không phủ định lỗi user-instance từng xảy ra. Không xóa instance, registry key hoặc file `.mdf` khi chưa backup và chưa xác nhận đường dẫn database.

Mở URL được in trong terminal. Không truy cập `/Verify` để tìm trang riêng; nút Verify nằm ngay trên trang chủ. `/Applicant` và `/Verify` chủ động chuyển về `/`.

## 4. Tái lập Verify

1. Bấm **Chạy Verify Harness (90s)** một lần.
2. Chờ tối đa năm request Qwen chạy tuần tự qua OpenRouter.
3. Xác nhận đúng 5 dòng, có timestamp và latency.
4. Kỳ vọng fixture v2: TC-01..03 `AUTO_APPROVE`, TC-04 `ESCALATE_FACT`, TC-05 `ESCALATE_POLICY`.
5. Nếu `429/5xx`, chờ retry. Nếu vẫn lỗi, kết quả phải là `ESCALATE_SYSTEM_ERROR`, tuyệt đối không PASS giả.

Fixture có thể tái tạo bằng Python/Pillow qua `tools/generate_verify_receipts.py --as-of-date 2026-09-21`. Script đồng thời sinh Test Kit v2 gồm 30 ca nhưng chỉ 5 ca đại diện được Verify gọi. Không đổi `as-of-date`, fixture hoặc expected sau khi chốt mà không cập nhật manifest, tài liệu và commit.

Để bảo toàn credit: build + 93 automated test offline trước, deploy, chạy đúng một ảnh smoke test, sau đó chỉ chạy **một lượt** Verify 5 ảnh trước khi quay video. Bộ 15/30 ca chỉ chạy trong phiên đánh giá riêng bằng runner ngoài UI sau khi xác nhận quota.

Delay giữa các ca Verify được cấu hình bằng `Verify:InterCaseDelayMs`, mặc định 4000 ms để bảo vệ quota. Không đổi giá trị trong cùng một benchmark; luôn ghi giá trị này vào metadata phép đo.

### Đánh giá mở rộng 15/30 ca, không thêm nút UI

Giữ dashboard gọn và giữ Official Verify đúng 5 ca. Khi cần chạy gói 15 ca:

```powershell
.\tools\Invoke-ExtendedDatasetEvaluation.ps1 -BaseUrl "http://localhost:5000" -ManifestPath ".\test_kit\judge-manifest.json" -ImagesDirectory ".\test_kit\images" -MaxCases 15 -InterCaseDelaySeconds 4
```

Runner gọi đúng upload production, poll trạng thái và ghi `metadata.json`, `results.csv`, `results.json`, `summary.json` vào `test_kit/results/<timestamp>`. Gói 30 ca dùng `manifest.json` và `-MaxCases 30`. Xem `docs/DATASET_EVALUATION_GUIDE.md` trước khi dùng ảnh thực tế hoặc công bố số liệu.

Trong demo, `DecisionPolicy:EscalateDuplicateReceipts=false` cho phép chạy lại cùng ảnh nhưng vẫn ghi nhận trùng trong audit. Trước production, đổi thành `true`. Thay đổi cấu hình này không cần sửa code.

Nếu một ca dừng gần đúng thời gian `OpenRouter:TimeoutSeconds`, đó là `AI_TIMEOUT`, không phải model “học kém đi”. Harness dừng gọi AI cho các ca còn lại sau timeout, rate limit, lỗi xác thực/credit/model hoặc lỗi contract JSON mang tính hệ thống để bảo vệ chi phí. HTTP 429 không được tự động retry.

### Model OpenRouter cho demo

- Mặc định demo: `qwen/qwen3-vl-8b-instruct`, model vision-language 8B tập trung OCR/document và hỗ trợ structured output bằng JSON Schema.
- Qwen3-VL-4B-Instruct là ứng viên self-host tiếp theo, nhưng không được khai báo như route OpenRouter khi catalog chưa cung cấp endpoint đó.
- HTTP 404 / `AI_MODEL_UNAVAILABLE`: slug model sai, đã bị gỡ hoặc hiện không có endpoint; đây không phải quota.
- HTTP 401/403 / `AI_AUTH_ERROR`: key sai hoặc thiếu quyền.
- HTTP 402 / `AI_CREDITS_REQUIRED`: tài khoản không đủ credit hoặc key không được phép dùng model.
- HTTP 429 / `AI_RATE_LIMIT`: rate limit của OpenRouter/provider; ứng dụng không tự retry để tránh phát sinh thêm chi phí.
- Đổi model bằng `OpenRouter:Model`; luôn xác nhận model nhận input ảnh trên catalog trước khi đổi.

### Provider Ollama local tùy chọn

- Chọn bằng `Vision:Provider=Ollama`; quay lại baseline bằng `Vision:Provider=OpenRouter`.
- Model mặc định local là `qwen3-vl:4b-instruct`; một request tại một thời điểm, context 8192 và timeout 180 giây.
- Fallback mặc định tắt để benchmark không trộn provider. Khi demo cần đường dự phòng, bật `Vision:FallbackEnabled=true`, giữ `Vision:Provider=OpenRouter` và đặt `Vision:FallbackProvider=Ollama` sau khi smoke Ollama.
- Fallback chỉ chạy một lần cho lỗi hạ tầng đủ điều kiện. Lỗi schema, semantic, truncated response hoặc invalid request vẫn chuyển kiểm tra thủ công.
- Sau JSON Schema, backend kiểm tra ngữ nghĩa tiền VND, loại chứng từ và các identifier. Dữ kiện mâu thuẫn được đọc lại đúng một lần; vẫn sai thì chuyển `ESCALATE_FACT`, không tự đoán hoặc tự nhân số tiền.
- `/healthz` chỉ đạt `ok` khi policy, cấu hình AI, database, migration và thư mục receipt sẵn sàng; endpoint không gọi provider nên không thay cho một ảnh smoke test.
- Baseline năm fixture ngày 27/09/2026 từng đạt 25/25 bằng Ollama 4B. Trên judge set mở rộng 15 ca ngày 29/09/2026, Ollama đạt 14/15 và bỏ sót escalation TK-12, trong khi OpenRouter đạt 15/15. Vì vậy Ollama chỉ là đường offline/manual có giới hạn; không dùng kết quả năm fixture để tuyên bố độ chính xác thực tế hoặc bật auto-fallback toàn cục.
- Xem hướng dẫn cài, giới hạn RAM và rollback tại `docs/LOCAL_OLLAMA.md`.

### Khi OpenRouter/Qwen trả lỗi

1. Mở **OpenRouter → Activity/Logs** và đối chiếu HTTP status, model, provider, token và chi phí.
2. Với `401/403`, xác nhận secret thuộc đúng tài khoản đã nạp credit và key chưa bị thu hồi/giới hạn bởi guardrail.
3. Với `402`, kiểm tra số dư và giới hạn chi tiêu riêng của API key. `Key limit` là trần chi tiêu, không phải số dư.
4. Với `429`, dừng vài phút rồi thử đúng **một ảnh**; không bấm Verify liên tục.
5. Với `5xx/timeout`, kiểm tra trang trạng thái OpenRouter/provider. Hệ thống phải giữ `ESCALATE_SYSTEM_ERROR`, không dùng kết quả giả hoặc cache cũ như một lần gọi AI mới.
6. Chỉ đổi model sau khi chạy lại ma trận 5 ca và xác nhận input ảnh, JSON Schema, latency và câu hỏi chuyển tiếp.

### Pipeline xử lý nền và fallback

1. Luôn chạy `dotnet ef database update` sau khi pull commit có migration mới.
2. Upload hợp lệ trả `202 Accepted` sau khi lưu file và record `PENDING`; model không còn giữ request HTTP mở.
3. Worker claim job bằng lease; UI gọi `GET /Applicant/Status/{id}` đến khi `COMPLETED` hoặc `FAILED`.
4. Refresh trong cùng tab không làm mất theo dõi vì status URL nằm trong `sessionStorage`. Nếu đóng tab, kết quả vẫn nằm trong database và audit.
5. Audit `AI_QUEUED` chứng minh request đã nhận; audit `AI_PROCESSED_*` ghi provider chính, provider phục vụ, fallback flag, error code và latency AI.
6. Circuit breaker mặc định mở sau ba lỗi hạ tầng liên tiếp, dùng fallback 60 giây rồi cho provider chính một lượt probe.

Để bật fallback cho demo trên máy cá nhân:

```powershell
ollama list
Invoke-RestMethod 'http://127.0.0.1:11434/api/tags'
dotnet user-secrets set "Vision:Provider" "OpenRouter"
dotnet user-secrets set "Vision:FallbackEnabled" "true"
dotnet user-secrets set "Vision:FallbackProvider" "Ollama"
```

Khi benchmark từng provider, bắt buộc đặt `Vision:FallbackEnabled=false`; nếu không, một kết quả thành công có thể đến từ fallback và làm sai phép so sánh.

## 5. Cấu hình deploy

Các biến môi trường bắt buộc:

```text
OpenRouter__ApiKey=<secret>
OpenRouter__Model=qwen/qwen3-vl-8b-instruct
Vision__Provider=OpenRouter
ConnectionStrings__DefaultConnection=<SQL Server connection string>
ReceiptStorage__Directory=<persistent volume path>
Database__ApplyMigrationsOnStartup=true
```

Quy trình release:

```powershell
dotnet test tests/AURA.Tests/AURA.Tests.csproj -c Release
dotnet publish AURA.csproj -c Release -o publish
dotnet ef database update --connection "<DEPLOYMENT_CONNECTION>"
```

Khi deploy public, ứng dụng phải chạy sau HTTPS/reverse proxy. Container Linux lắng nghe cổng `8080`; health endpoint là `/healthz`. Persistent volume phải tồn tại qua lần restart/redeploy; kiểm tra bằng cách upload một ảnh, restart instance, rồi mở lại link **Xem hóa đơn** trong Audit. Hướng dẫn SmarterASP.NET và các giới hạn Render nằm tại `docs/DEPLOYMENT.md`.

## 6. Checklist trước nộp

- Trang chủ public trả `200`, không yêu cầu tài khoản.
- POST Verify thiếu CSRF trả `400`; nút UI có token và chạy được.
- `/BUSINESS_RULES.md` trả `404` vì policy không được public từ static root.
- Upload giả MIME bị từ chối; JPG/PNG hợp lệ được lưu và mở lại qua route chứng từ.
- Ảnh lớn hơn 5 MB bị chặn với thông báo dễ hiểu, không xuất hiện lỗi `Unexpected end of JSON input`. Giới hạn multipart có phần đệm cho antiforgery/boundary nhưng controller vẫn khóa riêng file ở 5 MB.
- Verify đạt 5/5 trong thời gian demo cho phép. Timeout cấu hình là 90 giây cho OpenRouter và 180 giây cho Ollama; đây là chốt lỗi kỹ thuật, không phải tuyên bố latency mục tiêu.
- Audit hiển thị input, action, timestamp, reason; chuyển tiếp/Đồng ý/Từ chối/undo hoạt động.
- Ca `ESCALATE_*` xuất hiện ở cửa sổ nhân viên trước; bấm **Chuyển tiếp** rồi mới xuất hiện ở cửa sổ quản lý.
- Nhân viên có thể chuyển từng hồ sơ hoặc **Chuyển tiếp tất cả**; mỗi hồ sơ phải có audit `EMPLOYEE_FORWARDED_TO_MANAGER`.
- Quản lý bấm **Đồng ý duyệt** hoặc **Từ chối duyệt**; kiểm tra toast và audit `MANAGER_YES`/`MANAGER_NO` chứa câu hỏi, câu trả lời và outcome.
- Trạng thái quyết định và audit được lưu cùng một lần EF Core `SaveChanges`, tránh trạng thái đổi nhưng thiếu nhật ký.
- Ảnh POS có MID/TID, ShopID, POS/register hoặc pager/table không được dùng thay mã riêng của giao dịch. Chỉ `VAT_INVOICE` bắt buộc seller tax ID; retail/restaurant/POS receipt có `invoiceNumber`, `receiptNumber` hoặc `transactionReference` hợp lệ có thể không có MST. Ảnh hóa đơn nháp/chưa phát hành phải chuyển FACT.
- Ảnh e-commerce được phép thiếu MST/invoice number nếu có order/booking/tracking/receipt ID cùng trạng thái hoàn tất, ngày giao dịch/thanh toán, merchant, total, currency và line items đáng tin cậy. Ngày giao hàng không tự thay ngày giao dịch; mã vận chuyển không được gọi là MST hay hóa đơn thuế.
- README có hướng dẫn localhost tái lập; Live URL chỉ cần điền vào form/video nếu nhóm tiếp tục công bố môi trường hosting tùy chọn.
- API key/connection string không xuất hiện trong Git hoặc video.
