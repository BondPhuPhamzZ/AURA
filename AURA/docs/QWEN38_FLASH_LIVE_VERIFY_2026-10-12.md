# Qwen3.8 Flash live Verify gate — 12/10/2026

## Kết luận

Sau khi publish và recycle application pool, live AURA đã vượt cổng Verify dành cho demo:

- `/healthz` trả `status=ok`, model `qwen/qwen3.8-flash`, reasoning `none`, database/migration/storage đều sẵn sàng, AI concurrency 1 và fallback tắt;
- năm client cùng khởi chạy Verify chỉ tạo **một batch thật**: 1 HTTP 200 và 4 HTTP 409;
- batch thắng đạt **5/5**, đúng ba `AUTO_APPROVE`, một `ESCALATE_FACT` và một `ESCALATE_POLICY`;
- không có `ESCALATE_SYSTEM_ERROR`, provider error hoặc fallback;
- end-to-end **65.004 ms**, đạt mục tiêu không quá 90.000 ms.

Kết quả này mở cổng kỹ thuật cho một live demo có giám sát. Nó không phải SLA production, không thay official holdout và không bảo đảm dịch vụ bên thứ ba sẽ luôn khả dụng.

## Cấu hình live đã xác nhận

```json
{
  "status": "ok",
  "provider": "OpenRouter",
  "model": "qwen/qwen3.8-flash",
  "reasoningEffort": "none",
  "processingMode": "durable-background-queue",
  "maxConcurrentAiRequests": 1,
  "providerCooldownSeconds": 120,
  "verifyHarnessEnabled": true,
  "fallbackEnabled": false,
  "configuredFallbackProvider": "Ollama",
  "fallbackProvider": null
}
```

`configuredFallbackProvider=Ollama` chỉ cho biết adapter dự phòng đã được cấu hình. `fallbackEnabled=false` và `fallbackProvider=null` xác nhận live request không thể tự chuyển sang Ollama trên SmarterASP.

## Kết quả từng ca

| Ca | Expected | Actual | Latency | Provider |
|---|---|---|---:|---|
| TC-01 | `AUTO_APPROVE` | `AUTO_APPROVE` | 9.328 ms | OpenRouter |
| TC-02 | `AUTO_APPROVE` | `AUTO_APPROVE` | 11.213 ms | OpenRouter |
| TC-03 | `AUTO_APPROVE` | `AUTO_APPROVE` | 13.655 ms | OpenRouter |
| TC-04 | `ESCALATE_FACT` | `ESCALATE_FACT` | 12.079 ms | OpenRouter |
| TC-05 | `ESCALATE_POLICY` | `ESCALATE_POLICY` | 14.043 ms | OpenRouter |

Tổng latency năm ca do harness ghi nhận là 60.318 ms. Thời gian HTTP end-to-end 65.004 ms bao gồm lấy trang/antiforgery, network, orchestration và serialize response.

## Cổng nhiều người bấm Verify

| Client | HTTP | Thời gian |
|---:|---:|---:|
| 1 | 409 | 4.272 ms |
| 2 | 409 | 4.267 ms |
| 3 | 200 | 65.004 ms |
| 4 | 409 | 4.193 ms |
| 5 | 409 | 4.132 ms |

`WorkflowOperationGate` đã ngăn bốn click phụ tạo thêm batch AI. Đây là bằng chứng trên một IIS process; baseline demo vẫn phải giữ `Maximum Worker Processes = 1`. Multi-instance cần distributed lock/limiter trước khi claim cùng tính chất toàn cục.

## Sự cố đo lường và cách hiệu chỉnh

Runner gốc ghi sai `0/5` vì Windows PowerShell 5.1 giữ mảng JSON cấp cao nhất thành một đối tượng lồng khi body đi qua `Start-Job`. Raw HTTP 200 vẫn chứa đủ năm case đều `pass=true`.

Cách xử lý:

1. giữ nguyên summary gốc;
2. giữ raw body và SHA-256 `9246676DEA78F94C289C2EF12CB65C9CBD23171893ABD284B851EC69A923A6A2`;
3. tạo summary hiệu chỉnh từ raw body, không gọi lại provider;
4. sửa runner để mở rộng mảng tường minh trên cả Windows PowerShell 5.1 và PowerShell 7.x.

Sự cố này chỉ ảnh hưởng phép tổng hợp evidence cục bộ, không ảnh hưởng AURA, provider hoặc kết quả live.

## Evidence private

Raw response và health snapshot nằm ngoài Git để tránh đưa log vận hành hoặc dữ liệu nhạy cảm vào repository công khai:

`D:\aura\demo_evidence\31_qwen38_flash_live_verify_2026-10-12`

Các file cốt lõi:

- `healthz-before-verify.json`;
- `run-01/client-03-http-200.json`;
- `run-01/client-01/02/04/05-http-409.json`;
- `run-01/concurrent-verify-summary.json` — summary gốc có lỗi parser;
- `run-01/concurrent-verify-summary.corrected.json` — summary hiệu chỉnh, không replay.

## Quyết định demo và holdout

- Live demo: kỹ thuật đã GO. Trước khi quay, chạy một rehearsal sạch bằng đúng commit đã push, một operator và không mở Verify song song với upload.
- Video dự phòng: vẫn phải giữ vì OpenRouter/Alibaba là hạ tầng bên thứ ba.
- Bộ 11 hóa đơn mới: chỉ chạy khi danh sách ảnh cuối cùng, consent/redaction, ground truth độc lập và SHA-256 manifest đã khóa. Thêm ảnh thứ 11 làm thay đổi manifest, vì vậy phải tạo phiên bản 11 ca mới trước request đầu tiên.
- Nếu bất kỳ ảnh nào đã từng được gửi model hoặc dùng để tinh chỉnh prompt/policy, batch phải gọi là post-holdout regression, không phải blind holdout.
