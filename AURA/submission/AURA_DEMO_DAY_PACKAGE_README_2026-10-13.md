# AURA Demo Day — đọc trước

**Trạng thái đồng bộ:** 14/10/2026. Các tệp mang ngày 06/10, 07/10, 12/10 hoặc 13/10 trong thư mục Evidence là bằng chứng lịch sử và được giữ nguyên ngày nguồn; trạng thái hiện hành nằm trong `CURRENT_PROJECT_STATUS_2026-10-14.md`.

## Những gì đã sẵn sàng

- proposal tiếng Việt và tiếng Anh ở định dạng DOCX chỉnh sửa được và PDF đã kiểm tra trực quan;
- đúng năm slide thuyết trình trong PPTX;
- Build Log một trang ở DOCX và PDF;
- repository công khai, live URL, health endpoint, Verify cases, workflow spec, runbook và chỉ mục evidence;
- kịch bản video ba phút chỉ quay sản phẩm;
- kịch bản thuyết trình tối đa 15 phút và bộ hỏi đáp ngắn gọn;
- gói evidence đã tuyển chọn, không chứa ảnh hóa đơn gốc, API key, database riêng tư hoặc ảnh billing.

## Việc con người còn phải hoàn tất

1. Quay và tải lên video MP4 chính thức không quá 180 giây.
2. Hoàn tất ba phiên người dùng thật có đồng thuận; ghi tên, vai trò, feedback nguyên văn đã xác nhận, một tác động tiêu cực và một thay đổi sản phẩm bắt nguồn từ feedback.
3. Thay đường dẫn video đang chờ và xác nhận release ref trong tài liệu link nộp.
4. Xác nhận hạn nộp mới nhất và quyền xem Team Drive bằng cửa sổ ẩn danh.

## Câu chuyện thuyết trình

Câu chuyện không phải “model đọc được mọi hóa đơn”. Bước ngoặt là một ca missed escalation: JSON hợp schema nhưng sai ngữ nghĩa. Từ đó AURA tách định danh, ngày gốc, tổng tiền in trên hóa đơn, validation và policy tất định. Kết quả là một hệ thống có giám sát, biết dừng để hỏi con người, đồng thời giữ Audit và Undo.

## Số liệu chính thức được phép dùng

- Release build: 0 warning, 0 error.
- Offline tests: 163/163 đạt.
- Live Verify: 5/5 trong 65,004 giây.
- Mini-holdout tiến cứu 10 hóa đơn: 8/10, không missed, hai ca over-escalation fail-safe, không quan sát thấy system/provider/429 error.
- Lặp tuần tự trên dữ liệu đã biết: 11/12, không missed, một ca over; chỉ là evidence ổn định.
- Năm upload khác nhau đồng thời: 5/5 hoàn tất; chỉ chứng minh tính toàn vẹn của hàng đợi, không phải latency gate của Verify hoặc production SLA.

## Không được tuyên bố

- độ chính xác production hoặc khả năng vận hành không giám sát;
- 163 test tương đương 163 hóa đơn;
- lượt lặp 11/12 là holdout mới;
- evidence highlighting production, distributed provider limiting, RBAC production, PDF an toàn, quét malware hoặc object storage;
- evidence ba người dùng đã hoàn tất khi chưa có biên bản có xác nhận.

## Bản đồ thư mục

- `Evidence`: evidence kỹ thuật/đánh giá đã tuyển chọn và manifest SHA-256.
- `Gia_Phu/OnTap`: bộ ôn tập tiếng Việt hiện hành về flow mã nguồn, kiến trúc, nghiệp vụ, provider, capacity, holdout và vận hành tại chỗ.
- `Gia_Phu/Pitching_QnA`: kịch bản pitch, câu hỏi BGK có thể hỏi và mẫu trả lời ngắn gọn.
- `kich_ban_video`: kịch bản chỉ quay sản phẩm và cổng kiểm tra video đang chờ.
- `kich_ban_thuyettrinh_slide`: slide và kịch bản nói tối đa 15 phút.
- `Nop`: cấu trúc sẵn sàng upload; hạng mục con người chưa hoàn tất được ghi rõ thay vì bịa evidence.
