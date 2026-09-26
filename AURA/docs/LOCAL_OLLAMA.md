# Chạy Qwen3 VL 4B local bằng Ollama

Cập nhật: 27/09/2026. Tùy chọn này bổ sung một provider local để benchmark và demo ngoại tuyến. `Vision:Provider` vẫn mặc định là `OpenRouter`, nên cài Ollama không làm thay đổi bản Sprint 1 hiện tại. Không có cơ chế tự động gọi cả hai provider hoặc âm thầm thay kết quả.

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

`/healthz` xác nhận cấu hình và policy, không chứng minh model đã nạp. Request ảnh đầu tiên mới là phép kiểm kết nối đầy đủ. Sau mỗi lần trích xuất, backend kiểm tra chéo ngữ nghĩa như định dạng tiền VND, loại chứng từ, mã đơn/mã vận chuyển và tổng dòng hàng. Nếu JSON hợp lệ nhưng dữ kiện mâu thuẫn, AURA cho model đọc lại đúng một lần với chỉ dẫn sửa có mục tiêu; kết quả vẫn mâu thuẫn sẽ chuyển `ESCALATE_FACT` thay vì tự sửa số tiền.

## 6. Smoke test an toàn

1. Không chạy Verify ngay. Upload đúng một fixture nhỏ trong `wwwroot/test_data/images`.
2. Kiểm tra facts JSON, quyết định policy và Audit Log.
3. Kiểm `ollama ps`, Task Manager RAM/GPU và thời gian xử lý.
4. Nếu smoke pass, chạy Verify Harness theo kế hoạch cố định. Không bấm lại chỉ để săn PASS; mỗi batch phải được ghi nhận kể cả khi fail.
5. Với benchmark trước khi đổi provider, chạy ba batch Verify liên tiếp trên cùng build/cấu hình và dùng database riêng để không trộn dữ liệu demo. `Ollama:KeepAlive=30m` phù hợp trong benchmark/demo; sau đó có thể trả về `5m` để giải phóng RAM sớm hơn.
6. Ghi actual status, semantic repair, latency từng ca, tổng thời gian batch, RAM/GPU và lỗi nếu có. Ollama local có thể mất khoảng năm phút cho 5 ca trên RTX 3050 Laptop 4 GB, không dùng mốc 90 giây của hosted để kết luận sai về chất lượng.
7. Chỉ dùng Ollama cho demo chính khi đạt cổng benchmark trong `docs/MEASUREMENT_PLAN.md`.

Kết quả kiểm soát ngày 27/09/2026 trên build semantic-hardening: năm batch liên tiếp đều đạt 5/5, tức 25/25 quyết định đúng trên 5 fixture tổng hợp; upload thủ công `HoaDon1.jpg` đạt `AUTO_APPROVE` 3/3. Ba batch có đo chi tiết mất khoảng 303-304 giây mỗi batch; TC-02 cần một lượt repair nên khoảng 100 giây và các ca còn lại khoảng 45-55 giây. Hai batch xác nhận bổ sung chưa tổng hợp latency. Đây là bằng chứng pipeline có kiểm soát, chưa phải accuracy trên hóa đơn thật độc lập.

## 7. Dừng phiên test và tắt máy an toàn

1. Chờ request upload hoặc Verify hiện tại kết thúc; không ép tắt `AURA.exe` khi model còn xử lý.
2. Tại terminal đang chạy AURA, nhấn `Ctrl+C` một lần và đợi trở lại dấu nhắc PowerShell.
3. Dỡ model khỏi RAM/VRAM nếu muốn giải phóng ngay:

```powershell
ollama stop qwen3-vl:4b-instruct
ollama ps
```

`ollama ps` không còn model là trạng thái mong đợi. Có thể chọn **Quit Ollama** ở biểu tượng khay hệ thống, hoặc tắt Windows bình thường. Không cần dừng SQL LocalDB, xóa database, reset migration, xóa model hay đặt lại user-secrets. `Ollama:KeepAlive=30m` chỉ giữ model trong bộ nhớ khi máy đang chạy; thiết lập vẫn được lưu cho phiên sau nhưng RAM/VRAM luôn được giải phóng khi tắt máy.

Ngày test tiếp theo, mở Ollama rồi kiểm tra API trước khi chạy AURA:

```powershell
Invoke-RestMethod http://127.0.0.1:11434/api/tags
dotnet run
```

Nếu model chưa được nạp, request ảnh đầu tiên là cold run và sẽ chậm hơn; đây là hành vi bình thường. Không cần chạy lệnh warm-up khi đang đo cold latency.

## 8. Quay lại OpenRouter

Không cần xóa Ollama hoặc tải lại database:

```powershell
dotnet user-secrets set "Vision:Provider" "OpenRouter"
dotnet user-secrets set "OpenRouter:ApiKey" "YOUR_PRIVATE_KEY"
dotnet user-secrets set "OpenRouter:Model" "qwen/qwen3-vl-8b-instruct"
dotnet run
```

Sau đó mở `/healthz` và xác nhận `provider` là `OpenRouter`. AURA không tự chuyển provider khi đang xử lý một hồ sơ; mỗi lần chạy dùng đúng provider đã cấu hình để audit và benchmark không bị lẫn.

## 9. Lỗi thường gặp

| Mã/hiện tượng | Nguyên nhân thường gặp | Cách xử lý |
|---|---|---|
| `AI_LOCAL_UNAVAILABLE` | Ollama chưa chạy hoặc port 11434 không phản hồi | Mở Ollama, kiểm `/api/tags`, không chạy hai server |
| `AI_MODEL_UNAVAILABLE` | Chưa pull đúng tag | Chạy `ollama pull qwen3-vl:4b-instruct` |
| `AI_TIMEOUT` | CPU chậm, thiếu RAM hoặc ảnh/context lớn | Đóng ứng dụng nặng, giảm context 8192 xuống 4096, thử một ảnh |
| `AI_SCHEMA_MISMATCH` | Model 4B không giữ đúng JSON Schema | Giữ fail-safe, ghi ca lỗi; không sửa expected result |
| JSON hợp lệ nhưng sai loại chứng từ/định dạng VND | Lỗi ngữ nghĩa của model | Backend repair đúng một lần; nếu vẫn sai thì `ESCALATE_FACT`, không nhân tiền hoặc gán mã bằng suy đoán |
| Máy swap/đơ | Nhiều model/request hoặc context quá lớn | Một model, một request, context 4096; `ollama stop qwen3-vl:4b-instruct` để giải phóng RAM |

## 10. Nguyên tắc đánh giá

Không so sánh 4B local và 8B hosted bằng cảm giác. Chạy cùng ảnh, cùng policy và cùng schema; ghi model/tag, context, lượng tử hóa, latency, schema success, semantic-repair rate, exact match các field quan trọng, missed escalation và over-escalation. Ollama chỉ trở thành provider mặc định sau khi đạt cổng chất lượng trên tập độc lập và có kế hoạch quay lại OpenRouter.

Nguồn chính thức: `https://docs.ollama.com/windows`, `https://docs.ollama.com/api/chat`, `https://docs.ollama.com/capabilities/vision`, `https://docs.ollama.com/capabilities/structured-outputs`, `https://docs.ollama.com/faq`, `https://ollama.com/library/qwen3-vl/tags`.
