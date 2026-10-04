# Hướng dẫn BGK chạy AURA trên máy Windows

Tài liệu này mô tả baseline OpenRouter đơn giản nhất. BGK không cần cài Ollama. API key phải được nhận qua kênh riêng và lưu bằng .NET User Secrets, không ghi vào repository.

## 1. Điều kiện máy

- Windows 10 hoặc 11.
- Git.
- .NET 8 SDK. Baseline khuyến nghị 8.0.425.
- SQL Server Express LocalDB. Visual Studio có workload ASP.NET thường đã cài LocalDB.
- OpenRouter API key có quyền gọi `qwen/qwen3-vl-8b-instruct`.
- Trình duyệt Chrome hoặc Edge.

Kiểm tra nhanh:

```powershell
git --version
dotnet --info
sqllocaldb info
```

Nếu `dotnet` hoặc `sqllocaldb` không được nhận diện, cài dependency tương ứng rồi mở PowerShell mới. Không cần Run as Administrator cho các lệnh AURA thông thường.

## 2. Clone repository

Mở Windows PowerShell tại thư mục muốn lưu source:

```powershell
git clone https://github.com/BondPhuPhamzZ/AURA.git
cd .\AURA\AURA
```

Kiểm tra đúng thư mục:

```powershell
Test-Path -LiteralPath ".\AURA.csproj"
```

Kết quả phải là `True`.

## 3. Restore tool và package

```powershell
dotnet tool restore
dotnet restore
```

Không cần tự cài `dotnet-ef` global; repository đã pin tool local.

## 4. Lưu cấu hình an toàn

Thay `OPENROUTER_EVALUATION_KEY` bằng key được gửi riêng:

```powershell
dotnet user-secrets set "Vision:Provider" "OpenRouter"
dotnet user-secrets set "OpenRouter:ApiKey" "OPENROUTER_EVALUATION_KEY"
dotnet user-secrets set "OpenRouter:Model" "qwen/qwen3-vl-8b-instruct"
dotnet user-secrets set "Vision:FallbackEnabled" "false"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\mssqllocaldb;Database=AuraDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Connect Timeout=5;ConnectRetryCount=0"
```

Không chạy `dotnet user-secrets list` khi chia sẻ màn hình vì lệnh có thể in API key.

## 5. Tạo hoặc nâng database

```powershell
dotnet ef database update
```

Kỳ vọng: lệnh hoàn tất không có migration error. Không xóa LocalDB instance hoặc file MDF nếu chỉ thấy warning registry; hãy dùng preflight và `/healthz` ở các bước sau để xác định database có phục vụ ứng dụng hay không.

## 6. Preflight trước khi chạy app

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Test-DemoReadiness.ps1" -StartLocalDb -SkipHttp -DiagnoseLocalDb -OutputPath ".\demo_evidence\00_preflight\preflight-before-app.txt"
```

Kỳ vọng:

- đúng database `AuraDb`;
- LocalDB `mssqllocaldb` đang chạy;
- `BUSINESS_RULES.md` có mặt;
- receipt storage ghi được;
- 0 failure;
- có thể có đúng một warning vì chủ động dùng `-SkipHttp`.

`-OutputPath` tự tạo thư mục và transcript. Không cần khai báo `$evidenceRoot` hoặc dùng `Tee-Object`.

## 7. Build và test offline

```powershell
dotnet build ..\AURA.sln -c Release --no-restore
dotnet test .\tests\AURA.Tests\AURA.Tests.csproj -c Release --no-restore
```

Candidate ngày 04/10/2026 phải đạt build 0 warning/0 error và 108/108 test pass. Automated tests không gọi OpenRouter và không tốn credit.

## 8. Chạy AURA

Trong PowerShell thứ nhất:

```powershell
dotnet run
```

Giữ cửa sổ này mở. Chờ dòng:

```text
Now listening on: http://localhost:5000
```

## 9. Full preflight khi app đang chạy

Mở PowerShell thứ hai, đi vào thư mục project rồi chạy:

```powershell
cd .\AURA\AURA
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Test-DemoReadiness.ps1" -DiagnoseLocalDb -OutputPath ".\demo_evidence\00_preflight\preflight-running-app.txt"
```

Nếu PowerShell thứ hai không được mở từ cùng thư mục cha, dùng đường dẫn tuyệt đối đến thư mục chứa `AURA.csproj` trước khi chạy script.

Kết quả bắt buộc cho live demo:

- `READY: 0 failures, 0 warning(s)`;
- health `status=ok`;
- `databaseAvailable=true`;
- `databaseUpToDate=true`;
- `pendingMigrationCount=0`;
- `storageAvailable=true`;
- provider `OpenRouter`;
- model `qwen/qwen3-vl-8b-instruct`;
- `fallbackEnabled=false`.

Có thể kiểm độc lập:

```powershell
Invoke-RestMethod "http://localhost:5000/healthz" | ConvertTo-Json -Depth 5
```

## 10. Chạy luồng sản phẩm

Mở `http://localhost:5000` trên Chrome hoặc Edge.

