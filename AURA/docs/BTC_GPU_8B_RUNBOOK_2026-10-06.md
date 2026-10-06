# AURA — runbook GPU BTC (Qwen3-VL 8B)

Cập nhật: 06/10/2026. Mục tiêu là chạy AURA và Ollama trên **cùng máy BTC** trong một phiên Google Meet có giám sát. Đây là benchmark/POC một giờ, không phải GPU server công khai hoặc dịch vụ hosting 24/7.

## 1. Phạm vi an toàn

- Chỉ dùng ảnh tổng hợp trong `test_kit/images` hoặc `wwwroot/test_data/images`; không dùng lại blind holdout hoặc receipt có PII.
- Không gửi API key, connection string hay secret qua GitHub, Discord, log hoặc màn hình Meet.
- Không mở cổng Ollama `11434` ra Internet và không đặt `OLLAMA_HOST=0.0.0.0`.
- Dùng database, storage và port test riêng; không dùng `AuraDb` hoặc dữ liệu live.
- Đo Ollama-only trước. Chỉ kiểm fallback sau khi model pass smoke; không trộn provider trong benchmark.

## 2. Chuẩn bị trước buổi Meet

BTC cần có Git, .NET 8 SDK, SQL Server LocalDB, NVIDIA driver và Ollama `>= 0.12.7`. Pull model trước buổi Meet:

```powershell
ollama --version
ollama pull qwen3-vl:8b-instruct-q4_K_M
ollama list
```

Giữ Ollama ở loopback. Trên Windows, đặt ba biến user environment rồi **Quit Ollama** ở system tray và mở lại:

```text
OLLAMA_NO_CLOUD=1
OLLAMA_NUM_PARALLEL=1
OLLAMA_MAX_LOADED_MODELS=1
```

Không bật parallel trong phiên đầu. Model khuyến nghị là tag chính xác `qwen3-vl:8b-instruct-q4_K_M`; không dùng `latest`, 30B/32B hoặc BF16.

## 3. Pull và xác minh code

```powershell
git clone https://github.com/BondPhuPhamzZ/AURA.git
cd AURA\AURA
git checkout master
git pull --ff-only origin master
git status --short --branch
git rev-parse HEAD
dotnet --info
dotnet restore
dotnet build AURA.csproj -c Release --no-restore
dotnet test .\tests\AURA.Tests\AURA.Tests.csproj -c Release --no-build --no-restore
dotnet ef migrations has-pending-model-changes --configuration Release --no-build
```

PASS khi Git sạch, commit trùng SHA team gửi trong tin đặt lịch, build 0 error, toàn bộ test pass và EF báo không có model change chưa migration.

## 4. Kiểm GPU và Ollama

```powershell
nvidia-smi
Invoke-RestMethod http://127.0.0.1:11434/api/tags | ConvertTo-Json -Depth 5
```

Warm-up không dùng ảnh:

```powershell
$warmup = @{
  model = 'qwen3-vl:8b-instruct-q4_K_M'
  messages = @()
  stream = $false
  keep_alive = '30m'
} | ConvertTo-Json -Depth 5

Invoke-RestMethod -Method Post -Uri http://127.0.0.1:11434/api/chat `
  -ContentType 'application/json' -Body $warmup
ollama ps
```

Kỳ vọng `ollama ps` hiển thị `100% GPU`. Nếu bị CPU/GPU split, không tăng concurrency; ghi nhận tỷ lệ thực tế vào evidence.

## 5. Tạo database test

Mở PowerShell tại thư mục chứa `AURA.csproj`:

```powershell
$env:ConnectionStrings__DefaultConnection = 'Server=(localdb)\mssqllocaldb;Database=AuraGpuEvidence_20261007_01;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Connect Timeout=5;ConnectRetryCount=0'
$env:ReceiptStorage__Directory = 'App_Data/gpu-evidence-20261007'
$env:Database__ApplyMigrationsOnStartup = 'false'
dotnet ef database update --configuration Release --no-build
```

## 6. Chạy Ollama-only

Trong **cùng cửa sổ PowerShell**:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Production'
$env:ASPNETCORE_URLS = 'http://127.0.0.1:5001'
$env:Vision__Provider = 'Ollama'
$env:Vision__FallbackEnabled = 'false'
$env:Ollama__BaseUrl = 'http://127.0.0.1:11434/'
$env:Ollama__Model = 'qwen3-vl:8b-instruct-q4_K_M'
$env:Ollama__ContextTokens = '16384'
$env:Ollama__MaxOutputTokens = '4096'
$env:Ollama__TimeoutSeconds = '180'
$env:Ollama__KeepAlive = '30m'
$env:ReceiptProcessing__PollIntervalMs = '500'
dotnet run -c Release --no-build --no-launch-profile
```

