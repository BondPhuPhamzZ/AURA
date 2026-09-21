# Kịch bản video demo AURA — 2 phút 45 giây

Video URL: **[ĐIỀN SAU KHI UPLOAD — không commit file video lớn]**

## 00:00–00:20 — Bài toán

Hiện slide 1: quy trình hoàn ứng thủ công chậm, không nhất quán và khó truy vết. Chốt nguyên tắc: “AI đọc, policy quyết định, con người xử lý ngoại lệ”.

## 00:20–01:05 — Verify Harness

Mở Live URL ở tab ẩn danh, bấm **Chạy Verify Harness** đúng một lần. Chỉ ra 5 ca gồm 3 routine và 2 escalation, expected/actual, PASS/FAIL, latency. Không mở OpenRouter Activity, billing hoặc secret.

## 01:05–01:40 — Một hóa đơn mới

Chọn ảnh tổng hợp đã chuẩn bị, cho thấy preview, nhập số tiền và bấm **AI tự động kiểm**. Chỉ vào facts Qwen đọc và kết quả policy. Nếu là escalation, bấm **Chuyển tiếp**.

## 01:40–02:10 — Quản lý và Audit

Mở tab **Quản lý**, trả lời Đồng ý/Từ chối cho một hồ sơ. Sau đó mở **Lịch sử**: một dòng đại diện cho hồ sơ, mở timeline để cho thấy AI xử lý → nhân viên chuyển → quản lý quyết định. Nếu dữ liệu demo đã chuẩn bị, hoàn tác một lần và chỉ ra trạng thái được khôi phục.

## 02:10–02:35 — Kiến trúc

Hiện slide 4: Qwen/OpenRouter chỉ extraction; policy C# quyết định; SQL Server lưu hồ sơ/audit; ảnh nằm ở private storage. Nêu rõ Test Kit là dữ liệu tổng hợp.

## 02:35–02:45 — Giới hạn

Hiện slide 5: chỉ JPG/PNG đơn trang, chưa xác thực hóa đơn với registry, provider có thể rate-limit. Kết thúc trước 3 phút.

## Checklist quay

- Làm nóng Live URL và kiểm `/healthz` trước khi bấm record.
- Mở sẵn ba tab: Nhân viên, Quản lý, Lịch sử.
- Tắt notification, quay 1080p, zoom đủ đọc.
- Dùng fixture tổng hợp, không dùng hóa đơn cá nhân.
- Nếu provider lỗi, không chạy lặp để “săn PASS”; quay lại sau hoặc nêu đúng giới hạn.
- Upload video dạng Unlisted/Anyone with link, thử link bằng cửa sổ ẩn danh.
