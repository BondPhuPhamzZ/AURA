# OpenRouter post-contract Test Kit v2 — 07/10/2026

## Phạm vi và kết luận

Đây là batch hậu evidence-contract còn thiếu: 15 ca cân bằng trong
`test_kit/judge-manifest.json`, gửi qua đúng live upload endpoint bằng
OpenRouter/Qwen3-VL-8B-Instruct. Test Kit v3 raw và v3.1 đã có batch hậu-contract riêng nên
không được chạy lại. Bank 30 ca của v2 là nguồn phát triển; judge set 15 ca mới là gate đã
khóa, vì vậy không dùng thêm 30 request để làm lẫn claim.

- 15/15 request hoàn tất; 0 system error.
- Exact decision: **14/15 = 93,33%**.
- Missed escalation: **1/10**; over-escalation: **0/5**.
- Field UTF-8-safe: **69/75 = 92%**.
- Accepted P50/P95: 584/777 ms.
- End-to-end P50/P95: 14.669/37.054 ms.
- Fallback attempt/success: 0/0; cả 15 ca do OpenRouter phục vụ.

Evidence local:

`D:\aura\demo_evidence\21_openrouter_postcontract_v2_2026-10-07\01_live_openrouter`

## Ca lệch TK-12

`TK-12-blurred-total.jpg` khóa expected `ESCALATE_FACT` nhưng actual là `AUTO_APPROVE`.
Model trả `totalAmount=336000`, `totalAmountSource=PRINTED_FINAL_TOTAL`, evidence
`TỔNG THANH TOÁN 336.000 đ`, confidence 0,95 và không báo warning. Policy C# nhận một fact
đúng schema/provenance nên không có tín hiệu độc lập để biết pixel đã bị làm mờ.

Đây là đúng failure mode đã thấy ở Ollama 4B: phép blur v2 vẫn còn cấu trúc chữ/số đủ để VLM
đọc hoặc suy đoán. Không nên nới/siết policy bằng một heuristic đoán ảnh mờ, vì có thể làm hỏng
hóa đơn hợp lệ. Fixture v3 `R3-06` đã thay bằng physical ink loss làm mất nét thật và đi đúng
`ESCALATE_FACT`; v3.1 vì thế là safety gate hiện hành.

TK-12 cũng có mismatch taxonomy field: model đặt `FACT-2609-012` vào `receiptNumber` thay vì
`invoiceNumber`. Việc này không gây missed decision; nguyên nhân quyết định sai là model khẳng
định có printed final total.

## So sánh có kiểm soát claim

| Bộ / thời điểm | Exact decision | Missed | Over | System error | Field |
|---|---:|---:|---:|---:|---:|
| v2 OpenRouter lịch sử 29/09 | 15/15 | 0 | 0 | 0 | 70/75 |
| v2 OpenRouter hậu-contract 07/10 | 14/15 | 1 | 0 | 0 | 69/75 |
| v3 OpenRouter raw hậu-contract | 11/15 | 2 | 2 | 0 | 142/145 |
| v3.1 OpenRouter hậu-contract | 13/15 | 0 | 2 | 0 | 140/146 |

Các lượt chạy khác ngày/build/hosting nên latency và chênh lệch chất lượng chỉ là quan sát,
không đủ để suy ra evidence contract làm model kém đi. V2 hậu-contract không còn 15/15 như
lịch sử; v3.1 mới là bộ phản ánh hợp đồng và thiết kế fixture hiện hành tốt hơn.

## Tính toàn vẹn và ranh giới bằng chứng

- Judge manifest SHA-256:
  `D3EC52A8A9F09B1E7798ECD1C9947D9E4C78ECBA6E2FC8FB20B62EB117782007`.
- Source manifest SHA-256:
  `EA2DFFE3DD1C6E546886BFA3795D2473E368FC19B6277A5521F05FCB821472BD`.
- Raw results SHA-256:
  `7FE3E2C7997F9488B0ABBD318151F16C073550B7EC94981DF421CA007A513347`.
- Metadata SHA-256:
  `A59D9ADB3B576A2767366EEA416B678AC4879253EA1156C7405407E3795102EA`.
- Summary SHA-256:
  `190C0176752398BEF89970BA6D91AECF31358B9A4CF49A63FC7618DEDA2E9E12`.
- UTF-8 field re-evaluation SHA-256:
  `90E9ADE1674739860B6BDEFCD7833026E3A54AE48C4FE83363E4238F61B69D89`.
- Runner metadata ghi local checkout `971b2270e31621adbb68fc5c53b02a1b1ba0fca5`.

Live server chưa expose commit attestation; checkout SHA không được trình bày như server-reported
SHA. Raw `metadata/results/summary` không sửa. `field-reevaluation-utf8.json` chỉ đo lại offline
từ raw output, không gọi AI.

## Quyết định kỹ thuật

1. Giữ OpenRouter 8B làm primary và `FallbackEnabled=false` cho demo.
2. Không chạy lại v2/v3/v3.1 để làm đẹp chỉ số.
3. Không sửa policy chỉ để TK-12 v2 pass; dùng v3.1 làm safety regression hiện hành.
4. GPU BTC chỉ benchmark Ollama 8B bằng đúng v3.1 và cấu hình đã khóa.
5. Trước demo chỉ chạy Verify 5 ca, workflow/Audit smoke và readiness; tránh burn toàn bộ dataset.
