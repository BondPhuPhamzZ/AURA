# AURA — Official blind holdout postmortem

Cập nhật: 06/10/2026. Tài liệu này ghi kết luận trung thực của lượt official; không chứa ảnh, PII hay ground truth private.

## 1. Tính bất biến và phạm vi

- 15 ảnh thật được quyền sử dụng và đã redaction review.
- Ground truth, claimed amount, expected decision/facts và SHA-256 được khóa trước request đầu tiên.
- Manifest SHA-256: `B13EB77621A89DCFABC3E09F27810F9E7921F5E15433F03E3F9250115BA9CDE9`.
- Chạy đúng một lượt qua live AURA/OpenRouter, fallback tắt; 0 system error, 0 fallback.
- Final evidence manifest SHA-256: `FDD4B86B5B7D57E9DB0E4E44D410ED0321FD1D300E0DA5FFC0621973FD0C7035`.
- Raw results không được sửa hoặc chạy lại rồi thay thế. Từ sau lượt này, 15 ảnh chỉ là post-holdout regression; blind claim mới cần dữ liệu unseen mới.

## 2. Metrics

| View | Decision accuracy | Missed escalation | Over-escalation | Ghi chú |
|---|---:|---:|---:|---|
| Official raw | 10/15 = 66,67% | 1/9 = 11,11% | 3/6 = 50% | Số dùng cho báo cáo official |
| Post-hoc adjudicated | 11/15 = 73,33% | 1/10 = 10% | 2/5 = 40% | BH-04 ground truth sai; raw không đổi |

`28/30 = 93,33%` chỉ đo đúng hai field `currency` và `totalAmount` trên 15 ca. Không được gọi đây là full extraction accuracy. Accepted P50/P95 là 264/1.913 ms; end-to-end P50/P95 là 5.392/17.920 ms.

## 3. Root cause quan trọng

| Case | Hiện tượng | Root cause | Mức độ |
|---|---|---|---|
| BH-07 Circle K | Total value bị rách nhưng model trả 21.000 và AUTO | Contract chỉ có số tổng, không có provenance; backend không phân biệt số in với số suy từ subtotal/items | P0 fail-open |
| BH-06 Ministop | Bia nhưng FACT thay vì POLICY | Giờ `09:46:21` không khớp parser chỉ nhận `HH:mm`; FACT che POLICY | P0 deterministic |
| BH-01 CoopSmile | 60.000 bị đổi thành 444.444; cash/change/VAT bị gán sai | Money-role prompt/repair chưa đủ chặt; fractional included VAT bị xem như VND normalization error | P1 |
| BH-08 Katinat | `04-10-26` thành `2026-04-10` | Chỉ lưu ISO do model trả, không giữ raw date để backend đối chiếu day-first | P1 hidden defect |
| BH-15 Winmart | PTT lặp vào receipt/reference, Mã CQT không có field riêng | Taxonomy identifier thiếu supporting fields và rule PTT | P1 |
| BH-05/BH-13 | Kẹp tóc/khăn ướt chưa hiện policy finding | Taxonomy personal-item quá hẹp; FACT cuối tuần che lỗi | P1 hidden defect |

BH-04 GS25 không phải model error: ảnh in 04/10/2026 là Chủ nhật nhưng ground truth đã khóa AUTO. Việc sửa nhãn chỉ nằm trong adjudicated view.

## 4. Hardening hậu holdout

Evidence contract v2 bổ sung:

- `evidenceContractVersion=2`; response thiếu version bị xem là schema mismatch an toàn;
- `totalAmountSource` và `totalAmountEvidence`; chỉ `PRINTED_FINAL_TOTAL` có exact row khớp số mới đủ điều kiện đi tiếp;
- `NOT_VISIBLE`, `AMBIGUOUS`, `INFERRED`, evidence thiếu/sai label/sai số đều repair tối đa một lần, sau đó FACT, thêm missing field/warning và hạ confidence tối đa 0,69;
- raw `invoiceDateEvidence`/`transactionDateEvidence`; backend chuẩn hóa day-first và phát hiện model đổi tháng/ngày;
- `HH:mm:ss` và 12-hour AM/PM canonicalize lossless về `HH:mm` trước policy;
- VAT đã gồm có thể giữ phần lẻ hỗ trợ, nhưng cash/change/VAT/subtotal không phải final total hay discount;
- `taxAuthorityCode`, `invoiceSerial`, `documentNumber`, `posNumber` tách khỏi ba identifier giao dịch; PTT trên phiếu tính tiền là receipt number;
- taxonomy MVP thêm hair clips/hair accessories/wet wipes/skin-care wipes;
- UI hiển thị ngày nguyên văn, nguồn/dòng tổng và identifier hỗ trợ để reviewer thấy AI dựa vào bằng chứng nào.

Checkpoint offline: Release build sạch, 120/120 automated tests pass. Không có thay đổi schema DB vì extracted facts vẫn nằm trong JSON.

## 5. Go/no-go

- Production accuracy claim: **NO-GO**.
- Unattended real-user pilot: **NO-GO**.
- Supervised demo: **CONDITIONAL**, chỉ GO sau khi publish đúng commit hậu holdout và live regression chứng minh Circle K fail-safe, Ministop POLICY, Katinat day-first, CoopSmile money role và Winmart identifier.
- Ba user session sau đó phải có giám sát; feedback không được tự động đổi policy tài chính.

## 6. Live acceptance sau publish

1. `/healthz` HTTP 200, status OK, DB/storage true, pending migration 0.
2. Official Verify vẫn 5/5 theo 3 AUTO + 1 FACT + 1 POLICY.
3. Một receipt có total in rõ trả `PRINTED_FINAL_TOTAL` + exact evidence row.
4. Circle K hoặc fixture tương đương có giá trị Total bị cắt trả FACT dù subtotal/items cộng khớp.
5. Ministop hoặc fixture tương đương `09:46:21` + bia trả POLICY, không FACT vì format giờ.
6. `04-10-26` canonicalize thành `2026-10-04`; reason weekend nếu ngày là Chủ nhật.
7. CoopSmile-style cash/change/included VAT không bị nâng thành discount/total.
8. PTT và Mã CQT được lưu ở hai field đúng, không duplicate semantic issue.
9. Refresh/recycle giữ request, facts, status và audit.

Mọi lượt dùng lại 15 ảnh phải ghi rõ `post-holdout regression`; không cập nhật official raw metrics.