Dùng context `16384` và output cap `4096` vì evidence contract v2 có thể cần một lượt semantic repair dài hơn 8192 token/2048 output token. Nếu thiếu VRAM hoặc model không còn `100% GPU`, giảm context về `12288`, ghi rõ thay đổi và không trộn hai cấu hình trong cùng batch; không giảm output cap giữa batch.

Ở PowerShell thứ hai:

```powershell
$health = Invoke-RestMethod http://127.0.0.1:5001/healthz
$health | ConvertTo-Json -Depth 10
ollama ps
nvidia-smi
```

Health phải `ok`, DB/storage `true`, migration `0`, provider `Ollama`, model đúng tag và fallback `false`. Health chỉ xác nhận cấu hình; phải gửi một fixture mới chứng minh inference thật.

Smoke một ảnh trước:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File `
  '.\tools\Invoke-ConcurrentUploadSmoke.ps1' `
  -BaseUrl 'http://127.0.0.1:5001' -Copies 1 `
  -ImagePath '.\wwwroot\test_data\images\HoaDon1.jpg' -ClaimedAmount 295199
```

Nếu smoke pass, chạy judge set tuần tự:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File `
  '.\tools\Invoke-ExtendedDatasetEvaluation.ps1' `
  -BaseUrl 'http://127.0.0.1:5001' `
  -ManifestPath '.\test_kit\judge-manifest.json' `
  -ImagesDirectory '.\test_kit\images' `
  -MaxCases 15 -InterCaseDelaySeconds 4 -CaseTimeoutMinutes 10 `
  -OutputDirectory '.\gpu-evidence\judge-15-8b'
```

Không bấm/rerun để săn PASS. Giữ `results.csv`, `results.json`, `summary.json`, `metadata.json` kể cả khi fail.

## 7. Kiểm fallback thật trong môi trường cô lập

Dừng app bằng `Ctrl+C`. Tạo database/storage khác hoặc giữ dữ liệu tách rõ, dùng port 5002:

```powershell
$env:ASPNETCORE_URLS = 'http://127.0.0.1:5002'
$env:Vision__Provider = 'OpenRouter'
$env:Vision__FallbackEnabled = 'true'
$env:Vision__FallbackProvider = 'Ollama'
$env:OpenRouter__ApiKey = ' '
dotnet run -c Release --no-build --no-launch-profile
```

Gửi đúng một fixture tổng hợp. PASS khi bản ghi cuối có:

```text
PrimaryProvider=OpenRouter
ServedProvider=Ollama
FallbackUsed=true
ProviderErrorCode=AI_NOT_CONFIGURED
ProcessingState=COMPLETED
```

Health `degraded` trong phép thử này là dự kiến vì primary cố ý thiếu key. Không dùng test này để tuyên bố OpenRouter outage recovery hoặc production readiness.

## 8. Evidence bắt buộc

Lưu theo các thư mục `00_environment`, `01_health`, `02_smoke`, `03_judge15`, `04_fallback`, `05_conclusion`:

- commit SHA và `git status`;
- `dotnet --info`, build/test/EF output;
- `nvidia-smi`, `ollama --version`, `ollama list`, `ollama ps`;
- health JSON;
- runner `metadata.json`, `summary.json`, `results.csv/json`;
- latency P50/P95, VRAM peak, provider/fallback metadata;
- kết luận PASS/FAIL và mọi thay đổi context/model.

Không lưu secret, Authorization header, full connection string hoặc receipt thật.

## 9. Kết thúc và hoàn nguyên

```powershell
# Trong terminal AURA: Ctrl+C
ollama stop qwen3-vl:8b-instruct-q4_K_M
ollama ps
```

Đóng các cửa sổ PowerShell test để xóa process environment override. Không sửa `appsettings.json`, user-secrets của BTC hoặc 22 biến SmartASP. Không bật fallback trên SmartASP vì loopback của hosting không trỏ về máy BTC.

## 10. Gate sử dụng fallback khi demo

Chỉ cân nhắc bật khi cùng một cấu hình đạt: judge 15 không missed escalation, 0 system error, không schema/truncation error, một fallback record có metadata đúng, latency nằm dưới timeout với biên an toàn và model chạy chủ yếu/toàn bộ trên GPU. Nếu chưa đạt, OpenRouter vẫn là primary; Ollama chỉ là đường offline/manual có giám sát.
