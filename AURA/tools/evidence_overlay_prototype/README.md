# Prototype khoanh vùng evidence chỉ dùng dữ liệu tổng hợp

Đây là prototype độc lập, không có dependency, dùng để thử nghiệm evidence localization hướng tới reviewer của AURA. Prototype **không được nối vào extraction, policy, database hoặc route ảnh hóa đơn live**.

## Prototype chứng minh được gì

- Ảnh nguồn được hiển thị nguyên vẹn; vùng evidence được vẽ bằng SVG overlay.
- Tọa độ vùng dùng không gian chuẩn hóa `0..1000` và vẫn thẳng hàng khi ảnh đổi kích thước.
- Reviewer có thể chọn field hoặc vùng tương ứng theo cả hai chiều và dùng bàn phím.
- Vùng không chắc chắn hoặc thiếu được ghi là `Không định vị được`; UI không tự bịa bounding box.
- Hóa đơn tổng hợp có cả subtotal và final payable; vùng `totalAmount` chỉ trỏ tới dòng cuối cần thanh toán.
- Có thể tắt overlay mà không thay đổi extracted fact hoặc quyết định.

## Chạy tại máy local

Mở trực tiếp `index.html`. Để QA trên loopback URL, chạy `node serve.mjs` rồi mở `http://127.0.0.1:8765/`. Không cần network bên ngoài hoặc cài package.

## Những gì prototype không tuyên bố

Prototype không claim độ chính xác OCR hoặc localization. Tích hợp production vẫn cần word-level OCR box, ghép evidence text tất định, ràng buộc image hash, authorization cho route ảnh, tập development có annotation và một tập unseen khóa độc lập để đánh giá. Xem `docs/EVIDENCE_LOCALIZATION_PLAN.md`.
