# AURA - Automated Underwriting & Reimbursement AI

## Chủ đề Track A và Vấn đề AURA Giải Quyết
AURA tập trung giải quyết bài toán tự động hóa kiểm duyệt hóa đơn chi tiêu nội bộ (Reimbursement) bằng công nghệ AI Vision. Thay vì kế toán phải đọc tay hàng ngàn hóa đơn, AURA trích xuất thông tin tự động, đối chiếu với chính sách công ty (BUSINESS_RULES.md) và ra quyết định **Duyệt tự động (AUTO_APPROVE)** hoặc **Chuyển tiếp (ESCALATE)** cho sếp kèm theo lý do cụ thể.

## Live URL
*(Sẽ cập nhật sau khi deploy Azure)*

## Hướng dẫn Demo 90 giây
1. Truy cập Live URL.
2. Tại trang chủ, chọn nút **"Verify Harness"** (hoặc truy cập /Verify).
3. Bấm **"Run 5 Canonical Cases"**.
4. Bảng kết quả sẽ ngay lập tức hiện ra PASS/FAIL cùng thời gian phản hồi (Latency) cho từng ca, chứng minh tính Deterministic và tốc độ của Rule Engine.

## Kiến trúc Hệ thống
Ảnh hóa đơn -> AI Vision (Gemini 1.5 Flash) trích xuất Facts -> JSON Schema -> C# Policy Decision Engine (Deterministic) -> Quyết định (AUTO/ESCALATE).

## Cách chạy Local
1. \git clone\ repository.
2. Thêm Secret: \dotnet user-secrets set "Gemini:ApiKey" "YOUR_KEY"\.
3. \dotnet ef database update\.
4. \dotnet run\.

## Dữ liệu
- Dữ liệu thật: Tên công ty, tên nhà hàng, số tiền.
- Dữ liệu giả lập: Không có. Tất cả logic được xử lý realtime.

## Giới hạn hiện tại
- Chưa hỗ trợ PDF nhiều trang.
- Rule engine mới phủ được các luật cơ bản của Track A.
