# Nội dung 5 slide và kịch bản video Sprint 1

Tài liệu này là bản chốt nội dung. Khi dựng slide/video, không thêm slide thứ sáu và không công bố fixture v2 đạt 5/5 cho tới khi đã chạy đúng một lượt trên Live URL.

## Slide 1 — Vấn đề và chủ đề

**AURA — VNG Track A: The Escalation Referee**

- Hoàn ứng thủ công chậm, thiếu nhất quán và khó truy vết lý do.
- AURA không để AI tự quyết định mọi trường hợp: AI Vision chỉ đọc chứng từ; policy C# quyết định tất định.
- Mục tiêu Sprint 1: tự xử lý hồ sơ rõ ràng, dừng và hỏi đúng người khi dữ kiện/chính sách/thẩm quyền chưa đủ.

Hình minh họa: một luồng ngắn `Hóa đơn → Trích xuất → Policy → Tự duyệt hoặc Chuyển quản lý`.

## Slide 2 — Input, xử lý, output và Human-in-the-loop

- Input: JPG/PNG tối đa 5 MB + số tiền nhân viên đề nghị.
- Kiểm đầu vào: extension, MIME, magic bytes, kích thước và SHA-256 phát hiện trùng.
- Gemini Structured Output trả dữ kiện; không trả quyền quyết định cuối.
- Policy ưu tiên `FACT → POLICY → AUTHORITY`.
- Output: `AUTO_APPROVE` hoặc câu hỏi chuyển tiếp cụ thể; nhân viên chuyển hồ sơ, quản lý chọn đồng ý/từ chối duyệt; mọi bước vào audit và có hoàn tác.

Nhấn mạnh e-commerce: mã vận chuyển là bằng chứng truy vết logistics, không thay mã số thuế hoặc bằng chứng thanh toán.

## Slide 3 — Demo và phương pháp đo

- Verify một nút: 5 ảnh đại diện, kỳ vọng 3 tự duyệt + 2 chuyển tiếp.
- Test offline: 48/48 automated tests; Test Kit v2 có 30 ảnh tổng hợp đa layout.
- Chỉ số hiển thị: expected/actual, PASS/FAIL, latency và timestamp.
- Kết quả lịch sử fixture v1: 5/5, khoảng 32 giây ngày 20/09/2026.
- Kết quả fixture v2 trên Live URL: **[ĐIỀN SAU KHI CHẠY: __/5, __ giây, thời điểm __]**.

Không gọi 5/5 fixture tổng hợp là “độ chính xác thực tế”. Accuracy tổng quát cần tập ảnh độc lập lớn hơn.

## Slide 4 — Kiến trúc và phần chạy thật

- ASP.NET Core MVC, EF Core, SQL Server/Azure SQL.
- `GeminiVisionExtractorService` gọi model thật; `PolicyDecisionEngine` và audit chạy thật.
- Chứng từ lưu riêng tư ngoài `wwwroot`; DB giữ metadata, facts, status và audit.
- Linux container, `/healthz`, migration opt-in và persistent storage path cấu hình bằng environment.
- Live URL: **[ĐIỀN HTTPS URL]**; repository: `https://github.com/BondPhuPhamzZ/AURA`.

Phần giả lập chỉ là 30 ảnh Test Kit tổng hợp. Không có quyết định hoặc PASS giả khi Gemini lỗi/quota.

## Slide 5 — Giới hạn, an toàn và bước tiếp theo

- Hiện chỉ hỗ trợ một ảnh JPG/PNG; chưa có PDF/nhiều trang, antivirus, tax/e-invoice lookup và ngoại tệ.
- Vision không chứng minh hóa đơn là thật; input mơ hồ luôn chuyển người.
- Free-tier có thể `429/5xx`; hệ thống retry có giới hạn rồi chuyển `ESCALATE_SYSTEM_ERROR`.
- Trước nộp: Live URL, một lượt Verify v2, video <3 phút và kiểm chứng lưu ảnh qua restart.
- Sprint 2: authentication/role, blob storage, retention, integration test, tập dữ liệu độc lập và đo missed/over-escalation.

## Kịch bản video mục tiêu 2 phút 45 giây

### 00:00–00:20 — Mở đầu

Hiển thị slide 1, nêu vấn đề và nguyên tắc “AI đọc, policy quyết định, con người xử lý ngoại lệ”. Không mở terminal hoặc trang chứa secrets.

### 00:20–01:05 — Verify Harness

Mở Live URL từ cửa sổ ẩn danh, bấm Verify đúng một lần. Trong lúc chờ, giải thích 5 ca gồm 3 routine và 2 escalation. Khi xong, chỉ vào expected/actual, PASS/FAIL, latency/timestamp. Nếu quota lỗi, không quay lại nhiều lần; dùng cảnh quay dự phòng đã ghi từ một lượt hợp lệ và ghi rõ thời điểm.

### 01:05–01:40 — Input mới và chuyển tiếp

Upload một ảnh mới đã chuẩn bị, nhập số tiền và chạy AI. Với ca escalation, bấm **Chuyển tiếp**, sang cửa sổ quản lý và chọn **Đồng ý duyệt** hoặc **Từ chối duyệt**; với lỗi hệ thống, chỉ ra chú thích kết quả **Đủ tiêu chuẩn/Không đủ tiêu chuẩn**. Chỉ ra toast thành công.

### 01:40–02:10 — Audit và hoàn tác

Mở lịch sử, lọc ngày hiện tại, mở chi tiết, cho thấy AI processed → employee forwarded → manager decision. Thực hiện hoàn tác một lần nếu kịch bản dữ liệu đã chuẩn bị cho phép.

### 02:10–02:35 — Kiến trúc và tính minh bạch

Hiển thị slide 4: Gemini chỉ extraction, policy C# quyết định; ảnh, metadata và audit được lưu. Nêu rõ Test Kit là tổng hợp và Live URL/model là thật.

### 02:35–02:45 — Kết

Hiển thị slide 5, nêu một giới hạn quan trọng và bước Sprint 2. Kết thúc trước 3 phút.

## Checklist ngay trước khi quay

- Điền Live URL và số liệu fixture v2 vào slide 3–4.
- Mở sẵn ba tab: nhân viên, quản lý, lịch sử; đóng dashboard API/billing.
- Chuẩn bị một ảnh smoke và số tiền chính xác; tên file không chứa dữ liệu cá nhân.
- Bật quay ở 1080p, zoom trình duyệt dễ đọc, tắt thông báo hệ điều hành.
- Xác nhận còn tối thiểu 10 request Gemini; không chạy 30 ca Test Kit.
- Nếu web service có cold start, mở URL và `/healthz` trước khi bắt đầu quay.
