# Chạy AURA synthetic trên Windows

Cập nhật 26/10/2026. Nhánh mặc định `master` chứa source hiện hành; snapshot code trước hardening ở tag `archive/master-before-hardening-20261026`. Dùng một host local, DB/ảnh riêng và chỉ ảnh synthetic đã duyệt. Giữ nguyên DB đi thi, chưa publish candidate này hoặc mở pilot dữ liệu thật.

## 1. Chuẩn bị

UX hiện hành cập nhật fragment sau thao tác và giữ kỳ/bộ lọc/focus/cuộn. Release
377/377 và frontend 8/8 đã qua kiểm; Node chỉ cần khi chạy test JS. Khi thấy cảnh
báo dữ liệu cũ, chọn **Thử cập nhật lại** trước khi thao tác tiếp. Xem [runbook](RUNBOOK.md).

Cần Git, .NET 8 SDK, SQL Server LocalDB, PowerShell 5.1 hoặc 7 và OpenRouter key có credit/quyền gọi model được cấu hình. Không cần Ollama, bucket cloud hay tài khoản đăng nhập AURA.

```powershell
git --version
dotnet --info
sqllocaldb info MSSQLLocalDB
```

Nếu instance đang `Stopped`, chạy `sqllocaldb start MSSQLLocalDB` bằng đúng Windows account sở hữu instance. Không xóa instance, registry hoặc MDF để sửa một cảnh báo chưa được chẩn đoán.

## 2. Clone đúng candidate và lưu key

```powershell
git clone https://github.com/BondPhuPhamzZ/AURA.git
cd .\AURA\AURA
dotnet tool restore
dotnet restore
dotnet user-secrets set "OpenRouter:ApiKey" "<YOUR_OPENROUTER_API_KEY>"
```

Nếu đã clone, kiểm `git status` và giữ thay đổi hiện có; không clone đè hoặc reset. Không gửi key qua chat/ảnh và không chạy `dotnet user-secrets list` khi chia sẻ màn hình. API key lưu ở User Secrets, không ở repository.

## 3. Build, test rồi chạy sandbox

```powershell
dotnet build AURA.csproj -c Release --no-restore
$env:AURA_RUN_LOCALDB_INTEGRATION = '1'
$env:AURA_RUN_POWERSHELL_INTEGRATION = '1'
dotnet test tests/AURA.Tests/AURA.Tests.csproj -c Release --no-restore
Remove-Item Env:\AURA_RUN_LOCALDB_INTEGRATION
Remove-Item Env:\AURA_RUN_POWERSHELL_INTEGRATION
$env:Vision__Provider = 'OpenRouter'
$env:OpenRouter__Model = 'qwen/qwen3.8-27b'
$env:OpenRouter__RequireZeroDataRetention = 'true'
.\tools\Start-AuraRetentionSandbox.ps1
```

Các test PowerShell integration cần máy có **cả PS5.1 và PS7**. Bộ xác minh ghi nhận 371/371 mỗi cấu hình Debug/Release với cả hai integration flags bật; chạy không bật flag không chứng minh đã chạy integration. Automated tests dùng provider giả lập, không tốn credit. Upload/evaluator model thật có tính phí.

Launcher build và áp migration vào `AURA_RetentionSandbox_yyyyMMdd`, dùng ảnh riêng dưới `.tmp/retention-sandbox/`, bật retention 7 ngày cho hồ sơ đã chốt và `ApprovedImagesOnly`. Nó không sửa User Secrets hoặc migrate DB `AuraDb`. Không chạy `dotnet ef database update` trên DB cũ để làm theo hướng dẫn này. Biến model ở trên chỉ thuộc cửa sổ PowerShell; đóng cửa sổ sau khi dừng app để bỏ override.

Giữ terminal mở, truy cập `http://127.0.0.1:5098`. Trong PowerShell thứ hai:

```powershell
Invoke-RestMethod 'http://127.0.0.1:5098/healthz' | ConvertTo-Json -Depth 5
```

Cần `status=ok`, `pendingMigrationCount=0`, `externalImagePrivacyMode=ApprovedImagesOnly`, `externallyApprovedImageCount=49`, `retentionCleanupEnabled=true`, `strictZeroDataRetention=true`, `providerDataCollectionDenied=true`, concurrency 1 và fallback/Drive/Verify tắt. Health kiểm cấu hình/DB/storage, không tự gọi model hoặc xác nhận key còn credit.

## 4. Thử một ảnh và xem luồng

1. Upload `test_kit/v3_1/images/R3-01-vat-invoice-number.jpg` bằng giao diện. Không nhập tiền; AURA lấy tổng tiền từ facts đã kiểm chứng. HTTP 202 chỉ xác nhận đã tiếp nhận bền vững.
2. Chờ job hoàn tất; kỳ vọng `AUTO_APPROVE` cho fixture này, xem facts/lý do và audit. Kết quả model có thể biến thiên; ghi đúng kết quả thay vì sửa nhãn.
3. Với fixture cần người kiểm tra, chuyển tiếp sang Quản lý, chọn hành động rồi xem Lịch sử. Tiếp nhận `ESCALATE_FACT`/`ESCALATE_PRIVACY` là xử lý thủ công, không phải tự động duyệt chi tiền.
4. Chọn kỳ trên dashboard và tải CSV cùng kỳ. Dấu privacy vẫn được giữ sau khi tiếp nhận thủ công.

Chỉ 49 nội dung synthetic đã xác minh được phép gửi model; các bản sao byte trong bộ 100 cũng khớp hash. Ảnh khác bị chặn trước HTTP thành `ESCALATE_PRIVACY`, không facts; không tự thêm hash để vượt gate. Đây không phải detector/che PII. Ảnh đã duyệt vẫn gửi nguyên pixels; Strict ZDR là yêu cầu định tuyến, chưa phải cam kết pháp lý.

Không bật Verify trong luồng này. Muốn thử Drive, đọc [runbook](RUNBOOK.md), giữ folder synthetic Restricted và bật rõ `-EnableSyntheticDrive`; file trong folder con không được quét tự động. Muốn chạy đủ 15 ca, xem [model evidence](MODEL_ROUTE_COMPARISON_2026-10-24.md), khóa đúng manifest/images và lưu raw output ngoài Git. Không diễn giải bộ known-data nhỏ là accuracy thực tế hay throughput 100/1.000 hóa đơn.

## 5. Dừng và xử lý lỗi

Chờ queue rỗng, lưu evidence riêng tư rồi nhấn Ctrl+C một lần. Không xóa DB/ảnh hoặc đổi User Secrets để dừng app. Retention dọn hồ sơ đã chốt theo deadline; pending/unresolved giữ để xử lý tiếp. Cleanup chỉ chạy khi app/task và máy hoạt động, không bảo đảm xóa đúng hạn khi máy tắt.

- 401/402: kiểm key/credit trong dashboard riêng; không in key vào log.
- 429/timeout: giữ evidence và chờ backoff/cooldown; không spam upload.
- Startup báo approval file lỗi: kiểm fixture/manifest bằng Git, không tắt gate để chạy tiếp.
- Port 5098 đã dùng: kiểm terminal AURA đang chạy và dùng phiên đó; không kill process không rõ nguồn.
- LocalDB không kết nối được: xem [runbook](RUNBOOK.md), giữ DB cũ/backup; không reset dữ liệu.

Privacy cho dữ liệu thật, auth/RBAC, malware quarantine, hosting restore và nhiều host vẫn là gate tương lai. Xem [checklist hiện hành](CURRENT_ACTION_CHECKLIST.md).
