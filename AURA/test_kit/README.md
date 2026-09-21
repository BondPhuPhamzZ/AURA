# AURA Test Kit v2

## Hai gói bàn giao cho BGK

- **5 ca chạy được bằng một nút:** `wwwroot/test_data/expected-results.json` và ảnh tại `wwwroot/test_data/images`. Verify Harness gọi đúng production path, gồm 3 ca tự duyệt và 2 ca chuyển tiếp.
- **15 ca tham chiếu đã commit:** `judge-manifest.json` chọn đúng 15 ảnh trong `images`, cân bằng 5 routine, 4 FACT, 3 POLICY và 3 AUTHORITY. Gói này giúp BGK đọc expected result và tạo biến thể mà không tự động tiêu credit.

`manifest.json` vẫn giữ ngân hàng mở rộng 30 ca để nhóm benchmark nội bộ; đây không phải yêu cầu bắt buộc phải chạy toàn bộ trong buổi chấm.

## Ảnh thật nên đặt ở đâu?

Không thay trực tiếp năm fixture Verify và không bỏ ảnh cá nhân vào root/wwwroot. Bộ chấm cần kết quả tái lập nên 5 + 15 ca đã commit vẫn là dữ liệu tổng hợp có ground truth. Nếu cần đánh giá độ chân thực, đặt bản đã được phép sử dụng và che dữ liệu cá nhân trong `local_real/`, sau đó upload từng ảnh qua luồng kiểm thử lẻ. Git đã được cấu hình để không commit các ảnh riêng tư trong thư mục này.

Phương án thuyết phục nhất khi trình bày là: **5 ca tái lập chạy live + 15 ca challenge đã commit + một vài ảnh thật đã ẩn danh để smoke test thủ công**. Không lấy ảnh ngẫu nhiên trên Internet hoặc commit hóa đơn có dữ liệu cá nhân chỉ để giao diện trông thật hơn.

Test Kit v2 gồm 30 ảnh tổng hợp có seed cố định và ground truth trong `manifest.json`. Bộ này được tạo offline, không chứa dữ liệu cá nhân thật và không gọi OpenRouter/Qwen.

## Phạm vi

- 8 ca thường quy dự kiến `AUTO_APPROVE`.
- 11 ca thiếu/sai/không đáng tin cậy dự kiến `ESCALATE_FACT`.
- 6 ca có hạng mục ngoài chính sách dự kiến `ESCALATE_POLICY`.
- 5 ca vượt hạn mức dự kiến `ESCALATE_AUTHORITY`.
- Nhiều layout: mobile e-commerce, giấy in nhiệt/POS, VAT, nhà hàng và ride-hailing.
- Có tiếng Việt Unicode, góc xoay, nền camera, blur, crop, ngày cũ/tương lai/cuối tuần, amount mismatch, draft và returned/refunded.

Năm ca `TK-01..TK-05` được sao chép thành fixture của Verify Harness tại `wwwroot/test_data/images`. Các ca còn lại không tự động gọi API để bảo vệ credit.

## Tái tạo

```powershell
<python-with-pillow> tools/generate_verify_receipts.py --as-of-date 2026-09-21
```

Phải dùng cùng `--as-of-date` và seed trong manifest để tái tạo byte/layout ổn định. Khi thay ngày, fixture và SHA-256 sẽ thay đổi; phải commit manifest cùng ảnh.

## Quy tắc benchmark

1. Unit test policy trước; không cần API.
2. Chạy đúng một ảnh smoke test sau khi deploy.
3. Nếu ổn và quota còn đủ, chạy một lượt Verify 5 ảnh.
4. Chỉ chạy 30 ảnh khi có ngân sách benchmark riêng; ghi model, thời điểm, expected/actual, latency và lỗi.
5. Không sửa expected để hợp thức hóa output của model.
6. Kết quả trên dữ liệu tổng hợp không được công bố là accuracy trên hóa đơn thực tế.

Ảnh bên ngoài dùng làm cảm hứng bố cục không được sao chép vào repository nếu có dữ liệu cá nhân, watermark hoặc chưa rõ quyền sử dụng.
