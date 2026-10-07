# AURA Manual Final Gates

Cập nhật: 06/10/2026. Technical baseline `1f4b3c9` đã đạt. Tài liệu này chỉ liệt kê phần cần người thật hoặc thiết bị thật trước khi public live URL trong README.

## Trạng thái thực hiện ngày 06/10

- Điện thoại thật 4G/5G: PASS health, Verify 3 AUTO + 2 escalation, workflow, Audit, refresh và mở lại ảnh. Feedback P1 là hai nút quản lý lệch cột; candidate đã sửa và kiểm local 390x844, chờ republish/phone regression.
- Local Ollama 4B: pipeline hoạt động nhưng judge post-policy chỉ 13/15, missed TK-12 và over TK-02; không đủ cơ sở bật fallback mặc định.
- Còn mở: ba user session do BTC/Kelly hỗ trợ và ba rehearsal trên commit freeze.

## 1 Điện thoại thật qua 4G hoặc 5G

Chuẩn bị: một điện thoại, quay màn hình, tắt Wi-Fi, không mở dashboard API key hoặc thông tin SmarterASP.

1. Ghi model điện thoại, hệ điều hành, trình duyệt, mạng và giờ bắt đầu.
2. Mở `https://bondphupham-001-site1.ltempurl.com` bằng 4G/5G.
3. Ở tab Nhân viên, kiểm tra ba nút điều hướng, form upload, ô số tiền và ngữ cảnh `EMP-001 / Gia Phú / Vận hành`. Không cần gửi ảnh thật.
4. Mở file picker rồi hủy. Xác nhận trang không reload, không tràn ngang và nút vẫn bấm được.
5. Mở tab Quản lý. Dùng một fixture Verify đang chờ, ghi lại mã tám ký tự. Chọn một quyết định rồi xác nhận trang đi đúng sang Audit, không nháy về tab Nhân viên.
6. Ở Audit, xác nhận mã hồ sơ, quyết định, số tiền và giờ Việt Nam. Chọn Hoàn tác để đưa fixture về trạng thái escalation ban đầu.
7. Refresh trang ở cả Quản lý và Audit. Xác nhận hồ sơ và timeline vẫn còn.
8. Chụp tối thiểu bốn ảnh: URL trên 4G/5G, tab Nhân viên, tab Quản lý, Audit sau quyết định. Không chụp secret hoặc ảnh hóa đơn thật.

PASS khi không có HTTP 5xx, không tràn ngang, không nháy sai tab, quyết định và hoàn tác đúng, Audit giữ dữ liệu sau refresh. Nếu có lỗi, ghi video ngắn, thời gian, mã hồ sơ và thao tác ngay trước lỗi.

## 2 Ba phiên người dùng có giám sát

Mỗi phiên dùng fixture tổng hợp hoặc dữ liệu đã có quyền sử dụng. Không yêu cầu người dùng cung cấp hóa đơn cá nhân mới.

1. Ghi consent, vai trò người dùng, thiết bị/mạng và thời lượng phiên.
2. Giao cùng ba task: tìm nơi upload và nhập số tiền; giải thích kết quả AUTO hoặc ESCALATE; xử lý một ngoại lệ trong vai trò quản lý và tìm Audit.
3. Người quan sát không hướng dẫn trong lượt đầu. Ghi thời gian hoàn thành, điểm dừng, lỗi thao tác và câu hỏi của người dùng.
4. Cuối phiên hỏi ba câu: phần nào khó hiểu nhất, lý do AI có đủ rõ không, và người dùng có biết bước tiếp theo không.
5. Cho người dùng xác nhận bản ghi feedback. Tách phát hiện thành blocker, nên sửa trước demo hoặc backlog.

Không tổng hợp phần trăm từ ba người. Báo ba quan sát cụ thể và thay đổi đã thực hiện hoặc lý do chưa thực hiện.

## 3 Ba lượt rehearsal

1. Lượt một kiểm nội dung và đường đi demo. Bấm giờ từ câu mở đầu đến kết luận.
2. Sửa script hoặc thao tác gây chậm. Nếu phải sửa code, chạy lại technical gate liên quan.
3. Lượt hai kiểm thời lượng, chuyển slide và fallback local khi live URL chậm.
4. Lượt ba dùng đúng commit và đúng dữ liệu sẽ trình bày. Không đổi code sau lượt này nếu không có blocker.

Mục tiêu an toàn là 8 phút, để lại khoảng 1 đến 2 phút cho chuyển cảnh hoặc câu hỏi trong khung 8 đến 10 phút.

## 4 Quyết định public URL

Sau khi mục 1 PASS và có ít nhất một rehearsal sạch, có thể đưa URL vào README với nhãn `Supervised MVP demo`. Ba user session vẫn cần hoàn tất để có development story cho pitch. Không mô tả URL là production service hoặc unattended pilot.

Official holdout 15 ảnh không chạy lại trong các gate này. Nếu upload lại, phải ghi là post-holdout regression, xin quyền chuyển dữ liệu mới và giữ nguyên raw score 10/15.

## 5 Readiness cho đề onsite 17/10

Đề và cách đánh giá chỉ được BTC công bố trong khai mạc. Không đoán rồi mở feature lớn trên baseline hiện hành.

1. Chạy hai drill thay đổi: một rule/ngưỡng và một field/UI hoặc report nhỏ. Mỗi drill phải ghi acceptance criteria trước khi code.
2. Với mỗi drill, chứng minh được targeted test, full suite/build liên quan, một negative case và evidence runtime.
3. Chuẩn bị script vòng bảng 8 phút trong khung tối đa 15 phút, để khoảng 5 phút Q&A và 2 phút điều phối.
4. Chuẩn bị thêm bản nén 5 phút cho vòng Chung kết; đây là phương án dự phòng, không phải giới hạn BTC đã công bố.
5. Ngày 16/10 khóa commit, dữ liệu demo và slide; không đổi schema, model hoặc package nếu không có blocker.
6. Ngày 17/10 mục tiêu có mặt 07:30–07:45; mang laptop, sạc, hotspot và adapter. Kiểm local fallback trong 09:00–09:30.

Tài liệu rehearsal và chiến lược onsite chi tiết được giữ ngoài repository công khai. Trong repo chỉ giữ gate kỹ thuật, acceptance criteria và evidence có thể kiểm chứng.
