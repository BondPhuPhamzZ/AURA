# AURA — Ma trận 15 ca blind holdout

Cập nhật: 05/10/2026. Tài liệu này là checklist chọn **candidate**. Ground truth chính thức vẫn phải nằm trong manifest private và được khóa trước request đầu tiên.

## 1. Nguyên tắc không được phá vỡ

- Không sửa nội dung hóa đơn để ép nó vào một nhánh. Hãy chọn chứng từ tự nhiên phù hợp; nếu thiếu coverage thì thu thập thêm hoặc dùng synthetic và khai báo đúng `sourceType`.
- Chỉ che PII/QR nhạy cảm. Không che merchant, ngày, giờ, identifier, item, subtotal/discount/tax/total mà expected facts cần dùng.
- Một ảnh đã gửi AURA/OpenRouter/Ollama để test, tune prompt/policy/semantic repair không còn blind, kể cả khi đổi tên, crop, che QR hoặc nén lại; chuyển nó sang regression development.
- Cùng merchant vẫn được phép. Điều kiện là **giao dịch khác**, file cuối chưa từng được model xử lý và không dùng để phát triển rule. Nên giới hạn tối đa 2 ca/merchant để tránh tập bị lệch một chuỗi cửa hàng.
- Mỗi ca ưu tiên chỉ có một nguyên nhân chính. Do precedence `FACT > POLICY > AUTHORITY`, một hóa đơn vừa thiếu dữ kiện vừa có bia sẽ phải kỳ vọng `ESCALATE_FACT`, không phải `ESCALATE_POLICY`.
- Ngày hợp lệ tại thời điểm chạy phải là ngày trong vòng 90 ngày, không ở tương lai và không rơi vào thứ Bảy/Chủ nhật. Nếu có giờ thì phải nằm trong 06:00–22:00, trừ ca FACT chủ đích.
- `claimedAmount` thông thường là số cuối cùng thực phải trả sau giảm giá. Không dùng subtotal, tiền trước giảm, tiền khách đưa hoặc tiền thối.

## 2. Ma trận chính thức đề xuất

| ID | Nhánh kỳ vọng | Chứng từ cần chọn và format nhìn thấy | Claimed amount và điều kiện cô lập |
|---|---|---|---|
| BH-01 | `AUTO_APPROVE` | Hóa đơn bán lẻ rõ; có merchant, ngày làm việc, VND, ít nhất 1 item, tổng cuối, `Receipt No`/`Bill No`/số biên nhận; ảnh đủ 4 góc hoặc toàn vùng nội dung | Bằng đúng `totalAmount`; tổng `<= 1.000.000`; không item cấm |
| BH-02 | `AUTO_APPROVE` | Hóa đơn có giảm giá rõ: subtotal/giá trước giảm, dòng voucher/discount và `Thành tiền`/`Tổng thanh toán` cuối cùng đều đọc được | Bằng số cuối phải trả sau giảm; phép tính phải reconcile |
| BH-03 | `AUTO_APPROVE` | Bill nhà hàng/cà phê rõ; có `Check`, `Transaction No`, `Trace`, `RRN` hoặc mã giao dịch riêng từng giao dịch. Table/POS/ShopID/TID/MID đơn lẻ không đủ | Bằng tổng cuối; ngày/giờ hợp lệ; không alcohol/item cấm |
| BH-04 | `AUTO_APPROVE` | Hóa đơn GTGT đã phát hành; có seller, **MST người bán**, số hóa đơn, ngày hóa đơn, item, VND, subtotal/tax/total nếu được in | Bằng tổng cuối; `<= 1.000.000`; không dùng MST người mua làm seller tax ID |
| BH-05 | `AUTO_APPROVE` | Layout/merchant khác 4 ca trên. Có thể là e-commerce/ride screenshot đã hoàn tất/đã thanh toán, có mã đơn/chuyến/receipt và ngày giao dịch; hoặc receipt giấy có identifier hợp lệ | Bằng tổng cuối; nếu digital phải có trạng thái completed/paid và không chỉ có ngày giao hàng |
| BH-06 | `ESCALATE_FACT` | Receipt rõ nhưng **tự nhiên không có identifier riêng của giao dịch**; chỉ có ShopID/POS/Register/Table/MID/TID hoặc không có mã | Bằng tổng cuối; mọi fact khác hợp lệ để cô lập lỗi identifier. Không che một Bill No có thật để tạo ca |
| BH-07 | `ESCALATE_FACT` | Receipt hoàn toàn rõ, hợp lệ, có identifier và item; dùng một giao dịch riêng chưa xuất hiện ở ca khác | Nhập một `claimedAmount` cố ý khác `totalAmount`; khóa cả số khai sai và total thật trong manifest |
| BH-08 | `ESCALATE_FACT` | Ảnh thật bị crop/mờ/chói/che khiến danh sách item không đọc được, trong khi merchant/identifier/tổng còn thấy. Tốt nhất dùng giao dịch riêng | Bằng tổng thấy được; khai báo `sourceType`/biến đổi trung thực; không dùng cùng giao dịch với ca AUTO |
| BH-09 | `ESCALATE_FACT` | Receipt rõ có **một** lỗi thời gian nghiệp vụ tự nhiên: cuối tuần, quá 90 ngày, tương lai, hoặc giờ ngoài 06:00–22:00 | Bằng tổng cuối; ưu tiên `Sunday_Basic.jpg` nếu các field khác đầy đủ và ảnh chưa từng gửi model |
| BH-10 | `ESCALATE_POLICY` | Receipt rõ có bia/rượu/wine/alcohol; merchant, identifier, ngày, item và total đều đáng tin cậy | Bằng tổng cuối; nên `<= 1.000.000` để không trộn AUTHORITY |
| BH-11 | `ESCALATE_POLICY` | Receipt rõ có thuốc lá/cigarette/tobacco hoặc item được policy hiện hành gọi rõ là đồ cá nhân | Bằng tổng cuối; mọi FACT gate phải pass |
| BH-12 | `ESCALATE_POLICY` | Receipt rõ cho karaoke/cinema/massage/entertainment; dịch vụ phải xuất hiện trong item, không suy đoán từ logo | Bằng tổng cuối; mọi FACT gate phải pass |
| BH-13 | `ESCALATE_AUTHORITY` | Receipt hợp lệ, item bình thường, identifier rõ, tổng **trên 1.000.000 VND**; merchant/layout khác | Bằng tổng cuối; không item cấm; ngày/giờ hợp lệ |
| BH-14 | `ESCALATE_AUTHORITY` | Hóa đơn GTGT hoặc receipt thứ hai trên 1 triệu, khác merchant/layout BH-13; nếu GTGT phải đủ seller tax ID + invoice no | Bằng tổng cuối; không lỗi FACT/POLICY |
| BH-15 | `ESCALATE_AUTHORITY` | Receipt trên 1 triệu có giảm giá/thuế/phí nhưng final payable rõ, dùng để kiểm tra authority sau reconciliation | Bằng final payable sau discount và phí; không item cấm; mọi FACT gate pass |

