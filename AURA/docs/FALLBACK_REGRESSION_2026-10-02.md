# OpenRouter → Ollama fallback regression — 02/10/2026

## Kết luận

Cơ chế fallback thật đã chạy thành công trên laptop demo bằng một database LocalDB cô lập và một ảnh tổng hợp. Primary được chủ động đặt ở trạng thái chưa cấu hình nên không có request OpenRouter nào được gửi. AURA ghi đúng lỗi primary, gọi Ollama đúng một lần, lưu provider thực sự phục vụ và hoàn tất hồ sơ.

Kết quả này chứng minh đường chuyển provider hoạt động. Nó **không** đủ để bật Ollama fallback mặc định trong buổi chấm: đây chỉ là một ca, latency sát giới hạn 90 giây và judge set lịch sử của Ollama 4B vẫn có một missed escalation.

## Thiết kế an toàn

- Source: `master` tại `b1f6445`; logic chạy không đổi so với baseline kỹ thuật `80522f8`.
- Database riêng: `AuraFallbackEvidence_20261002_01`; không đọc hoặc ghi `AuraDb`.
- Port riêng: `http://localhost:5001`.
- Storage riêng: `App_Data/fallback-evidence-20261002`.
- Ảnh: fixture tổng hợp `wwwroot/test_data/images/HoaDon1.jpg`.
- Primary: `OpenRouter`; `OpenRouter__ApiKey` được override bằng whitespace chỉ trong process test để tạo `AI_NOT_CONFIGURED`, vì vậy không gọi mạng và không tốn credit.
- Fallback: `Ollama`, model `qwen3-vl:4b-instruct`, loopback `127.0.0.1:11434`.
- Không thay user-secrets và không thay `appsettings*.json`.

Health trả `degraded` trong kịch bản này là đúng: primary cố ý chưa cấu hình. Đồng thời health xác nhận `databaseAvailable=true`, `databaseUpToDate=true`, `pendingMigrationCount=0`, `storageAvailable=true`, `fallbackEnabled=true` và `fallbackProvider=Ollama`.

## Kết quả

| Trường | Giá trị |
|---|---|
| Case ID | `588dcbf05a644c63b89eb7a03a8e48ca` |
| HTTP accepted | 351 ms |
| Runner end-to-end | 84.425 ms |
| Worker processing | 83.649 ms |
| Processing state | `COMPLETED` |
| Decision | `AUTO_APPROVE` |
| Primary provider | `OpenRouter` |
| Served provider | `Ollama` |
| Fallback used | `true` |
| Primary error | `AI_NOT_CONFIGURED` |
| Semantic repair | `false` |
| Validation issues | 0 |

Facts cuối đọc đúng tổng `295.199 VND`, hai dòng hàng và mã vận chuyển SPX. Audit metadata đủ để giải thích primary lỗi gì và provider nào thực sự phục vụ.

## Cách tái lập

Chỉ chạy quy trình này trong phiên test riêng. Không dùng `AuraDb` và không bật fallback khi benchmark từng provider.

1. Kiểm Ollama và model:

```powershell
ollama list
Invoke-RestMethod "http://127.0.0.1:11434/api/tags" | ConvertTo-Json -Depth 5
```

2. Mở PowerShell mới tại thư mục chứa `AURA.csproj`, khai báo database test trong đúng cửa sổ đó:

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=(localdb)\mssqllocaldb;Database=AuraFallbackEvidence_20261002_01;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Connect Timeout=5;ConnectRetryCount=0"
dotnet ef database update --no-build
```

3. Trong cùng cửa sổ, đặt override chỉ tồn tại trong process và chạy port 5001:

```powershell
$env:ASPNETCORE_URLS = "http://localhost:5001"
$env:Vision__Provider = "OpenRouter"
$env:Vision__FallbackEnabled = "true"
$env:Vision__FallbackProvider = "Ollama"
$env:OpenRouter__ApiKey = " "
$env:ReceiptStorage__Directory = "App_Data/fallback-evidence-20261002"
dotnet run --no-build --no-launch-profile
```

4. Ở PowerShell thứ hai, chạy đúng một ảnh tổng hợp:

```powershell
cd D:\aura\AURA\AURA
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Invoke-ConcurrentUploadSmoke.ps1" -BaseUrl "http://localhost:5001" -Copies 1 -ImagePath ".\wwwroot\test_data\images\HoaDon1.jpg" -ClaimedAmount 295199
```

5. Pass khi final record có đủ:

- `PrimaryProvider=OpenRouter`;
- `ServedProvider=Ollama`;
- `FallbackUsed=true`;
- `ProviderErrorCode=AI_NOT_CONFIGURED`;
- trạng thái cuối không còn `PENDING/PROCESSING`;
- facts cuối và decision phù hợp ground truth.

6. Dừng bằng `Ctrl+C`, rồi giải phóng model nếu cần:

```powershell
ollama stop qwen3-vl:4b-instruct
```

Đóng cửa sổ PowerShell test để xóa toàn bộ environment override. Lần chạy demo chính vẫn dùng `AuraDb`, port 5000 và `Vision:FallbackEnabled=false`.

## Ranh giới claim

- Test này chứng minh switching, audit metadata và Ollama invocation thật.
- Test này không mô phỏng outage OpenRouter thật, không chứng minh primary recovery sau cooldown và không phải load test.
- Circuit-open/cooldown/primary-probe được bảo vệ bằng automated tests; không đáng chạy ba hoặc bốn lượt Ollama live sát ngày demo chỉ để mở circuit.
- Judge set lịch sử vẫn là cơ sở so sánh provider: OpenRouter 15/15 decision, Ollama 14/15 và missed escalation TK-12. Vì vậy baseline demo tiếp tục để fallback tắt; Ollama là đường offline/manual có giới hạn.