### Verify Harness

1. Chọn **Chạy Verify Harness** đúng một lần.
2. Chờ năm ca chạy tuần tự.
3. Kỳ vọng ba `AUTO_APPROVE`, một `ESCALATE_FACT`, một `ESCALATE_POLICY` và 5/5 PASS.
4. Không bấm lại nếu batch đang chạy. Nếu provider trả 429, dừng và kiểm quota thay vì spam retry.

### Một hóa đơn lẻ

1. Chọn JPG/PNG dưới 5 MB.
2. Nhập số tiền nhân viên khai bằng VND.
3. Chọn **AI tự động kiểm**.
4. Upload trả HTTP 202 rồi UI hiển thị pending/processing cho tới trạng thái cuối.
5. Kiểm tra ảnh, facts, quyết định và lý do.
6. Với `ESCALATE_*`, chọn **Chuyển tiếp**, mở tab **Quản lý**, chọn đồng ý hoặc từ chối rồi xem **Lịch sử hành vi**.

Ảnh mờ, thiếu dữ kiện hoặc model mâu thuẫn có thể được chuyển cho con người. Đây là fail-safe, không phải lỗi chỉ vì kết quả không phải `AUTO_APPROVE`.

## 11. Dừng ứng dụng an toàn

1. Chờ mọi request về trạng thái cuối; không tắt khi còn `PENDING` hoặc `PROCESSING`.
2. Lưu evidence cần thiết và đóng trình duyệt.
3. Tại PowerShell đang chạy app, nhấn `Ctrl+C` một lần.
4. Chờ thông báo shutdown rồi mới đóng terminal hoặc tắt máy.

Không cần xóa database, migration, user secrets hoặc dừng LocalDB thủ công.

## 12. Lỗi thường gặp

### Script bị execution policy chặn

Dùng đúng dạng:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Test-DemoReadiness.ps1"
```

Bypass chỉ áp dụng cho process con của lệnh này, không thay policy toàn máy.

### Dấu nhắc `>>`

PowerShell đang chờ dấu ngoặc hoặc chuỗi chưa đóng. Nhấn `Ctrl+C`, quay về prompt `PS ...>` rồi dán từng lệnh một dòng. Không dán phần `PS C:\...>` hoặc ký tự `>>` từ ảnh hướng dẫn.

### Port 5000 đã được dùng

Đóng AURA cũ bằng `Ctrl+C`. Có thể kiểm process đang listen:

```powershell
Get-NetTCPConnection -LocalPort 5000 -State Listen -ErrorAction SilentlyContinue
```

### LocalDB registry warning

Warning inspect registry không tự động có nghĩa database hỏng. Nếu full `/healthz` xác nhận `databaseAvailable=true`, `databaseUpToDate=true` và không có pending migration thì AURA đang truy cập database đúng. Không xóa registry key, LocalDB instance hoặc MDF để làm warning biến mất.

### OpenRouter 401 402 429 5xx hoặc timeout

- 401: kiểm key private.
- 402: kiểm credit.
- 429: dừng batch và chờ quota/rate limit.
- 5xx/timeout: giữ response và log; hệ thống phải fail-safe thay vì tạo PASS giả.

## 13. Ollama tùy chọn

Ollama không phải dependency của baseline BGK. Nếu muốn đánh giá local model, làm theo `docs/LOCAL_OLLAMA.md`, chạy phiên riêng và giữ fallback tắt để đo đúng một provider. Không so sánh batch OpenRouter có fallback với batch Ollama thuần.
