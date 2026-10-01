# Semantic date repair cho chứng từ giấy

Ngày triển khai: 01/10/2026  
Phạm vi: OpenRouter và Ollama dùng chung `ReceiptExtractionContract`/`ReceiptSemanticValidator`

## 1. Sự cố đã tái hiện

Cùng ảnh Highlands Coffee, cùng claim 59.000 VND và cùng model OpenRouter cho hai output khác vị trí trường ngày:

| Lượt | Case ID | `invoiceDate` | `transactionDate` | AI decision |
|---|---|---|---|---|
| 1 | `e86ec3873b4248019b956363176b86f9` | `null` | `2026-09-29` | `ESCALATE_FACT` |
| 2 | `bfa51102429e4b1890aba65c8a36e249` | `2026-09-29` | `2026-09-29` | `AUTO_APPROVE` |

Policy không ngẫu nhiên: `RETAIL_RECEIPT` là chứng từ giấy nên `PolicyDecisionEngine` dùng `invoiceDate`. UI cũ lại hiển thị giá trị đầu tiên trong `invoiceDate || transactionDate || completionDate`, khiến lượt 1 nhìn như đã có ngày dù policy không nhận ngày hóa đơn hợp lệ.

## 2. Invariant sau sửa

1. `VAT_INVOICE`, `RETAIL_RECEIPT`, `RESTAURANT_BILL`:
   - ngày hóa đơn/biên nhận chỉ nằm ở `invoiceDate`;
   - `transactionDate` dành cho chứng từ số;
   - `completionDate` chỉ là bằng chứng hỗ trợ, không thay ngày hóa đơn.
2. Paper facts có `invoiceDate=null` nhưng có `transactionDate`/`completionDate` là semantic conflict và phải đọc lại ảnh đúng một lần.
3. Nếu `invoiceDate` và `transactionDate` giống hệt nhau trên paper facts, backend giữ `invoiceDate` và xóa bản sao `transactionDate` mà không gọi repair; thao tác này không tạo bằng chứng mới.
4. Nếu hai ngày khác nhau, model phải đọc lại nhãn. Backend không tự chọn một ngày.
5. Repair không nhận claimed amount, expected result hoặc tên fixture.
6. Repair lỗi hoặc output vẫn mâu thuẫn luôn dẫn tới `ESCALATE_FACT`, không đổi provider và không repair vô hạn.

## 3. Luồng thực thi

```text
Model response đúng JSON Schema
        |
        v
Normalize duplicate paper fields (lossless only)
        |
        v
ReceiptSemanticValidator.Validate
        |
        +-- không có issue --> PolicyDecisionEngine
        |
        +-- có issue --> đọc lại cùng ảnh đúng 1 lần với issue cụ thể
                              |
                              +-- hết issue --> PolicyDecisionEngine
                              |
                              +-- còn issue/lỗi --> ValidationIssues --> ESCALATE_FACT
```

`SemanticRepairApplied` và `SemanticRepairIssues` là metadata backend-only. Chúng không nằm trong JSON Schema gửi model, nhưng được lưu cùng `ExtractedFactsJson`, trả về status JSON và tóm tắt trong audit bằng `SemanticRepairApplied`/`SemanticRepairIssueCount`.

## 4. Thay đổi UI

- Paper evidence hiển thị `Ngày hóa đơn / biên nhận` và chỉ đọc `invoiceDate`.
- Digital evidence hiển thị `Ngày giao dịch / thanh toán` và ưu tiên `transactionDate` theo cùng logic policy.
- `completionDate`, nếu có, hiển thị riêng là `Ngày hoàn tất (tham khảo)`.
- `ValidationIssues` được hiển thị trong khối cảnh báo, không còn bị ẩn sau một ngày fallback.

UI chỉ giải thích dữ kiện; quyền quyết định vẫn thuộc `PolicyDecisionEngine`.

## 5. Regression offline

- OpenRouter adapter: output paper date sai trường kích hoạt đúng một repair; output sửa có `invoiceDate=2026-09-29`, `transactionDate=null`, có repair metadata và không còn `ValidationIssues`.
- Ollama adapter: cùng contract và cùng kết quả kiểm soát.
- Validator: phát hiện paper date ở trường số, phát hiện hai ngày khác nhau và canonicalize bản sao giống hệt.
- Release build: 0 warning, 0 error.
- Automated tests tại checkpoint sửa ngày: 93/93 pass. Baseline hiện tại sau hardening giảm giá: 100/100 pass.
- EF model check: không có thay đổi model chưa migration.

## 6. Cổng live re-validation

Không dùng hai lượt cũ để công bố fix đã pass. Trên commit chứa thay đổi này, upload lại cùng ảnh ba lần và lưu raw status JSON.

Chỉ đạt khi cả ba lượt đồng thời có:

- `actual=AUTO_APPROVE`;
- `facts.invoiceDate=2026-09-29`;
- `facts.transactionReference=221196`;
- `facts.totalAmount=59000`;
- `facts.validationIssues=[]`;
- `providerErrorCode` rỗng;
- mỗi request hoàn tất dưới 90 giây.

`semanticRepairApplied` có thể `true` hoặc `false`: `true` nghĩa là first pass sai semantic nhưng repair đã giải quyết. Chỉ số này phải được ghi nhận để phân biệt first-pass stability với final-decision stability.

Sau ba lượt Highlands, chạy một Official Verify 5-case smoke. Khi commit được freeze, chạy lại ba batch chính thức để evidence và source cùng một SHA.

## 7. Giới hạn còn lại

Semantic repair làm cho policy nhất quán trước cùng một contract, nhưng không thể biến VLM thành hệ thống tuyệt đối tất định. Output ảnh mờ hoặc nhãn ngày thực sự không rõ vẫn có thể khác nhau; kết quả đúng trong trường hợp đó là escalation ổn định, không phải ép `AUTO_APPROVE`. Accuracy tổng quát vẫn cần holdout độc lập và ground truth khóa trước.
