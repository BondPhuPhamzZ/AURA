# Paper Receipt Line-item Safety Guard

Cập nhật: 02/10/2026

## Phát hiện từ hóa đơn Phê La

Ảnh thật có thể đọc rõ merchant `Phê La`, số hóa đơn `020056`, ngày `02/10/2026` và tổng `69.000 VND`, nhưng toàn bộ vùng mô tả món hàng bị che đen. Build trước guard trả `AUTO_APPROVE` trong 4.895 ms với `lineItems=[]`, `missingFields=[]`, `warnings=[]`, confidence `0.95` và reason khẳng định không có hạng mục cấm.

Đây không phải bằng chứng AUTO_APPROVE thành công. Đây là **unsafe fail-open**: hệ thống không có dữ kiện để biết món bị che có thuộc danh mục cấm hay không. JSON trước sửa được lưu riêng là `final-before-lineitem-guard.json`; không trộn nó vào accuracy sau sửa.

## Thay đổi kỹ thuật

1. Prompt/contract yêu cầu ít nhất một hàng hóa hoặc dịch vụ đọc được trước khi xét tự duyệt hóa đơn giấy.
2. `ReceiptSemanticValidator` tạo semantic issue khi `VAT_INVOICE`, `RETAIL_RECEIPT` hoặc `RESTAURANT_BILL` có `lineItems=[]`.
3. Provider được đọc lại đúng một lần với chỉ dẫn không suy đoán vùng bị che/mờ/cắt.
4. Nếu vẫn rỗng, backend thêm `lineItems` vào `missingFields`, thêm warning cần người kiểm tra, giới hạn confidence ở 0.69 và chuyển `ESCALATE_FACT`.
5. `PolicyDecisionEngine` có guard thứ hai để direct call không thể AUTO khi danh sách hàng hóa trống.

## Cổng tái kiểm thử bắt buộc

Sau khi dừng app cũ và chạy build mới:

1. Lưu commit SHA, `/healthz` và full preflight.
2. Upload lại đúng ảnh Phê La với claimed amount `69000` đúng một lần.
3. Kỳ vọng `ESCALATE_FACT`, không phải `AUTO_APPROVE`.
4. Final JSON phải có `lineItems=[]`, `missingFields` chứa `lineItems`, warning giải thích không đọc được hàng hóa, `semanticRepairApplied=true`, repair issue nhắc `lineItems`, `confidence<=0.69`, provider/error/latency rõ ràng.
5. Chụp UI cuối, audit và Network `202` + final status. Không cần ba lần cho negative deterministic này; một lượt đủ để chứng minh guard, sau đó chạy Verify 5 để kiểm regression.

Nếu model đọc ra tên món từ vùng thật sự nhìn thấy được ở một ảnh khác, `lineItems` không rỗng và policy có thể tiếp tục. Guard không ép mọi receipt phải FACT; nó chỉ chặn kết luận không có mặt hàng cấm khi evidence món hàng hoàn toàn vắng mặt.

## Phạm vi evidence

Evidence hiện có chứng minh upload `202`, queue hoàn tất, provider OpenRouter, audit, latency và việc mô hình từng fail-open. Nó chưa chứng minh guard live cho tới khi app được restart bằng source mới và ca Phê La được chạy lại. Automated test 102/102 bảo vệ code path nhưng không thay thế lượt provider thật này.
