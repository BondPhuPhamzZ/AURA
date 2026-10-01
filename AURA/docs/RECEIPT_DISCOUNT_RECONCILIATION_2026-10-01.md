# Receipt Discount Reconciliation

Cập nhật: 01/10/2026

## Kết luận

Ca Vinamilk không phải lỗi đọc chữ hoặc đọc số. Kết quả trước sửa đã nhận đúng tổng dòng hàng `183.114 VND`, khoản `Giảm giá -2.828 VND` trên ảnh và tổng thanh toán `180.286 VND`, nhưng JSON contract chỉ có `subtotal`, `tax` và `totalAmount`. Vì không có trường giảm giá có cấu trúc, backend nhìn thấy `lineItems != totalAmount`, semantic repair không có chỗ để sửa và policy fail-safe sang `ESCALATE_FACT`.

Contract hiện bổ sung `discountAmount`. Đây là trị tuyệt đối không âm của khoản giảm giá, chiết khấu, voucher hoặc khuyến mãi được in rõ ở cấp toàn hóa đơn. Model không được suy ra trường này chỉ từ chênh lệch số học hoặc số tiền người dùng khai báo.

Postfix live smoke sau đó phát hiện một nhánh khác: Highlands có `sum(lineItems)=subtotal=totalAmount=59000`, nhưng model gán một số `1000` không tác động thành `discountAmount`. Đây không phải giảm giá có thể đối chiếu. Contract hiện loại rõ điểm/tích lũy, số dư điểm, mã hoặc phần trăm voucher, tiền khách đưa, tiền thừa, số lượng và mã terminal/khách hàng. Backend chỉ bỏ giá trị không tác động khi cả ba cách tính tổng độc lập đã bằng nhau và thuế bằng 0; warning vẫn lưu giá trị bị bỏ để audit. Vinamilk `183114 - 2828 = 180286` không thỏa điều kiện bỏ và vẫn được giữ nguyên.

## Quy tắc số học

Backend chỉ chấp nhận một trong các quan hệ có bằng chứng cấu trúc:

```text
subtotal - discountAmount = totalAmount
subtotal + tax - discountAmount = totalAmount
```

Công thức thứ nhất dùng khi subtotal đã gồm thuế. Công thức thứ hai dùng khi thuế được cộng sau subtotal. `lineItems` phải đối chiếu được với subtotal; nếu subtotal có nhưng không khớp tổng dòng hàng thì đó là semantic issue. Chỉ khi subtotal không được in rõ, tổng line item mới trở thành số tiền nền để kiểm tra. Với VND, `discountAmount` phải là số nguyên đồng.

Các trường hợp sau tạo `ValidationIssues` và đi qua tối đa một semantic repair:

- thiếu `discountAmount` trong khi tổng dòng hàng không khớp tổng thanh toán;
- `discountAmount` âm, có phần thập phân với VND hoặc lớn hơn giá trị trước giảm;
- phép tính có `discountAmount` vẫn không ra `totalAmount`;
- chỉ ghi từ khóa giảm giá trong `warnings` nhưng không có số tiền cấu trúc.

Nếu repair vẫn không giải quyết được, policy giữ `ESCALATE_FACT`. Việc thêm từ khóa “voucher” vào cảnh báo không còn bỏ qua kiểm tra số học.

## Quyền quyết định

`totalAmount` tiếp tục là số tiền cuối cùng khách phải trả và là số được so với khoản nhân viên khai báo. Với ca Vinamilk:

- khai `180.286 VND`: có thể `AUTO_APPROVE` nếu mọi dữ kiện khác đều hợp lệ;
- khai `183.114 VND`: phải `ESCALATE_FACT` vì đây là số trước giảm giá;
- ảnh không đọc rõ dòng giảm hoặc tổng thanh toán: phải `ESCALATE_FACT`, không tự suy ra.

Qwen chỉ trích xuất dữ kiện. `ReceiptSemanticValidator` kiểm phép tính và `PolicyDecisionEngine` mới ra quyết định. Cách tách này áp dụng chung cho OpenRouter và Ollama.

## Thay đổi kỹ thuật

