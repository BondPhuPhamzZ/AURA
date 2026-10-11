# Phân tích 4 ca over-escalation receipt thật — 12/10/2026

## Phạm vi và nguyên tắc

- Nguồn lịch sử bất biến: post-holdout regression ngày 07/10, `11/15` exact, `0` missed, `4` over, `0` system error.
- Bốn ca cần điều tra: `BH-01`, `BH-03`, `BH-14`, `BH-15`.
- Đây là regression sau holdout; không đổi ground truth, không ghi đè raw official và không gọi là blind holdout mới.
- Bản sửa không dùng `claimedAmount` để dẫn dắt OCR, không nới precedence `FACT > POLICY > AUTHORITY` và không xóa `ValidationIssues` khi evidence còn thiếu hoặc mâu thuẫn.

## Root cause theo từng ca

| Ca | Expected / regression 07/10 | Dữ kiện ảnh và raw | Root cause | Biện pháp an toàn |
|---|---|---|---|---|
| BH-01 CoopSmile | `AUTO_APPROVE` / `ESCALATE_FACT` | Ảnh in `TỔNG CỘNG 60.000`, tiền mặt `500.000`, tiền thối `-440.000`, VAT đã gồm; hai line items cộng `60.000` | Model gán sai money role: tiền mặt/VAT thành total và tiền thối thành discount. Đây là lỗi extraction; policy C# chỉ fail-safe trên facts mâu thuẫn | Chỉ sửa total khi một dòng final-total có nhãn/số rõ, subtotal và đầy đủ line items cùng khớp. Không suy từ claimed amount hoặc chỉ từ phép cộng |
| BH-03 FamilyMart | `AUTO_APPROVE` / `ESCALATE_FACT` | Một sản phẩm `12.000`; dòng `Tổng số/Tổng cộng: 1 12.000` | Repair đưa dòng summary vào `lineItems`, làm tổng dòng thành `24.000`; đồng thời có thể lặp cùng identifier sang hai field | Xóa đúng pseudo-item có nhãn summary + amount bằng total khi vẫn còn item thực; canonicalize exact duplicate identifier theo loại chứng từ |
| BH-14 Winmart 1 | `AUTO_APPROVE` / `ESCALATE_FACT` | `PTT: 622002261001106`; `Mã CQT: M1-26-MX4H6-10112701106`; total `48.200` | Model lặp chính giá trị PTT vào `receiptNumber` và `transactionReference`; validator buộc repair/FACT dù một mã giao dịch hợp lệ vẫn được giữ | Với retail/restaurant, giữ PTT ở `receiptNumber`, bỏ đúng bản sao ở `transactionReference`; `Mã CQT` vẫn tách riêng. Giá trị khác nhau không bị merge |
| BH-15 Winmart 2 | `AUTO_APPROVE` / `ESCALATE_FACT` | Cùng pattern BH-14, total `143.700` | Cùng root cause exact duplicate canonical field | Cùng normalization lossless như BH-14 |

## Thay đổi và kiểm chứng offline

- Commit code ứng viên: `d0440f2`.
- `ReceiptSemanticValidator` bổ sung ba normalization hẹp: exact duplicate canonical identifier; summary pseudo-line; printed final total có đối chứng ba nguồn.
- Bổ sung 7 targeted tests gồm negative controls để không xóa sản phẩm chứa chữ `Total`, không merge identifier khác nhau và không dựng total khi evidence không đủ.
- Kết quả: `149/149` automated tests pass; Release build `0` warning, `0` error.

## Hậu kiểm API thật trên commit `d0440f2`

Provider được xác nhận từ `/healthz`: OpenRouter, `qwen/qwen3-vl-8b-instruct`, fallback tắt, DB/migration/storage OK.

Batch 15 ca dùng lại manifest/hash adjudicated đã khóa:

- 10 decision pass, `0` missed, `0` over trên các ca provider hoàn tất;
- 5 ca `ESCALATE_SYSTEM_ERROR` do OpenRouter HTTP 429 (`AI_RATE_LIMIT`);
- vì system-error rate `33,33%`, batch này **không** được dùng làm accuracy claim hoặc thay regression 07/10.

Trong bốn ca điều tra:

- `BH-03`, `BH-14`, `BH-15`: response thật đều `AUTO_APPROVE`; currency/total đúng; không semantic repair. BH-14/BH-15 lưu warning rằng bản sao `transactionReference` đã bị loại và vẫn giữ `receiptNumber` + `taxAuthorityCode`.
- `BH-01`: batch chính gặp HTTP 429. Một targeted retry sau cooldown hoàn tất nhưng vẫn `ESCALATE_FACT`: model trả `TIEN MAT 500.000` làm total evidence, `440.000` làm discount và VAT sai vai trò. Backend giữ fail-safe vì response cuối không còn dòng tổng `60.000` đủ điều kiện xác minh.

## Kết luận và release gate

- Bản sửa giải quyết có evidence thực tế 3/4 false-positive; không tạo missed escalation trong batch quan sát được.
- `BH-01` còn là residual extraction error. Không thêm heuristic biến `cash - change` hoặc line-item sum thành total vì sẽ vi phạm guard chống suy tổng khi dòng final-total rách/mất.
- Trạng thái: **candidate an toàn để publish/recycle và chạy live gate; chưa được tuyên bố fix live hoàn tất**.
- Sau publish: health phải `ok`, fallback vẫn `false`; chạy một post-policy regression có khoảng cách đủ để tránh 429; báo riêng missed/over/system error và từng ca đổi quyết định. Chỉ freeze release nếu không có missed escalation và không có system error.

## Evidence local ngoài Git

`D:\aura\demo_evidence\24_real_receipt_postpolicy_candidate_2026-10-12`

- `03_raw_results`: batch 15 ca, có 5 `AI_RATE_LIMIT`.
- `04_targeted_bh01_after_rate_limit`: retry BH-01, provider hoàn tất nhưng quyết định vẫn fail-safe.

Ảnh receipt thật và raw response tiếp tục nằm ngoài repository công khai.
