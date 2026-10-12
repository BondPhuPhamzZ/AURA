# Qwen3.8 Flash / Alibaba benchmark — 12/10/2026

## Kết luận

`qwen/qwen3.8-flash` qua OpenRouter/Alibaba đã vượt các cổng kỹ thuật và regression được phép chạy tại thời điểm đo:

- Release build sạch và 161/161 automated test pass;
- smoke một ảnh pass sau khi khóa `OpenRouter:ReasoningEffort=none`;
- đúng **15/15** quyết định trên 15 receipt thật post-holdout, **0 missed**, **0 over**, **0 system error**, field trọng yếu **30/30**;
- P50/P95 end-to-end **10.807/26.687 ms**;
- 20/20 upstream calls trong batch trả HTTP 200, không có 429; năm call tăng thêm là semantic repair có kiểm soát;
- năm upload đồng thời đều được nhận và hoàn tất, accepted P95 **469 ms**, end-to-end P95 **41.079 ms**, dưới gate 90 giây dù AI concurrency vẫn là 1.
- sau publish/recycle, live Verify đạt **5/5** trong **65.004 ms**, không có system/provider error; năm client đồng thời chỉ tạo một batch thật với 1 HTTP 200 và 4 HTTP 409.

Vì vậy source mặc định chuyển từ `qwen/qwen3-vl-8b-instruct` sang `qwen/qwen3.8-flash`, reasoning `none`, fallback vẫn tắt và AI concurrency vẫn là 1. Đây là **release candidate cho demo có giám sát**, không phải production SLA và không xóa các bằng chứng 429 trước đây của Parasail.

## Ranh giới so sánh

Hai lượt dùng cùng manifest đã khóa và cùng 15 ảnh:

- manifest SHA-256: `850479750F65BB201ABBE9CAEDF7F4ED5C859A39110CB05C959F8185F2476FB0`;
- baseline 8B ngày 07/10 là tiền chỉnh;
- candidate Qwen3.8 Flash ngày 12/10 là hậu chỉnh, sau các thay đổi policy/semantic hardening;
- tập ảnh đã được nhìn thấy nên đây là post-holdout regression, không phải holdout mù mới;
- do khác revision, thời điểm và upstream, không được diễn giải toàn bộ chênh lệch là tác động riêng của model.

| Chỉ số | Qwen3-VL-8B baseline | Qwen3.8 Flash candidate | Chênh lệch |
|---|---:|---:|---:|
| Decision exact | 11/15 | **15/15** | +4 ca |
| Missed escalation | 0 | **0** | 0 |
| Over-escalation | 4 | **0** | -4 |
| System error | 0 | **0** | 0 |
| Field exact trọng yếu | 28/30 | **30/30** | +2 field |
| Accepted P50/P95 | 657/2.051 ms | **18/184 ms** | -639/-1.867 ms |
| E2E P50/P95 | 12.348/26.277 ms | **10.807/26.687 ms** | -1.541/+410 ms |

P95 E2E gần như ngang nhau; candidate nhanh hơn ở P50 nhưng semantic repair làm một số ca đuôi dài. Không dùng 15/15 này làm claim accuracy tổng quát; official holdout raw 10/15 và adjudicated 11/15 vẫn bất biến.

## Vì sao smoke đầu tiên thất bại

Lần chạy đầu giữ reasoning mặc định của model:

- upstream trả HTTP 200, không phải lỗi provider/capacity;
- cả hai attempt đều dùng đúng 4.096 completion token;
- `finish_reason=length`, AURA fail-safe thành `AI_RESPONSE_TRUNCATED`;
- E2E 114.329 ms và không sinh quyết định tự động.

OpenRouter quy định reasoning tokens được tính trong `max_tokens`. Qwen3.8 Flash đã dùng hết ngân sách trước khi hoàn tất JSON. Tăng output cap sẽ kéo dài latency và chi phí nhưng không giải quyết đúng mục tiêu extraction. Candidate vì thế gửi:

```json
"reasoning": { "effort": "none" }
```

Smoke thứ hai hoàn tất trong 11.053 ms, completion 568 token và trả đúng `AUTO_APPROVE`. Evidence thất bại đầu tiên được giữ nguyên, không replay để thay thế.

## Cấu hình đã khóa

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

`ReasoningEffort` được validate: để trống hoặc `none|minimal|low|medium|high|xhigh|max`. Adapter chỉ gửi object reasoning khi giá trị không rỗng. Health endpoint công khai model và reasoning effort để đối chiếu cấu hình deploy nhưng không lộ API key.

