# Competition failover and HTTP 429 strategy — 12/10/2026

## Quyết định cuối

Primary release candidate hiện là OpenRouter `qwen/qwen3.8-flash` trên Alibaba với `ReasoningEffort=none`. Lượt 15 receipt post-holdout có 20/20 upstream HTTP 200, không 429; năm upload đồng thời hoàn tất trong P95 41,079 giây ở concurrency AI 1. Kết quả này thay quyết định vận hành cho demo nhưng **không xóa** incident Parasail/8B bên dưới và không đảm bảo upstream sẽ không bao giờ lỗi. Xem `QWEN38_FLASH_BENCHMARK_2026-10-12.md`.

Ollama `qwen3-vl:4b-instruct` digest `ee4b975b58c1` **đủ điều kiện làm fallback có giám sát trên laptop chạy đồng thời AURA và Ollama**. Cơ sở là Test Kit v2 synthetic ở profile 16K đạt 15/15 decision, 0 missed escalation, 0 over-escalation, 0 system error và 69/75 field. Đây không phải chứng nhận production accuracy hoặc multi-user capacity.

Không bật Ollama fallback trên SmarterASP khi `Ollama:BaseUrl=http://127.0.0.1:11434/`. Loopback đó trỏ tới server hosting, không trỏ về laptop. Chỉ bật remote fallback khi có endpoint riêng qua private network hoặc reverse proxy TLS, xác thực, rate limit và log đã che dữ liệu; adapter hiện chưa hỗ trợ bearer header riêng cho Ollama.

## Hai profile không được trộn

### Public URL trên SmarterASP

```text
Vision__Provider=OpenRouter
Vision__FallbackEnabled=false
Vision__FallbackProvider=Ollama
Vision__MaxConcurrentRequests=1
Vision__ProviderCooldownSeconds=120
OpenRouter__Model=qwen/qwen3.8-flash
OpenRouter__ReasoningEffort=none
Verify__Enabled=false              # bật tạm trong phiên Verify có giám sát
```

- Đặt IIS `Maximum Worker Processes = 1`, vì limiter/cooldown/circuit hiện nằm trong memory của từng process.
- Nhiều upload có thể được nhận vào durable queue; chỉ một operator khởi chạy Verify và không chạy Verify song song với upload trong phần trình diễn.
- Chạy một ảnh synthetic smoke trước phần demo. Nếu gặp 429, dừng request mới, giữ evidence/video dự phòng và dùng human-review fail-safe; không spam retry.
- Key mới không giải quyết upstream capacity và không được dùng để né rate limit.

### Local contingency trên laptop

```text
Vision__Provider=OpenRouter
Vision__FallbackEnabled=true
Vision__FallbackProvider=Ollama
Vision__MaxConcurrentRequests=1
Vision__ProviderCooldownSeconds=300
ReceiptProcessing__LeaseSeconds=1800
Ollama__BaseUrl=http://127.0.0.1:11434/
Ollama__Model=qwen3-vl:4b-instruct
Ollama__TimeoutSeconds=600
Ollama__MaxOutputTokens=4096
Ollama__ContextTokens=16384
Ollama__KeepAlive=30m
Ollama__Think=false
```

`LeaseSeconds=1800` dành cho profile local chậm, tránh lease 600 giây hết hạn nếu primary timeout rồi fallback và semantic repair kéo dài. Không áp dụng giá trị này cho public baseline nếu không cần, vì lease dài cũng làm recovery sau crash chậm hơn.

Trước demo local:

1. Khóa đúng model/digest bằng `ollama list` và xác nhận `qwen3-vl:4b-instruct` / `ee4b975b58c1`.
2. Gọi `GET http://127.0.0.1:11434/api/tags`, sau đó warm đúng model bằng một synthetic smoke.
3. Kiểm `/healthz`; lưu ý health chỉ kiểm cấu hình, không gọi thử provider.
4. Upload đúng một fixture; Audit phải chỉ rõ `PrimaryProvider`, `ServedProvider` và `FallbackUsed`.
5. Giữ laptop cắm nguồn, tắt sleep, đóng ứng dụng chiếm RAM/VRAM; không hạ context xuống 8K để đổi latency vì profile đó đã tạo 10/15 `AI_RESPONSE_TRUNCATED`.

