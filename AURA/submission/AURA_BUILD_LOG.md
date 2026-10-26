# Nhật ký xây dựng dự án AURA

> **Cập nhật 26/10/2026 (hậu cuộc thi):** Source local synthetic đã thống nhất
> một profile tại cổng 5000, đọc folder Drive Restricted ở quyền Viewer và lưu
> mỗi ảnh được nhận thành hồ sơ SQL. Bảng nhân viên nay tự cập nhật trạng thái
> chờ/chạy/kết quả bằng `fetch` polling khi tab hiển thị; không cần F5. File Drive
> cũ không đổi được bỏ qua; bản sao file mới có thể được đánh dấu trùng nhưng
> profile repeatability chưa escalate trùng. Release unit suite đạt 378/378;
> xem [status](../docs/CURRENT_PROJECT_STATUS.md) cho kiểm thử integration.
> Strict ZDR chỉ là yêu cầu routing của API, không thay thế che PII hoặc hợp đồng
> lưu trữ dữ liệu. Đoạn dưới giữ nguyên evidence lúc nộp (163 test, Verify 5/5)
> để không gộp sai số liệu giữa hai thời điểm.

**Track A The Escalation Referee | Bản chốt 14/10/2026 | Phạm Gia Phú**

AURA là trợ lý thẩm định tuyến đầu có con người giám sát cho quy trình hoàn ứng. Vision model trích xuất dữ kiện có cấu trúc từ hóa đơn, policy C# tất định quyết định hướng xử lý, còn con người chịu trách nhiệm với ngoại lệ. Hệ thống bao phủ upload, xử lý nền bền vững, duyệt thủ công, Audit và Undo.

## Tôi đã dùng AI như thế nào

Qwen3.8 Flash qua OpenRouter và Alibaba, với reasoning tắt, đọc một ảnh JPG/PNG và trả về hợp đồng JSON nghiêm ngặt. Codex hỗ trợ truy luồng code, thiết kế test, rà soát refactor và đồng bộ tài liệu. Tôi chỉ giữ thay đổi sau khi build, chạy test và kiểm tra evidence. Model không có quyền phê duyệt.

## AI đã giúp ở đâu

- Truy code nhanh đã làm rõ sự khác nhau giữa tín hiệu trong bộ nhớ và hàng đợi công việc bền vững trong SQL.
- Các test được bổ sung bao phủ vai trò trường dữ liệu, số học, evidence ngày tháng, fallback, concurrency, Audit và hợp đồng UI.
- Vision extraction xử lý nhiều bố cục hóa đơn trong khi policy vẫn có thể thay thế và kiểm thử độc lập.

## AI đã làm tốn thời gian hoặc chi phí ở đâu

Tên provider và model slug từng thay đổi; một số route cũ trả 404 hoặc lỗi 429 không ổn định. Output đúng schema vẫn có thể sai nghĩa, chẳng hạn nhầm đơn vị tiền hoặc vai trò mã định danh. Một request bật reasoning đã dùng hết completion budget nhưng không trả JSON dùng được. Các lỗi này buộc dự án phải có chẩn đoán provider, schema nghiêm ngặt, semantic validation, đúng một lần repair, cooldown và ranh giới claim rõ ràng. Khi dữ liệu đã trở thành known data, dự án không chạy lại API trả phí để săn số đẹp.

## Điều gì thay đổi sau thất bại

Holdout hóa đơn thật đầu tiên phát hiện một missed escalation dù response parse thành công. Tôi thay giả định “JSON hợp lệ là đủ” bằng evidence contract v2: mỗi mã định danh có vai trò riêng, ngày tháng giữ chuỗi gốc, khoản giảm giá có trường riêng và số tiền chỉ được tự duyệt khi có dòng tổng cuối in rõ để đối chứng. Bất định chưa giải quyết được chuyển sang FACT review. Thứ tự policy vẫn là `FACT -> POLICY -> AUTHORITY`.

## Bằng chứng đã xác minh

| Gate | Kết quả hiện tại |
|---|---:|
| Release build | 0 warning, 0 error |
| Test offline | 163/163 pass |
| Verify live | 5/5 trong 65,004 giây |
| Mini-holdout prospective | 8/10, 0 missed, 2 over, 0 lỗi system/provider/429 |
| Lặp tuần tự trên known data | 11/12, 0 missed, 1 over; chỉ là evidence ổn định |

Mẫu prospective nhỏ và đã redaction. Kết quả đủ hỗ trợ demo có giám sát, không phải production accuracy. Ba phiên người dùng thực tế vẫn chưa hoàn tất.

## Tính năng lớn nhất đã cắt

Tôi chưa đưa evidence localization vào baseline production. Việc khoanh vùng dữ kiện có thể tăng độ tin cậy của reviewer, nhưng khi chưa có word-level OCR, annotation độc lập và benchmark false-highlight, hệ thống có thể trỏ nhầm số tiền và tạo tự tin sai. Prototype SVG gắn với hash ảnh tổng hợp vẫn được giữ tách biệt với extraction và policy. PDF, production RBAC, malware scanning, object storage và distributed rate limiting cũng nằm trong roadmap.

## Gate tiếp theo

Hoàn tất ba phiên người dùng có đồng ý tham gia, giữ feedback nguyên văn, truy vết một thay đổi tới commit và ghi nhận ít nhất một tác động vận hành tiêu cực. Sau đó mới giảm over-escalation bằng fixture dương/âm độc lập mà không làm yếu guard chống missed escalation.
