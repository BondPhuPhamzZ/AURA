# Live Test Kit v3.1 result — 07/10/2026

## Kết luận

Live URL `https://bondphupham-001-site1.ltempurl.com` đạt technical/safety gate cho
**supervised MVP demo** sau lần publish mới. Đây không phải production accuracy claim.

- Public endpoint: 5/5 health request HTTP 200, `status=ok`.
- Security negative: 4/4 pass; policy/appsettings không public, POST thiếu antiforgery bị chặn.
- V3.1: 15/15 request hoàn tất, 13/15 exact decision, **0 missed escalation**, 0 system error.
- Fallback: 0 attempt, 0 success; toàn bộ 15 ca do OpenRouter phục vụ.
- Latency: accepted P50/P95 593/1.726 ms; end-to-end P50/P95 6.312/33.905 ms.
- Field metric UTF-8-safe: 140/146 = 95,89%.

Evidence local:

`D:\aura\demo_evidence\20_smarterasp_policy_v31_2026-10-07`

## Tính toàn vẹn

| Thành phần | SHA-256 |
|---|---|
| Judge manifest | `5781E37F3E6C42EA0C8154B01F83FA2613AD097AC6D1B1851DCFA12663872462` |
| Source manifest | `796F1E2E78F830A84E21D010DAD273F94229168D0CFEB32393F96A8EDE0F447A` |
| Offline field re-evaluation | `0153436CFB9AD4AA7490DC55C21D33452F6321643553F70B4BCAB098043C4FF0` |

Runner metadata ghi local checkout `f23dd4c6dbda7361cccb6d549590f4465c3890e5`. Live
server chưa có endpoint attestation commit, nên không gọi giá trị này là server-reported SHA.
Tuy vậy, R3-04 AUTO, R3-08 FACT và R3-12 POLICY đều đi đúng các hardening mới, cung cấp
bằng chứng hành vi rằng bản publish mới đang hoạt động.

## Hai decision mismatch

### R3-03 — expected AUTO, actual FACT

Model đọc đúng `RRN=683104927615` nhưng sau semantic repair vẫn đặt cùng mã vào cả
`receiptNumber` và `transactionReference`. Semantic validator fail safe với duplicate-field
collision. Đây là over-escalation an toàn, không phải policy C# bỏ sót điều cấm.

### R3-15 — expected AUTHORITY, actual FACT

Model đọc đúng tổng 2.350.000 VND và `TRC-261005-1508`, nhưng cũng lặp mã vào cả hai trường
identifier. Thứ tự `FACT > POLICY > AUTHORITY` làm FACT thắng AUTHORITY. Hồ sơ vẫn được
chuyển người duyệt, nên không phải missed escalation; lý do escalation chưa đúng taxonomy.

Không nên bỏ duplicate validator chỉ để tăng exact accuracy. Hướng P1 là thêm provenance
label cho identifier hoặc canonicalization có audit; không đoán loại identifier từ prefix.

## Field metric và sự cố encoding runner

`summary.json` gốc ghi 125/146 vì Windows PowerShell 5.1 đọc manifest UTF-8 không BOM như
ANSI, làm mojibake tên người bán trong expected facts. Raw model outputs và decision không bị
đổi. `field-reevaluation-utf8.json` đọc UTF-8 tường minh và cho kết quả đúng 140/146.

Sáu field mismatch thật:

- R3-01: sai `invoiceSerial`;
- R3-08: sai `documentType`;
- R3-11: thiếu `transactionReference`;
- R3-12: sai `documentType`, thiếu `documentNumber`;
- R3-13: thiếu `invoiceSerial`.

Runner đã được sửa dùng `UTF8Encoding(false, true)`; công cụ offline
`Measure-ExtendedDatasetEvidence.ps1` cho phép tái tính từ raw response mà không gọi AI.

## Ý nghĩa cho OpenRouter 8B và Ollama

V3.1 chỉ đo OpenRouter/Qwen3-VL-8B; không chứng minh tham số 8B là nguyên nhân duy nhất.
So sánh hợp lệ phải dùng cùng ảnh/hash, prompt, policy, context, output cap và metric. Lịch sử
cùng judge set v2 cho thấy OpenRouter 8B 15/15 và Ollama 4B 14/15; post-policy Ollama 4B
13/15 trên cấu hình khác. Evidence hiện nghiêng về 8B/OpenRouter ổn định hơn, nhưng còn ảnh
hưởng từ quantization, context, phần cứng và provider.

Live health hiện có `fallbackEnabled=false` và batch ghi `ServedProvider=OpenRouter`,
`FallbackUsed=false`. `configuredFallbackProvider=Ollama` chỉ là tên provider dự phòng trong
cấu hình, không có nghĩa Ollama đang chạy trên SmarterASP. Ollama chỉ self-host khi process
Ollama chạy trên chính host AURA (thường là laptop/GPU BTC) và được chủ động bật.

## Hướng tiếp theo

1. Giữ OpenRouter 8B làm baseline demo; không sửa thêm policy theo hai mismatch taxonomy.
2. Chạy Ollama 8B trên GPU BTC bằng đúng v3.1 để có so sánh apples-to-apples; database/storage
   riêng, fallback tắt, context 16384/output 4096.
3. Thu `ollama ps`, `nvidia-smi`, token/duration, decision/field metrics và semantic repair.
4. Chỉ cân nhắc fallback sau khi Ollama-only đạt 0 system error, 0 missed escalation và latency
   có biên an toàn.
5. Hoàn tất ba user session và rehearsal; public URL chỉ dưới nhãn supervised MVP/demo.