Disk off-load không làm model tự mất token hay đổi ảnh đầu vào. Nó chủ yếu làm tăng thời gian load/chạy do thiếu VRAM. Chất lượng giảm chỉ xảy ra gián tiếp khi giảm context/output, đổi quantization/digest, bật thinking hoặc request bị timeout/truncated.

## Vì sao 429 xen kẽ 200

Evidence OpenRouter xác định request được route tới upstream Parasail; 429 xuất hiện xen kẽ 200 trong khi workspace vẫn còn credit/budget. Đây phù hợp với cửa sổ rate/capacity ngắn hạn của upstream, không phải lỗi policy C#, SQL, migration, tên API key, BTC clone source hoặc `MaxOutputTokens=4096`.

Tại thời điểm kiểm tra, API endpoint của `qwen/qwen3-vl-8b-instruct` chỉ liệt kê Parasail. Vì vậy provider failover mặc định của OpenRouter không có endpoint thứ hai cùng model để chuyển sang. `require_parameters=true` giúp khóa capability nhưng không tạo thêm capacity; tạo key khác cũng không tạo provider mới.

AURA xử lý 429 theo hướng giới hạn thiệt hại:

1. Không tự retry request 429 có tính phí.
2. Tôn trọng `Retry-After`; nếu upstream không gửi thì dùng `Vision:ProviderCooldownSeconds`.
3. Trong cooldown, request mới dùng fallback nếu fallback đã bật và đạt gate; nếu tắt thì fail-safe `ESCALATE_SYSTEM_ERROR`.
4. Worker dừng drain sau blocking provider failure; job chưa claim vẫn bền trong SQL.
5. Verify và worker chia sẻ một AI permit trong cùng process.

Không thể cam kết “không bao giờ có 429” với dịch vụ upstream bên ngoài. Mục tiêu đúng là không tạo quyết định giả, không gây bão retry, giữ hồ sơ/audit và chuyển sang đường dự phòng đã kiểm chứng.

## Benchmark Verify 5 người và ngưỡng 90 giây

Ngày 12/10, một capacity probe OpenRouter pass trong 26.817 ms. Sau đó hai lượt mô phỏng 5 client đồng thời đều đạt đúng 1 batch chạy + 4 HTTP 409, chứng minh gate chống double-click hoạt động trên live instance. Tuy nhiên batch chính gặp 429 ở TC-05 rồi TC-03; không lượt nào hoàn tất đủ 5 inference. Thời gian HTTP 84.545 ms và 46.584 ms là thời gian batch kết thúc có system error, không phải bằng chứng 5/5 dưới 90 giây.

Không giảm delay để đổi lấy số latency đẹp khi reliability đang fail. `Verify:InterCaseDelayMs` có miền hợp lệ 0–30.000 ms; tăng quá lớn có thể làm app fail validation hoặc chắc chắn phá SLA, còn giảm về 0 làm request burst hơn. Chỉ tuning sau khi có provider/GPU đủ capacity và phải chạy lại accuracy + reliability gate.

Chi tiết và runner tái lập: `OPENROUTER_VERIFY_CONCURRENCY_BENCHMARK_2026-10-12.md` và `tools/Invoke-ConcurrentVerifySmoke.ps1`.

## Hướng mở rộng sau cuộc thi

1. Đưa Ollama/GPU thành service riêng có TLS, authentication, health probe, rate limit và capacity SLO.
2. Chuyển semaphore/cooldown/circuit state sang Redis hoặc SQL trước khi chạy nhiều app instance.
3. Thêm lease renewal/heartbeat cho inference dài thay vì chỉ tăng lease tĩnh.
4. Benchmark model-level fallback khác trên cùng contract trước khi khai báo trong OpenRouter; không đổi model âm thầm.
5. Chạy load gate 1/2/5 người và báo riêng accepted latency, queue wait, inference latency, error rate, missed/over escalation.

## Thay đổi source trong checkpoint này

Không đổi pipeline, policy, schema, routing hay kiến trúc. Chỉ đồng bộ default Ollama với profile đã có evidence: timeout 600 giây, context 16384, output 4096, keep-alive 30 phút và `Think=false`. Fallback vẫn mặc định tắt.

