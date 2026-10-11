# Provider capacity hardening — 12/10/2026

## Phạm vi và nguyên nhân

OpenRouter Upstream Requests xác nhận các lỗi live là HTTP 429 từ provider Parasail, mỗi lỗi có một attempt. Key production có nhãn `btc_key_test_1`, vẫn còn credit và mới dùng dưới giới hạn chi tiêu tháng; `192 requests` là usage của workspace trong tuần, không phải quota. Vì vậy nguyên nhân quan sát được là rate/capacity ngắn hạn ở upstream provider, không phải policy C#, SQL, số dư hay migration. Ảnh dashboard/key là evidence private, không commit vào Git.

## Thay đổi ứng viên

- `VisionInvocationCoordinator` là singleton dùng chung Upload Worker và Verify Harness; mặc định chỉ cho một chuỗi primary→fallback gọi AI tại một thời điểm.
- HTTP 429 tạo cooldown provider mặc định 120 giây hoặc theo `Retry-After`. Trong cooldown, fallback đủ điều kiện được dùng nếu đã bật; nếu fallback tắt, request fail-safe mà không gọi thêm upstream.
- Worker dừng drain queue sau lỗi provider-wide, chờ cooldown rồi mới claim job tiếp theo. Job đã gặp lỗi vẫn được lưu `ESCALATE_SYSTEM_ERROR`; các job chưa claim còn bền trong SQL.
- Verify vẫn giữ đúng năm fixture và không ghi vào queue nghiệp vụ, nhưng chia sẻ AI gate/cooldown và trả metadata `primaryProvider`, `servedProvider`, `fallbackUsed`, `providerErrorCode`.
- Adapter gửi `X-OpenRouter-Metadata: enabled`, giữ `Retry-After`, request ID và upstream provider đã khử nhạy cảm trong server log. Chuỗi hard-code “Alibaba” đã được thay bằng “upstream provider”.
- `Verify:Enabled` cho phép tắt harness trên môi trường public không giám sát.

## Cấu hình

```text
Vision__MaxConcurrentRequests=1
Vision__ProviderCooldownSeconds=120
Verify__Enabled=true   # chỉ trong phiên demo/test có giám sát
```

Khi public URL mà không cần BGK tự chạy harness, đặt `Verify__Enabled=false`. Cấu hình này không thay đổi upload queue. Fallback production vẫn phải giữ `false` cho đến khi endpoint Ollama HTTPS ổn định, được bảo vệ và model vượt safety regression.

## Gate đã chạy trước publish

- `dotnet test tests/AURA.Tests/AURA.Tests.csproj --no-restore`: **159/159 pass**.
- `dotnet build AURA.csproj -c Release --no-restore`: **0 warning, 0 error**.
- Targeted tests chứng minh serialization, cooldown/expiry, blocking-failure classification và 429 diagnostics.

Đây là offline implementation evidence, chưa phải live deployment evidence. Sau publish phải recycle pool, kiểm `/healthz`, chạy một synthetic upload smoke, rồi mới chạy concurrency admission 2 và 5 upload. Không chạy Verify song song với phép đo upload và không dùng batch bị 429 làm accuracy claim.

## Giới hạn còn lại

Gate/cooldown/circuit breaker là in-memory theo process. App pool demo phải dùng một worker process. Multi-instance production cần distributed limiter/cooldown (SQL/Redis), lease renewal và load test riêng. Một worker xử lý AI FIFO nên 2–5 upload được tiếp nhận bền nhưng latency cộng dồn; không tuyên bố AI concurrency SLA.
