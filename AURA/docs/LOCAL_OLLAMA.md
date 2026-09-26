# Chạy Qwen3 VL 4B local bằng Ollama

Cập nhật: 26/09/2026. Tùy chọn này bổ sung một provider local để benchmark và demo ngoại tuyến. `Vision:Provider` vẫn mặc định là `OpenRouter`, nên cài Ollama không làm thay đổi bản Sprint 1 hiện tại. Không có cơ chế tự động gọi cả hai provider hoặc âm thầm thay kết quả.

## 1. Phạm vi và cấu hình khuyến nghị

- Windows 10 22H2 trở lên, .NET 8 và SQL Server LocalDB như luồng local hiện tại.
- RAM 16 GB: bắt đầu với `qwen3-vl:4b-instruct` Q4_K_M, một request tại một thời điểm.
- Ổ đĩa trống: dành ít nhất 8 GB cho model, cache và log. Gói model Q4 hiện khoảng 3,3 GB nhưng RAM runtime lớn hơn dung lượng file.
- `ContextTokens=8192`, `MaxOutputTokens=2048`, `KeepAlive=5m`. Nếu máy swap hoặc thiếu RAM, giảm context còn 4096 trước khi đổi model nhỏ hơn.
- Không mở port 11434 ra Internet. AURA chỉ chấp nhận HTTP Ollama trên loopback hoặc HTTPS khi cấu hình endpoint khác.

Ollama là lựa chọn local cho một người dùng và demo. vLLM chỉ nên cân nhắc khi có NVIDIA GPU server và cần throughput/concurrency cao.

## 2. Cài Ollama trên Windows

1. Tải `OllamaSetup.exe` từ `https://ollama.com/download/windows`.
2. Chạy installer. Ollama cài theo user, chạy nền và cung cấp API tại `http://127.0.0.1:11434`.
3. Đóng terminal cũ, mở PowerShell mới rồi kiểm tra:

```powershell
ollama --version
Invoke-RestMethod http://127.0.0.1:11434/api/tags
```

Nếu lệnh thứ hai không kết nối được, mở Ollama từ Start Menu. Chỉ chạy `ollama serve` thủ công khi ứng dụng nền chưa chạy; không chạy hai server trên cùng port.

## 3. Giới hạn tài nguyên cho máy 16 GB

Ollama trên Windows đọc environment variable khi ứng dụng khởi động. Trong **Edit environment variables for your account**, tạo:

```text
OLLAMA_NUM_PARALLEL=1
OLLAMA_MAX_LOADED_MODELS=1
OLLAMA_NO_CLOUD=1
```

Sau khi lưu, Quit Ollama ở system tray rồi mở lại từ Start Menu. `OLLAMA_NO_CLOUD=1` là tùy chọn riêng tư, tắt tính năng cloud của Ollama; AURA chỉ dùng model local dù không bật biến này. Không đặt `OLLAMA_HOST=0.0.0.0`.

Nếu RAM vẫn cao, có thể thêm `OLLAMA_KV_CACHE_TYPE=q8_0`. Đây là tối ưu bộ nhớ có thể tạo sai khác nhỏ, nên phải benchmark lại trước khi dùng cho demo chính thức.

## 4. Tải và kiểm tra model

```powershell
ollama pull qwen3-vl:4b-instruct
ollama list
ollama run qwen3-vl:4b-instruct
```

Trong phiên `ollama run`, nhập một câu ngắn để xác nhận model nạp được, sau đó gõ `/bye`. Dùng lệnh sau để xem model chạy bằng CPU, GPU hay kết hợp:

```powershell
ollama ps
```

Trước lúc demo có thể warm-up mà không cần gửi hóa đơn:

```powershell
Invoke-RestMethod -Method Post `
  -Uri http://127.0.0.1:11434/api/chat `
  -ContentType "application/json" `
  -Body '{"model":"qwen3-vl:4b-instruct","messages":[],"stream":false,"keep_alive":"5m"}'
```

## 5. Chuyển AURA sang Ollama

Tại `D:\aura\AURA\AURA`:

