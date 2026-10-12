# Provider capacity hardening — 12/10/2026

> **Addendum:** Candidate `qwen/qwen3.8-flash`/Alibaba với reasoning `none` đã pass 20/20 upstream calls trong một lượt 15 receipt và năm upload đồng thời 5/5, P95 41,079 giây. Các cơ chế gate/cooldown bên dưới vẫn được giữ; incident Parasail là evidence lịch sử của model 8B cũ. Xem `QWEN38_FLASH_BENCHMARK_2026-10-12.md`.

## Phạm vi và nguyên nhân

OpenRouter Upstream Requests xác nhận các lỗi live là HTTP 429 từ provider Parasail, mỗi lỗi có một attempt. Key production có nhãn `btc_key_test_1`, vẫn còn credit và mới dùng dưới giới hạn chi tiêu tháng; `192 requests` là usage của workspace trong tuần, không phải quota. Vì vậy nguyên nhân quan sát được là rate/capacity ngắn hạn ở upstream provider, không phải policy C#, SQL, số dư hay migration. Ảnh dashboard/key là evidence private, không commit vào Git.

Không có bằng chứng việc BTC clone repository làm phát sinh 429. Evidence BTC chạy local Ollama; lỗi tại URL tunnel là `AI_RESPONSE_TRUNCATED`, không phải HTTP 4xx. `OpenRouter:MaxOutputTokens=4096` đã giữ nguyên từ 22/09/2026. Prompt/policy dài có thể tăng chi phí và latency, hoặc gây context error riêng, nhưng không giải thích response 429 đã mang metadata upstream Parasail. Không tạo key mới để né 429 provider-side; trước một batch dài, kiểm `GET /api/v1/key`/dashboard cho `limit_remaining`, `effective_budget` và trạng thái key.

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

## Gate hardening lịch sử trước khi đổi candidate

- `dotnet test tests/AURA.Tests/AURA.Tests.csproj --no-restore`: **159/159 pass**.
- `dotnet build AURA.csproj -c Release --no-restore`: **0 warning, 0 error**.
- Targeted tests chứng minh serialization, cooldown/expiry, blocking-failure classification và 429 diagnostics.

Đây là offline implementation evidence, chưa phải live deployment evidence. Sau publish phải recycle pool, kiểm `/healthz`, chạy một synthetic upload smoke, rồi mới chạy concurrency admission 2 và 5 upload. Không chạy Verify song song với phép đo upload và không dùng batch bị 429 làm accuracy claim.

Gate candidate sau đó đạt 161/161 test, Release build sạch, 15/15 post-holdout regression với 20/20 upstream HTTP 200 và burst 5/5. Số 159/159 phía trên được giữ nguyên như checkpoint lịch sử của capacity hardening.

## Giới hạn còn lại

Gate/cooldown/circuit breaker là in-memory theo process. App pool demo phải dùng một worker process. Multi-instance production cần distributed limiter/cooldown (SQL/Redis), lease renewal và load test riêng. Một worker xử lý AI FIFO nên 2–5 upload được tiếp nhận bền nhưng latency cộng dồn; không tuyên bố AI concurrency SLA.
