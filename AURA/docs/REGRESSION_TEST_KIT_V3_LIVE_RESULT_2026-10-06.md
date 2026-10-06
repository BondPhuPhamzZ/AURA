# Live OpenRouter regression — Test Kit v3 — 06/10/2026

## Kết luận

Test Kit v3 đã chạy đúng **một batch 15 ca** trên live AURA/OpenRouter tại commit `64cd61033100e5e252e0a46cc57fad401417ce9d`. Raw result được giữ bất biến: **11/15 decision**, **142/145 field**, 0 system error, 0 fallback; P50/P95 end-to-end **9.235/37.917 ms**.

Đây là synthetic post-holdout regression, không phải blind holdout hoặc production accuracy. V2, official holdout và các raw result trước đó không bị thay thế.

## Khóa trước request

- Judge manifest SHA-256: `0923863135A1CF7DED39BA4AEDFA1AA7114968EAE2276724573A5D356CC9662D`.
- Source manifest SHA-256: `E065860D67D1F38FEA7F3E765AF4A29BC6110F704BD88D5D08E6FFAF2C05EBEE`.
- Runner xác minh đủ 15 image hash trước khi lấy token/upload.
- Health đầu batch: `ok`; database/storage/policy/migration đều đạt; provider `OpenRouter`, model `qwen/qwen3-vl-8b-instruct`, fallback `false`.
- Evidence raw ngoài Git: `D:\aura\demo_evidence\19_regression_test_kit_v3_2026-10-06\01_live_openrouter`.

## Kết quả

| Metric | Raw v3 |
|---|---:|
| Decision exact | 11/15 — 73,33% |
| Field exact | 142/145 — 97,93% |
| Missed escalation | 2/10 — R3-08, R3-12 |
| Over-escalation | 2/5 — R3-03, R3-04 |
| System error | 0 |
| Fallback | 0 |
| Accepted P50/P95 | 240/4.309 ms |
| End-to-end P50/P95 | 9.235/37.917 ms |

## Phân tích bốn mismatch

### R3-03 — over-escalation an toàn

Ảnh in `RRN: 683104927615`. Model điền cùng mã vào `receiptNumber` và `transactionReference`; một semantic repair vẫn giữ trùng. Validator fail-safe `ESCALATE_FACT`. Không tự chọn một field ở backend vì JSON hiện không giữ raw identifier-label evidence đủ để chứng minh lựa chọn đó. Đây là over-escalation cần cải thiện model/contract, không phải lý do nới validator.

### R3-04 — lỗi deterministic policy matcher

Ảnh in `Bìa hồ sơ`; model làm mất dấu và trả `Bia hồ sơ`. Vì vậy chỉ giữ dấu trong matcher là chưa đủ. Bản hardening vừa giữ dấu khi model đọc đúng, vừa dùng ngoại lệ ngữ cảnh hẹp cho các cụm văn phòng phẩm như `Bia hồ sơ/còng/nhựa/cứng/trình ký` khi OCR mất dấu. Các ngữ cảnh rượu bia rõ ràng như `Bia lon/chai`, brand, `beer`, `rượu/ruou` vẫn bị chặn.

### R3-08 — safety gap deterministic

Số biên nhận bị mất mực và model chỉ trả `RCF`. Logic cũ coi mọi chuỗi không rỗng là traceable identifier nên auto-approve. Bản hardening coi một prefix toàn chữ dài tối đa bốn ký tự là mảnh không đủ truy vết, cho semantic repair một lần và vẫn FACT nếu không phục hồi được. Không áp minimum length toàn cục cho mã số ngắn hợp lệ.

### R3-12 — ground-truth precondition sai

Ảnh v3 chỉ in `Số chứng từ`. Theo policy, `documentNumber` không thay `invoiceNumber/receiptNumber/transactionReference`; do đó FACT phải ưu tiên trước POLICY. Raw v3 không được relabel. Revision v3.1 tạo ảnh/hash mới, in riêng `Số biên nhận` và `Số chứng từ`, rồi mới kỳ vọng POLICY vì `Vé xem phim`.

## Quyết định

- Raw v3: **không đạt safety gate** vì có missed escalation.
- Hardening deterministic và v3.1 phải pass full offline regression, được publish, rồi chạy đúng một live v3.1 batch mới.
- Cổng v3.1: 15 completed, 0 system error, 0 missed escalation. R3-03 có thể còn over-escalate fail-safe và phải được báo cáo, không được tự động hợp thức hóa.
- OpenRouter vẫn là primary, fallback vẫn tắt; không có thay đổi production config trong lượt v3.