```powershell
dotnet user-secrets set "Vision:Provider" "Ollama"
dotnet user-secrets set "Ollama:BaseUrl" "http://127.0.0.1:11434/"
dotnet user-secrets set "Ollama:Model" "qwen3-vl:4b-instruct"
dotnet user-secrets set "Ollama:ContextTokens" "8192"
dotnet user-secrets set "Ollama:MaxOutputTokens" "2048"
dotnet user-secrets set "Ollama:TimeoutSeconds" "180"
dotnet user-secrets set "Ollama:KeepAlive" "5m"

dotnet build --no-restore
dotnet test tests\AURA.Tests\AURA.Tests.csproj --no-restore
dotnet run
```

Mở `/healthz`. Kỳ vọng:

```json
{
  "status": "ok",
  "provider": "Ollama",
  "model": "qwen3-vl:4b-instruct",
  "policyAvailable": true
}
```

`/healthz` xác nhận cấu hình và policy, không chứng minh model đã nạp. Request ảnh đầu tiên mới là phép kiểm kết nối đầy đủ.

## 6. Smoke test an toàn

1. Không chạy Verify ngay. Upload đúng một fixture nhỏ trong `wwwroot/test_data/images`.
2. Kiểm tra facts JSON, quyết định policy và Audit Log.
3. Kiểm `ollama ps`, Task Manager RAM/GPU và thời gian xử lý.
4. Nếu smoke pass, chạy Verify Harness đúng một lượt. Ollama có thể chậm hơn giới hạn 90 giây của bản hosted; ghi kết quả thật, không chạy lặp để săn PASS.
5. Chạy lại cùng một ảnh ba lần để phát hiện output không ổn định.
6. Chỉ dùng Ollama cho demo chính khi đạt cổng benchmark trong `docs/MEASUREMENT_PLAN.md`.

## 7. Quay lại OpenRouter

Không cần xóa Ollama hoặc tải lại database:

```powershell
dotnet user-secrets set "Vision:Provider" "OpenRouter"
dotnet user-secrets set "OpenRouter:ApiKey" "YOUR_PRIVATE_KEY"
dotnet user-secrets set "OpenRouter:Model" "qwen/qwen3-vl-8b-instruct"
dotnet run
```

Sau đó mở `/healthz` và xác nhận `provider` là `OpenRouter`. AURA không tự chuyển provider khi đang xử lý một hồ sơ; mỗi lần chạy dùng đúng provider đã cấu hình để audit và benchmark không bị lẫn.

## 8. Lỗi thường gặp

| Mã/hiện tượng | Nguyên nhân thường gặp | Cách xử lý |
|---|---|---|
| `AI_LOCAL_UNAVAILABLE` | Ollama chưa chạy hoặc port 11434 không phản hồi | Mở Ollama, kiểm `/api/tags`, không chạy hai server |
| `AI_MODEL_UNAVAILABLE` | Chưa pull đúng tag | Chạy `ollama pull qwen3-vl:4b-instruct` |
| `AI_TIMEOUT` | CPU chậm, thiếu RAM hoặc ảnh/context lớn | Đóng ứng dụng nặng, giảm context 8192 xuống 4096, thử một ảnh |
| `AI_SCHEMA_MISMATCH` | Model 4B không giữ đúng JSON Schema | Giữ fail-safe, ghi ca lỗi; không sửa expected result |
| Máy swap/đơ | Nhiều model/request hoặc context quá lớn | Một model, một request, context 4096; `ollama stop qwen3-vl:4b-instruct` để giải phóng RAM |

## 9. Nguyên tắc đánh giá

Không so sánh 4B local và 8B hosted bằng cảm giác. Chạy cùng ảnh, cùng policy và cùng schema; ghi model/tag, context, lượng tử hóa, latency, schema success, exact match các field quan trọng, missed escalation và over-escalation. Ollama chỉ trở thành provider mặc định sau khi đạt cổng chất lượng và có kế hoạch quay lại OpenRouter.

Nguồn chính thức: `https://docs.ollama.com/windows`, `https://docs.ollama.com/api/chat`, `https://docs.ollama.com/capabilities/vision`, `https://docs.ollama.com/capabilities/structured-outputs`, `https://docs.ollama.com/faq`, `https://ollama.com/library/qwen3-vl/tags`.
