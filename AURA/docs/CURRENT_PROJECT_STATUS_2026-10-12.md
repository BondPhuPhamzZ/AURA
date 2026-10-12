# AURA — Current project status 12/10/2026

## Kết luận điều hành

AURA hiện là **supervised demo release candidate** với primary mới `OpenRouter / qwen/qwen3.8-flash`, reasoning `none`, fallback tắt và AI concurrency 1. Candidate đã pass build/test, một smoke, một lượt 15 receipt thật post-holdout và burst năm upload đồng thời. Source đã đồng bộ default; SmarterASP vẫn cần publish/recycle/health/smoke trước khi được coi là đang chạy candidate này.

Lỗi 429 lịch sử đã được phân lập ở upstream Parasail của model 8B cũ, không phải SQL, policy, API key budget hay BTC clone source. Candidate Alibaba không phát sinh non-200 trong 20 upstream calls của batch 15 ca. Điều này giải quyết rủi ro quan sát được trong phiên benchmark, nhưng không thể biến dịch vụ bên thứ ba thành SLA tuyệt đối.

## Evidence hiện hành

| Hạng mục | Kết quả và ranh giới |
|---|---|
| Offline suite | Release build 0 warning/error; **161/161** automated tests sau khi thêm validation/default tests |
| Final local health | `status=ok`; model `qwen/qwen3.8-flash`; reasoning `none`; DB/migration/storage OK; concurrency 1; fallback false. User Secrets local đã được đồng bộ để không còn override model 8B cũ |
| Candidate smoke | Lần reasoning mặc định fail-safe `AI_RESPONSE_TRUNCATED` 114.329 ms; lần `reasoning:none` pass 11.053 ms |
| Qwen3.8 Flash real regression | **15/15**, 0 missed, 0 over, 0 system error, 30/30 field; P50/P95 10.807/26.687 ms |
| Upstream trong batch | 20/20 HTTP 200; 20 calls gồm 15 extraction + 5 semantic repair; 199.970 total token |
| Năm upload đồng thời | 5/5 completed; accepted P95 469 ms; E2E P95 41.079 ms; concurrency AI vẫn 1 |
| Baseline 8B ngày 07/10 | 11/15, 0 missed, 4 over, 0 system error, 28/30 field; đây là baseline tiền chỉnh |
| Official holdout | Raw 10/15, adjudicated 11/15; không thay bằng regression mới |
| Ollama 4B 16K | An toàn trên regression đã chạy nhưng P95 receipt thật 384,539 giây; chỉ là contingency local |

Chi tiết và ranh giới so sánh: [`QWEN38_FLASH_BENCHMARK_2026-10-12.md`](QWEN38_FLASH_BENCHMARK_2026-10-12.md). Raw evidence private nằm tại `D:\aura\demo_evidence\30_qwen38_flash_benchmark_2026-10-12`.

## Quyết định cấu hình

```text
Vision__Provider=OpenRouter
Vision__FallbackEnabled=false
Vision__MaxConcurrentRequests=1
Vision__ProviderCooldownSeconds=120
OpenRouter__Model=qwen/qwen3.8-flash
OpenRouter__ReasoningEffort=none
OpenRouter__MaxOutputTokens=4096
OpenRouter__TimeoutSeconds=90
```

- Không tăng concurrency AI: FIFO hiện đã hoàn tất năm upload trong 41,079 giây và giảm nguy cơ burst 429.
- Không bật fallback Ollama trên SmarterASP. Loopback của host không trỏ về laptop.
- Local contingency vẫn là `qwen3-vl:4b-instruct`, context 16384, output 4096, timeout 600 giây, `Think=false`, lease 1800 giây khi bật.
- `ReasoningEffort=none` là bắt buộc với Qwen3.8 Flash cho contract extraction hiện tại; reasoning mặc định đã có raw failure rõ ràng.

## Kiến trúc nhiều giám khảo

Upload đã bất đồng bộ: HTTP 202 → SQL durable queue → Channel wake-up → background worker → atomic lease → AI capacity gate. Năm người có thể upload cùng lúc mà không mất job. Inference vẫn tuần tự trên một process; đây là lựa chọn đã benchmark đạt gate 90 giây.

Không nhầm với Verify Harness: `WorkflowOperationGate` vẫn chặn nhiều người khởi chạy cùng một batch Verify để tránh đốt quota trùng. Khi cần multi-instance, phải thay gate/cooldown/rate limiter in-memory bằng cơ chế phân tán SQL/Redis và bổ sung lease heartbeat; không chỉ tăng semaphore.

## Việc còn lại trước freeze/nộp

1. Commit/push candidate đã review và giữ evidence ngoài Git.
2. Publish toàn bộ output lên SmarterASP, thêm `OpenRouter__ReasoningEffort=none`, recycle pool.
3. Xác nhận `/healthz`: model `qwen/qwen3.8-flash`, reasoning `none`, DB/migration/storage OK, concurrency 1, fallback false.
4. Chạy đúng một synthetic smoke trên Live URL; không chạy lại 15 receipt để săn PASS.
5. Quay rehearsal/video với một operator; giữ raw evidence/video dự phòng cho sự cố Internet/provider.
6. Chỉ mở official mini-holdout mới nếu có 8–10 receipt unseen, khóa nhãn/hash trước request đầu tiên.

## Claim an toàn khi trình bày

Có thể nói candidate Alibaba đã hoàn tất 15/15 receipt regression và năm upload đồng thời dưới 90 giây trong phép đo ngày 12/10, đồng thời AURA fail-safe khi provider/output lỗi. Phải nói rõ regression này không còn blind, so sánh 8B và 3.8 Flash khác revision/thời điểm, và availability của upstream không phải production SLA.