- `ReceiptExtractionDto`: thêm `DiscountAmount` nullable.
- JSON Schema và prompt chung: bắt buộc trả khóa `discountAmount`, giá trị có thể null.
- Semantic validator: đối chiếu giảm giá có cấu trúc và loại bỏ đường tắt dựa trên keyword trong warnings.
- Semantic repair: hướng dẫn đọc lại khoản giảm được in rõ, không nhận claimed amount.
- UI: hiển thị tạm tính, giảm giá hoặc chiết khấu, thuế và tổng thanh toán theo từng dòng.
- Business rules: thống nhất cách hiểu subtotal, receipt-level discount và final payable.
- Canonicalizer: loại một `discountAmount` không có base trước giảm riêng chỉ khi `lineItems = subtotal = totalAmount` và tax bằng 0; không suy ra hoặc sửa `totalAmount`.

Không cần EF migration. Facts được lưu trong cột JSON hiện có nên JSON cũ không có `discountAmount` vẫn deserialize thành null.

## Kiểm thử offline

Suite hiện có 102 test và không gọi API trả phí. Các regression mới bảo vệ:

- đúng phép tính Vinamilk và quyết định theo tổng sau giảm;
- claimed amount dùng giá trước giảm phải bị từ chối tự động;
- keyword trong warning không được bỏ qua arithmetic;
- giảm giá âm, vượt giá trị nền, sai phép tính hoặc có phần lẻ VND;
- OpenRouter one-repair nhận lỗi thiếu `discountAmount` và trả facts hợp lệ;
- OpenRouter và Ollama đều nhận schema chung có trường mới.
- Highlands-style non-impacting discount bị bỏ có audit warning, còn khoản Vinamilk hợp lệ được giữ.

## Cổng kiểm thử provider thật

Lưu evidence sau cập nhật tại:

```text
D:\aura\benchmark_2times_img\OpenRouter_test\final_validation_2026-10-01\05_discount_reconciliation_postfix\
├── 00_preflight_and_metadata\
├── 01_no_discount_smoke\
├── 02_vinamilk_final_amount_3runs\
├── 03_vinamilk_pre_discount_claim_negative\
├── 04_verify_3batches\
├── 05_restart_persistence\
├── 06_manager_and_audit\
└── 07_summary\
```

Mỗi run lưu ảnh đầu vào đã ẩn danh, claimed amount, ảnh UI cuối, response HTTP 202, `statusUrl`, final JSON, latency, provider/model/fallback, commit SHA và timestamp. Không lưu API key, Authorization header hoặc connection string.

Điều kiện đạt:

1. Highlands không có giảm giá chạy lại ba lượt: `AUTO_APPROVE`, `discountAmount=null`, không có `ValidationIssues`. Warning chuẩn hóa có thể tồn tại nếu model vẫn đề xuất số `1000` không tác động.
2. Vinamilk chạy ba lượt với claimed amount `180286`: cả ba `AUTO_APPROVE`, `discountAmount=2828`, `totalAmount=180286`, không có `ValidationIssues`.
3. Vinamilk chạy một lượt với claimed amount `183114`: `ESCALATE_FACT` vì amount mismatch.
4. Verify Harness chạy ba batch: mỗi batch 5/5 PASS với phân bố 3 AUTO, 1 FACT, 1 POLICY.
5. Một hồ sơ escalation còn đúng ảnh, trạng thái và audit sau restart.
6. Manager accept, reject và undo tạo timeline đúng.

`semanticRepairApplied=false` ở ca Vinamilk là first-pass tốt. `true` vẫn là kết quả hợp lệ nếu repair chỉ chạy một lần, final facts đúng, `ValidationIssues` rỗng và decision đúng. Không dùng riêng cờ này làm tiêu chí pass/fail.

Evidence của commit `29257c3` tại `05_discount_reconciliation_postfix` là incident evidence, chưa phải post-fix pass: hai `statusUrl.txt` được lưu khi còn `PENDING`, và UI cuối cho thấy date-placement false escalation; Highlands còn có non-impacting `discountAmount=1000`. Thực hiện gate mới theo `LIVE_POSTFIX_INCIDENT_2026-10-01.md` trên commit chứa bản sửa này.