Snapshot endpoint tại thời điểm đo liệt kê hai endpoint Alibaba cho model, context 1.000.000, max completion 131.072 và uptime 30 phút 100%. Đây là ảnh chụp trạng thái thời điểm chạy, không phải cam kết availability tương lai. Nguồn: <https://openrouter.ai/api/v1/models/qwen/qwen3.8-flash/endpoints>.

## Kết quả concurrency và kiến trúc async

Năm upload đồng thời không tạo năm inference cùng lúc:

1. HTTP upload lưu file/record và trả 202;
2. SQL durable queue giữ công việc qua restart;
3. Channel chỉ đánh thức worker;
4. worker claim lease và `VisionInvocationCoordinator` giữ một AI call tại một thời điểm.

| Request | Accepted | E2E | Kết quả |
|---:|---:|---:|---|
| 1 | 469 ms | 18.053 ms | Completed |
| 2 | 317 ms | 9.126 ms | Completed |
| 3 | 126 ms | 25.904 ms | Completed |
| 4 | 63 ms | 32.886 ms | Completed |
| 5 | 51 ms | 41.079 ms | Completed |

Kết quả này chứng minh admission/queue đáp ứng burst 5 người trên **một process**. Chưa tăng `MaxConcurrentRequests`: parallel inference có thể tái tạo 429 mà chưa mang lợi ích cần thiết, vì FIFO hiện vẫn đạt gate 90 giây. Multi-instance thật vẫn cần distributed limiter/cooldown/lock.

## Live Verify sau publish

Một lượt Verify duy nhất được chạy trên live URL sau khi health xác nhận đúng Qwen3.8 Flash/reasoning none. Kết quả cả năm ca đều đúng, không fallback và E2E 65.004 ms. Khi năm client cùng bấm, `WorkflowOperationGate` cho đúng một batch chạy và trả 409 cho bốn lượt còn lại; bốn click phụ không tạo thêm AI call.

Runner Windows PowerShell 5.1 ban đầu tổng hợp nhầm `0/5` do top-level JSON array đi qua `Start-Job`. Raw HTTP 200 vẫn chứa năm case `pass=true`; bản hiệu chỉnh được tạo từ raw body có SHA-256 khóa và **không replay provider**. Chi tiết: [`QWEN38_FLASH_LIVE_VERIFY_2026-10-12.md`](QWEN38_FLASH_LIVE_VERIFY_2026-10-12.md).

## Evidence và tính toàn vẹn

Evidence private, không commit ảnh receipt/raw response vào Git:

`D:\aura\demo_evidence\30_qwen38_flash_benchmark_2026-10-12`

Các phần chính:

- `00_scope_and_baseline`: baseline, manifest khóa, snapshot endpoint;
- `01_offline_gates`: build/test transcript;
- `02_candidate_smoke`: raw failure reasoning mặc định, smoke pass reasoning none và final local health xác nhận default/User Secrets cùng trỏ Qwen3.8 Flash;
- `03_candidate_real_regression/run-01`: metadata, results CSV/JSON, summary và app log;
- `04_concurrent_judges`: raw concurrency report/log;
- `04_comparison`: comparison JSON và SHA-256 inventory.

Batch 15 ca dùng 20 API calls, 189.869 prompt token, 10.101 completion token, tổng 199.970 token. Không có non-200 response trong app log. Runner metadata ghi base commit `0db009d` vì thay đổi candidate chưa commit ở thời điểm đo; report này và commit kế tiếp đóng gói đúng candidate diff đã đo.

## Quyết định deploy và rollback

Candidate đã được publish và recycle thành công trên SmarterASP. Checklist tái triển khai/rollback vẫn là:

1. thêm/cập nhật `OpenRouter__Model=qwen/qwen3.8-flash`;
2. thêm `OpenRouter__ReasoningEffort=none`;
3. giữ concurrency 1, cooldown 120 và fallback false;
4. publish toàn bộ output, recycle pool;
5. `/healthz` phải trả model mới, reasoning `none`, DB/migration/storage OK;
6. chạy đúng một synthetic smoke/Verify có kiểm soát trước khi freeze; không chạy lại 15 receipt để săn số đẹp.

Rollback không cần code change: đặt lại model `qwen/qwen3-vl-8b-instruct`, để trống `OpenRouter__ReasoningEffort` nếu muốn hành vi model cũ, recycle và smoke lại. Nếu Alibaba phát sinh 429/5xx, AURA vẫn fail-safe, cooldown, không retry 429 và không giả quyết định nghiệp vụ.
