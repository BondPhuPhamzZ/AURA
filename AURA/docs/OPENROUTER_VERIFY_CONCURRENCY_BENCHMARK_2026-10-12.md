# OpenRouter Verify latency and concurrency benchmark — 12/10/2026

> **Addendum hiện hành:** Phần kết luận bên dưới là evidence lịch sử của `qwen/qwen3-vl-8b-instruct`/Parasail. Candidate `qwen/qwen3.8-flash`/Alibaba, reasoning `none`, đã đóng live Verify sau publish: **5/5**, 0 system/provider error, E2E **65,004 giây**, đúng 1 HTTP 200 + 4 HTTP 409 khi năm client cùng bấm. Raw cũ không bị ghi đè; xem `QWEN38_FLASH_LIVE_VERIFY_2026-10-12.md` và `QWEN38_FLASH_BENCHMARK_2026-10-12.md`.

## Trạng thái live hiện hành

Candidate mới đã giải quyết blocker quan sát được trong hai lượt Parasail cũ tại thời điểm đo. Có thể dùng Verify trong live demo có giám sát với một operator. Vẫn giữ video/evidence dự phòng và không gọi một lượt pass là SLA production. `WorkflowOperationGate` chỉ là khóa theo process, vì vậy IIS baseline tiếp tục dùng một worker.

## Kết luận

Tại thời điểm đo model 8B cũ, live AURA **đã bảo vệ đúng khi 5 người cùng bấm Verify**, nhưng route Parasail **chưa đạt reliability gate cho demo 5 ca**:

- cả 2/2 lượt concurrent đều chỉ cho đúng 1 batch chạy và trả HTTP 409 cho 4 lượt còn lại;
- capacity probe một ảnh trước batch hoàn tất bằng OpenRouter trong 26.817 ms;
- hai batch Verify đều gặp `AI_RATE_LIMIT`: lượt 1 xử lý đúng 4 ca rồi lỗi ở TC-05; lượt 2 xử lý đúng 2 ca rồi lỗi ở TC-03 và bỏ qua TC-04/TC-05 để bảo vệ quota;
- thời gian HTTP của batch thắng là 84.545 ms và 46.584 ms, nhưng **không lượt nào xử lý thành công đủ 5 inference**, nên không được tuyên bố “5 ca dưới 90 giây”.

Vì vậy OpenRouter có thể dùng cho một smoke/upload có giám sát, nhưng chưa đủ bằng chứng để chọn live Verify làm phần bắt buộc của video hoặc buổi chấm.

## Cấu hình và tính toàn vẹn

- URL: `https://bondphupham-001-site1.ltempurl.com`
- Source HEAD trước benchmark: `975f9606287e2c1fefab0caeacbcfce01e028b4e`
- Provider/model: OpenRouter / `qwen/qwen3-vl-8b-instruct`
- `Vision:MaxConcurrentRequests=1`
- `Vision:ProviderCooldownSeconds=120`
- Verify bật; fallback tắt
- Business-rule SHA-256: `93D6E38CEE8C17C1837E3A9218E232F2BCA86C09F5ADB340442865A6EC925905`
- Verify manifest SHA-256: `F3D1660B18DC36685FC9F687FA1422266ED3683D2CBC34BDCDFA2CFB5D0149DC`

Ảnh Verify là fixture tổng hợp đã có trong repository. Không dùng 15 receipt thật cho benchmark này.

## Kết quả

### Capacity gate

| Chỉ số | Kết quả |
|---|---:|
| Upload được nhận | 10.234 ms |
| Hoàn tất end-to-end | 26.817 ms |
| Trạng thái | `AUTO_APPROVE` |
| Provider phục vụ | OpenRouter |
| Fallback | Không |
| System/client error | 0 |

### Hai lượt mô phỏng 5 giám khảo

