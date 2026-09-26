# Kịch bản video demo AURA — 2 phút 50 giây

Trạng thái: video demo đã được liên kết từ README root. Tài liệu này được giữ làm kịch bản tái hiện; BTC vẫn có thể chạy toàn bộ luồng bằng localhost nếu hosting trial không ổn định.

Live URL: `https://bondphupham-001-site1.ltempurl.com/`

Chỉ quay sau khi URL trên mở ổn định trong cửa sổ ẩn danh, file mặc định `index.html` của hosting không còn được phục vụ, `/healthz` trả `status: ok` và một ảnh smoke test thành công. Lượt Verify live nên chính là lượt quay chính thức để không tốn thêm năm request. Video nên có thuyết minh tiếng Việt; không cần quay khuôn mặt.

## Chuẩn bị trước khi bấm ghi hình

1. Đổi `Database__ApplyMigrationsOnStartup` từ `true` sang `false`, lưu và recycle pool.
2. Mở `/healthz`, trang Nhân viên, Quản lý và Lịch sử trong các tab riêng.
3. Đăng xuất các tài khoản cá nhân, tắt thông báo và không mở OpenRouter Billing/API Keys.
4. Chuẩn bị `test_kit/images/TK-04-missing-identifier.jpg` và số tiền `210000` để minh họa một hồ sơ cần con người xử lý.
5. Đặt trình duyệt ở 100%, quay 1920x1080 và kiểm micro trước khi ghi.
6. Làm sạch dữ liệu demo thừa nếu cần, nhưng giữ một hàng Audit đã hoàn tất để tránh chờ lâu khi quay. Chưa bấm Verify trong lúc diễn tập.

## 00:00–00:15 — Mở đầu tại Live URL

**Thao tác:** Mở Live URL trong cửa sổ ẩn danh, lia qua ba khu vực Nhân viên, Quản lý và Lịch sử.

**Lời thoại:**

> Xin chào Ban Giám khảo. Đây là AURA, hệ thống hỗ trợ thẩm định hoàn ứng. Qwen chỉ đọc dữ kiện trên hóa đơn; policy C# mới quyết định tự duyệt hay chuyển con người xử lý ngoại lệ.

## 00:15–01:00 — Verify Harness 5 ca

**Thao tác:** Nhấn `Chạy Verify Harness` đúng một lần. Trong khi hệ thống chạy tuần tự, chỉ vào kỳ vọng, kết quả thực tế, đối chiếu và độ trễ. Khi hoàn tất, giữ màn hình ở bảng tổng kết 5 ca.

**Lời thoại:**

> Bộ Verify chạy năm ảnh tổng hợp qua cùng đường xử lý production, gồm ba ca thường quy và hai ca phải chuyển tiếp. Bảng so sánh expected với actual, không gán PASS khi provider lỗi. Hệ thống dừng các lượt còn lại nếu gặp lỗi quota hoặc lỗi provider mang tính hệ thống.

Nếu kết quả live không đạt 5/5, không quay lại liên tục để săn PASS. Dừng ghi, kiểm tra OpenRouter Activity và xử lý nguyên nhân trước.

## 01:00–01:40 — Upload một hóa đơn mới

**Thao tác:** Chọn `TK-04-missing-identifier.jpg`, nhập `210000`, bấm `AI tự động kiểm`. Cho thấy preview ảnh, loading, khung dữ kiện AI và kết quả `ESCALATE_FACT`. Nhấn `Chuyển tiếp`.

**Lời thoại:**

> Đây là một hóa đơn thiếu số hóa đơn hoặc mã truy vết. AURA vẫn hiển thị những dữ kiện đọc được, nhưng policy ưu tiên bất định dữ kiện và yêu cầu kiểm tra thủ công. Nhân viên chủ động xác nhận chuyển tiếp; hệ thống không âm thầm giao quyền quyết định cho AI.

## 01:40–02:15 — Quản lý và Audit Log

**Thao tác:** Mở tab Quản lý, chọn hồ sơ vừa chuyển và nhấn `Từ chối` hoặc `Đồng ý` theo câu hỏi hiển thị. Chuyển sang Lịch sử, mở chi tiết/timeline của đúng hồ sơ.

**Lời thoại:**

> Quản lý nhận đúng câu hỏi cần quyết định, kèm ảnh và dữ kiện đầu vào. Sau thao tác, Audit Log hiển thị một hồ sơ với timeline từ kết quả AI, chuyển tiếp của nhân viên đến quyết định của quản lý; dữ liệu không bị nhân thành ba lịch sử rời rạc.

## 02:15–02:38 — Kiến trúc và bằng chứng

**Thao tác:** Hiện slide kiến trúc hoặc sơ đồ workflow trong hồ sơ nộp.

**Lời thoại:**

> AURA chạy trên ASP.NET Core 8, SQL Server và kho ảnh riêng. Bản Sprint 1 dùng OpenRouter chuyển ảnh tới Qwen để trích xuất JSON có cấu trúc. Policy, workflow chuyển tiếp và audit chạy trong C#. Repo có 60 test offline, năm ca Verify trực tiếp và gói 15 ca tham chiếu cho Ban Giám khảo.

## 02:38–02:50 — Giới hạn thật và kết thúc

**Thao tác:** Hiện slide giới hạn hoặc quay về màn hình chính.

**Lời thoại:**

> Sprint này mới hỗ trợ JPG hoặc PNG một trang và còn phụ thuộc quota của provider. Khi AI lỗi, AURA chuyển kiểm tra thủ công thay vì tạo kết quả giả. Cảm ơn Ban Giám khảo.

## Checklist sau khi quay

- Thời lượng dưới 3 phút; ưu tiên một cảnh quay liền mạch.
- Có tiếng thuyết minh rõ, không cần nhạc nền và không cần webcam.
- Video có Live URL, Verify, một input mới, quyết định quản lý, Audit Log và một giới hạn chưa hoàn hảo.
- Không lộ API key, connection string, billing, publish profile hoặc dữ liệu cá nhân thật.
- Upload YouTube Unlisted hoặc Google Drive `Anyone with the link`, rồi thử link bằng cửa sổ ẩn danh.
- Không commit video `.mp4` vào GitHub; chỉ điền link xem vào checklist/form nộp.
