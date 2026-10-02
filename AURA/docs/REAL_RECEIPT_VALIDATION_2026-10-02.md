# Real-receipt development validation — 02/10/2026

## Phạm vi

Đợt này bổ sung hai ảnh dương mới và chạy lại một ảnh âm bị che vùng món. Đây là development validation trên dữ liệu thật có quyền sử dụng, không phải blind holdout: ảnh đã được xem và dùng để phân tích sau khi chạy nên không được cộng vào accuracy production.

Preflight chung chạy trước các ca lúc 21:43–21:44 ngày 02/10/2026:

- trước app: `READY: 0 failures, 1 warning` do chủ động `-SkipHttp`;
- khi app chạy: `READY: 0 failures, 0 warnings`;
- health `status=ok`, database reachable/up-to-date, OpenRouter/Qwen3-VL-8B configured, fallback tắt.

Evidence cục bộ nằm ngoài Git tại `D:\aura\demo_evidence\07_new_real_receipt_2_10`. Ba thư mục đã có ảnh gốc, ảnh UI/F12, snapshot HTTP 202 và final JSON xuất trực tiếp bằng truy vấn SELECT theo case ID.

## Kết quả

| Ca | Ground truth nghiệp vụ | Kết quả | Repair | Latency | Kiểm tra facts cuối |
|---|---|---|---|---:|---|
| Oppa | Hóa đơn hợp lệ, tổng cuối 68.000 sau giảm 20.000 | `AUTO_APPROVE` | Có, 1 lần | 13.899 ms | subtotal 88.000, discount 20.000, total 68.000, 6 dòng, 0 validation issue |
| Texas | Hóa đơn hợp lệ, thanh toán 199.000 | `AUTO_APPROVE` | Không | 6.391 ms | transaction reference ZaloPay, total 199.000, 6 dòng, 0 validation issue |
| Phê La | Vùng hàng hóa bị che; không thể kiểm policy | `ESCALATE_FACT` | Có, 1 lần | 20.734 ms | total 69.000, lineItems rỗng, missing `lineItems`, confidence 0,69, còn đúng 1 validation issue |

`SemanticRepairApplied=true` không phải fail. Oppa pass vì repair giải quyết được mâu thuẫn và facts cuối còn 0 issue. Phê La pass theo nghĩa fail-safe vì sau repair vẫn thiếu dữ kiện và policy không auto-approve.

## Điểm cần lưu ý

1. Oppa ban đầu có tổng dòng hàng 78.000 nhưng subtotal in 88.000. Repair bổ sung đúng phần còn thiếu: sáu amount cuối cộng thành 88.000, discount 20.000 và total 68.000 nên validation issue về 0. `SemanticRepairIssues` vẫn giữ mâu thuẫn ban đầu để truy vết; đó không phải lỗi còn tồn tại ở facts cuối.
2. Phê La có một warning do model tự viết rằng ngày 02/10/2026 nằm ngoài 90 ngày, trái với ngày chạy 02/10/2026. Deterministic policy tự tính ngày nên không dùng nhận định sai này để quyết định; lý do cuối chỉ dựa vào line items bị che. Đây là lỗi prose không làm sai nhánh quyết định, nhưng cho thấy warning của model không phải nguồn sự thật.
3. Texas còn QR và transaction reference thanh toán. Không commit ảnh, không đưa ảnh gốc vào slide/public repo; cần che QR/reference nếu trình chiếu công khai.
4. Oppa và Phê La đã che dữ liệu khách hàng. Vẫn giữ toàn bộ ảnh thật ngoài Git và chỉ chia sẻ khi chủ sở hữu đồng ý.

## Đánh giá readiness

Đợt test củng cố ba hành vi quan trọng: positive không repair, positive có repair, và negative còn mâu thuẫn sau repair. Cùng với Verify 5/5, restart persistence, human audit và concurrent smoke 5/5, core MVP đủ điều kiện để freeze và diễn tập local.

Chưa đủ để gọi production-ready hoặc “ổn định tuyệt đối”. Cổng còn mở:

- blind holdout 10–15 ảnh đa merchant, khóa ground truth trước khi chạy;
- ít nhất ba người dùng thực tế;
- ba dress rehearsal cùng commit và fallback tắt;
- Live URL smoke nếu đưa URL vào phần trình bày;
- authentication/RBAC, object storage/retention và privacy controls trước production pilot.

## Follow-up không chặn demo

- Backend có thể loại hoặc gắn nhãn các warning mang tính quyết định policy do model tự sinh, ví dụ nhận xét “quá 90 ngày”, vì ngày hợp lệ đã được C# tính tất định. Chỉ triển khai sau khi có test hồi quy để không xóa warning thị giác thật như mờ/cắt/sửa.
- Bổ sung ground-truth sheet cho receipt thật: case ID, hash ảnh, claimed amount, expected decision, expected critical fields, consent/redaction status và lý do nhãn.