| Chỉ số | Lượt 1 | Lượt 2 |
|---|---:|---:|
| HTTP 200 | 1 | 1 |
| HTTP 409 | 4 | 4 |
| Batch HTTP end-to-end | 84.545 ms | 46.584 ms |
| Case đúng | 4/5 | 2/5 |
| System error trong kết quả | 1 | 3 |
| Lời gọi AI thật trước khi dừng | 5 | 3 |
| Ca phát sinh 429 thật | TC-05 | TC-03 |
| Ca bỏ qua sau lỗi | 0 | TC-04, TC-05 |

Sáu ca nhận được output AI hoàn chỉnh đều khớp expected. Tuy nhiên đơn vị chấm là cả batch 5 ca; cả hai batch đều fail reliability vì có system error.

## Điều benchmark chứng minh

1. `WorkflowOperationGate` hoạt động trên live instance: click thứ hai đến thứ năm không tạo thêm batch AI.
2. `VisionInvocationCoordinator` giữ một permit AI dùng chung giữa Verify và worker trong cùng process.
3. Controller không tạo PASS giả khi provider lỗi. Sau lỗi blocking, các case còn lại được ghi `ESCALATE_SYSTEM_ERROR` nhưng không gọi AI tiếp.
4. 429 là upstream/provider capacity: business rule, database, storage và application health vẫn hoạt động.
5. `/healthz` không gọi provider, nên `status=ok` không phải bằng chứng OpenRouter đang nhận inference.

## Giới hạn của kết luận concurrency

`WorkflowOperationGate`, semaphore AI, cooldown và circuit breaker đang nằm trong memory. Bằng chứng 1×200 + 4×409 đúng cho live deployment được đo, nhưng để giữ tính chất này phải đặt IIS `Maximum Worker Processes = 1`. Nếu scale thành nhiều worker/process/server, cần distributed lock và distributed capacity limiter trước khi tuyên bố global single-batch.

Upload thường không dùng `WorkflowOperationGate`: nhiều upload vẫn được nhận 202 và xếp vào SQL durable queue. Chúng không mất dữ liệu, nhưng với concurrency AI bằng 1 thì thời gian chờ tăng tuyến tính. Không chạy upload cùng Verify trong phần demo.

## Quyết định vận hành

- Không chạy 15 receipt regression cũ hoặc mini-holdout mới trên OpenRouter ở trạng thái provider hiện tại.
- Không giảm `Verify:InterCaseDelayMs` chỉ để săn mốc 90 giây: hai lượt đã gặp 429; giảm khoảng cách sẽ tăng burst mà chưa tạo thêm endpoint dự phòng.
- Trước video/buổi chấm, chạy đúng một fixture smoke. Chỉ tiếp tục live flow nếu smoke pass; nếu 429 thì dùng evidence/video đã quay và human fail-safe.
- Live Verify chỉ do một operator bấm. Các giám khảo khác quan sát; nếu họ bấm cùng lúc, UI phải giải thích HTTP 409 là bảo vệ quota chứ không phải mất dữ liệu.
- Muốn cam kết 5/5 dưới 90 giây cần benchmark một backend có capacity ổn định hơn: GPU 8B được host riêng hoặc model fallback đã qua cùng safety/accuracy gate. Không đổi model âm thầm trước khi đo.

## Công cụ tái lập

```powershell
.\tools\Invoke-ConcurrentVerifySmoke.ps1 `
  -BaseUrl 'https://bondphupham-001-site1.ltempurl.com' `
  -Clients 5 `
  -OutputDirectory 'D:\evidence\verify-concurrency' `
  -LatencyTargetMs 90000
```

Runner giữ raw body của từng client, summary JSON, latency, số HTTP 200/409, kết quả từng case và exit code khác 0 nếu gate, accuracy, system-error hoặc latency không đạt.

Raw evidence cục bộ: `D:\aura\demo_evidence\29_openrouter_verify_concurrency_2026-10-12`. Không commit raw response/evidence lên repository công khai.
