# Liên kết nộp bài AURA

## Thông tin dự án

- Dự án: AURA Automated Underwriting and Reimbursement AI
- Đội: The Deciders
- Track: OrganizationAI
- Đề bài: Spec A The Escalation Referee
- Tác giả: Phạm Gia Phú

## Truy cập

- Ứng dụng live: https://bondphupham-001-site1.ltempurl.com/
- Health endpoint: https://bondphupham-001-site1.ltempurl.com/healthz
- Repository công khai: https://github.com/BondPhuPhamzZ/AURA
- Release đóng băng: `aura-demo-day-2026-10-14` (tag được tạo từ commit đã đồng bộ cuối cùng và push cùng repository)
- Video chính thức: https://drive.google.com/file/d/14Co4QjnoDeYPTORXcxi1u-r1MOuX2KM0/view?usp=sharing

## Thao tác đầu tiên dành cho BGK

Mở ứng dụng live, chọn **Chạy Verify Harness** và chờ bảng hoàn tất ba ca duyệt thường quy cùng hai ca chuyển tiếp. Sau đó mở một hồ sơ cần chuyển tiếp, chọn **Chuyển tiếp**, xử lý tại tab **Quản lý**, rồi kiểm tra **Lịch sử hành vi** và **Undo**.

## Ranh giới của bằng chứng hiện tại

- 163/163 là số test offline, không phải 163 hóa đơn.
- Verify live hoàn tất 5/5 trong 65,004 giây.
- Mini-holdout prospective 10 hóa đơn đạt 8/10, không có missed escalation, có hai over-escalation và không quan sát thấy lỗi system/provider/HTTP 429.
- Kết quả tuần tự 11/12 là lần lặp ổn định trên known data, không phải holdout mới.
- Candidate là MVP có giám sát, không hỗ trợ claim production accuracy hoặc triển khai không cần con người.

## Dữ liệu và bí mật

Không đưa API key, user secrets, hóa đơn cá nhân gốc, ảnh billing provider, log riêng tư hoặc connection string production vào gói Drive này. Nếu BGK cần evaluation key, hãy chia sẻ bằng kênh riêng và thu hồi sau đánh giá.
