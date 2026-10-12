# Live post-policy validation — 12/10/2026

## Phạm vi

- URL: `https://bondphupham-001-site1.ltempurl.com`
- Commit được publish: `2b75f3f3860b41db45e7167170ae5eb0738f0b9c`
- Provider/model: OpenRouter / `qwen/qwen3-vl-8b-instruct`
- Fallback: tắt
- Bộ 15 receipt là post-holdout regression, không còn là blind holdout.

## Gate đã pass

- `/healthz` pass 3/3; home HTTP 200; database, migration và storage đều sẵn sàng.
- Security-negative pass 4/4: business rule và appsettings không public; Verify/upload thiếu antiforgery bị từ chối.
- Upload-negative pass 2/2: JPEG sai magic bytes và file vượt 5 MB bị từ chối trước storage/DB/AI.
- UI production tải hoàn tất, không có console error hoặc document-level horizontal overflow; tên mặc định là `Gia Phú`.
- Offline candidate pass `149/149`; Release build 0 warning, 0 error.

## Verify và capacity gate

Addendum 12/10: capacity probe mới hoàn tất một ảnh bằng OpenRouter trong 26.817 ms. Hai lượt mô phỏng 5 giám khảo đều cho đúng 1 HTTP 200 + 4 HTTP 409, nhưng batch lần lượt chỉ đạt 4/5 và 2/5 vì upstream 429. Các HTTP batch 84.545 ms/46.584 ms đều kết thúc sớm khi lỗi nên **không** chứng minh 5 inference dưới 90 giây. Xem `OPENROUTER_VERIFY_CONCURRENCY_BENCHMARK_2026-10-12.md`.

Một lượt Verify chính thức không hợp lệ để chấm accuracy: TC-01 pass, TC-02 nhận `AI_RATE_LIMIT` (HTTP 429), TC-03–TC-05 được dừng có chủ đích để bảo vệ quota. Hệ thống không tạo PASS giả.

Một probe BH-03 sau cooldown pass `AUTO_APPROVE`, 2/2 field, OpenRouter phục vụ, fallback không dùng. Regression thật sau đó được chạy tuần tự, cách nhau 60 giây và dừng tại system error đầu tiên:

| Chỉ số | Kết quả quan sát |
|---|---:|
| Hoàn tất trước khi dừng | 10/15 |
| Exact trong phần đã chạy | 8/10 |
| Missed escalation | 0 |
| Over-escalation | 1 — BH-01 |
| System error | 1 — BH-10, HTTP 429 |
| Non-system E2E P50/P95 | 13,689 / 137,734 ms |

Batch này **không phải accuracy result** vì chưa hoàn tất 15/15 và có system error. Số so sánh công khai gần nhất vẫn là regression 07/10: 11/15 exact, 0 missed, 4 over, 0 system error. BH-01 vẫn fail-safe vì model chọn `TIỀN MẶT 500.000` làm total thay cho dòng tổng 60.000; backend không suy total từ claimed amount hoặc phép trừ tiền thối.

## Phân tích 2–5 người dùng/giám khảo

- Upload thường không giữ HTTP chờ AI: hồ sơ được lưu `PENDING`, trả 202 và một worker claim FIFO. Vì vậy 2–5 upload gần nhau không làm mất hồ sơ, nhưng thời gian chờ cộng dồn.
- Worker hiện là một reader/processor; đây là chủ ý để giữ tải AI tuần tự trên một instance. Signal channel chỉ đánh thức worker, DB mới là queue bền vững.
- Verify đi trực tiếp qua Vision và có `WorkflowOperationGate`: chỉ một Verify trên instance; lượt Verify thứ hai nhận HTTP 409.
- Verify và worker dùng chung `VisionInvocationCoordinator`, nên tối đa một lời gọi AI trong cùng process. `WorkflowOperationGate` chỉ chống hai batch Verify chạy chồng. Nếu hosting scale thành nhiều process thì semaphore/cooldown/circuit/gate in-memory không còn là giới hạn toàn cục.
- Do production đã trả 429 ngay cả khi regression cách nhau 60 giây, không có cơ sở tuyên bố multi-user AI capacity. OpenRouter Activity ghi final provider là Parasail; chiều giới hạn cụ thể (account, provider, rolling token/request hoặc credit/key cap) không xuất hiện trong response mà AURA lưu, nên phải xác nhận ở OpenRouter Activity/Credits/Key.

## Quyết định vận hành

- Demo hiện tại là **single-operator supervised demo**: một người bấm, những người khác quan sát.
- Không chạy Verify đồng thời với upload; không bấm Verify nhiều lần; không chạy 15 ca trong buổi chấm.
- Benchmark concurrent mới chứng minh các click Verify phụ nhận 409 và không nhân số AI call, nhưng full-batch reliability vẫn fail vì 429; không dùng thời gian của batch kết thúc sớm để claim 5/5 dưới 90 giây.
- Trước demo kiểm OpenRouter Activity, credit, key limit và provider; chạy đúng một ảnh smoke. Nếu 429, chuyển sang trình bày evidence/video và human fail-safe, không retry dồn.
- Chỉ tuyên bố 2–5 hồ sơ được tiếp nhận bền vững qua DB queue; chưa tuyên bố 2–5 quyết định AI đồng thời hoặc SLA.
- Fallback Ollama vẫn tắt trên production vì endpoint local của SmarterASP không trỏ tới GPU riêng. Ollama 4B 16K đã đạt safety regression nhưng latency trên laptop quá cao cho Verify 90 giây.

## Evidence local

`D:\aura\demo_evidence\25_smarterasp_postpolicy_2b75f3f_2026-10-12`

Ảnh receipt và raw facts có thể chứa dữ liệu giao dịch nên không commit vào repository công khai.