Nếu ba nhánh POLICY hoặc AUTHORITY không thể thu thập an toàn bằng hóa đơn thật, dùng fixture synthetic đã khai báo rõ thay vì sửa một hóa đơn thật. Ma trận 5/4/3/3 là coverage mục tiêu, không phải lý do để gán sai nhãn.

## 3. Cách chốt 15 candidate hiện tại

1. Giữ nguyên 15 file đã chốt trong `02_selected_images`; chưa upload file nào vào AURA.
2. Với từng file, con người mở offline và điền bảng nháp: merchant, ngày/giờ, document type, identifier và nhãn của identifier, item, subtotal, discount, tax, final total, PII đã che, quyền sử dụng, đã từng gửi model hay chưa.
3. Đánh dấu cell BH-01…BH-15 mà file **tự nhiên** thỏa mãn. Một file chỉ được chọn cho một cell.
4. Nếu nhiều file cùng phù hợp, ưu tiên merchant/layout/điều kiện chụp khác nhau. Không bẻ nhãn để ép phân bố; nhánh hiếm có thể được báo cáo bằng synthetic supplement tách biệt.
5. Khi đủ 15 cell hợp lệ, đổi tên ổn định, hoàn tất redaction, rồi mới tạo SHA-256 và manifest locked.

`NewVinamilk.jpg` và `NewKatinat.jpg` được xem là candidate holdout nếu đây là giao dịch mới, ảnh cuối chưa từng gửi bất kỳ model/pipeline nào và chưa dùng output của chúng để sửa hệ thống. Việc trùng merchant với regression cũ không tự làm mất tính blind. Nếu chính hai giao dịch này đã từng được upload thử, chúng phải chuyển sang regression dù ảnh hiện tại đã che QR hoặc đổi hash.

## 4. Cổng trước official run

- đủ đúng 15 file, mỗi file có quyền sử dụng và redaction review;
- hai người khóa `claimedAmount`, `expectedStatus`, `expectedFacts`, rationale và SHA-256 trước request;
- code/prompt/policy/model/provider/fallback được freeze và ghi revision;
- live `/healthz` là `ok`, DB up-to-date, storage available, OpenRouter đúng model, fallback tắt;
- runner preflight xác minh 15/15 hash rồi mới gửi request;
- chỉ chạy một official run và giữ mọi mismatch/system error.

Holdout 15 ca chỉ là bằng chứng MVP có kiểm soát; không đủ để tuyên bố production accuracy.
