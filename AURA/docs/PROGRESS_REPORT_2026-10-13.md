# Báo cáo tiến độ sau prospective mini-holdout 13/10/2026

## Nội dung điền biểu mẫu

### Mốc Check-in hoặc Giai đoạn

`Check-in sau prospective real-receipt mini-holdout 13/10/2026`

### Tiến độ và Công việc đã làm

`Đã hoàn tất đúng một official run trên 10 hóa đơn thật mới do chủ project sở hữu, đã consent, redaction và khóa expected status/SHA-256 trước request đầu tiên. Live AURA dùng OpenRouter/Qwen3.8 Flash qua Alibaba, reasoning none, fallback tắt và AI concurrency 1. Kết quả đạt 8/10 quyết định đúng, 0 missed-escalation, 2 over-escalation, 0 system error, 0 lỗi 429/provider quan sát được, 0 fallback, 0 duplicate và 28/30 field date/identifier/final-total. Năm request đầu hoàn tất trong 64,763 giây; cả 10 request trong 108,803 giây. Hai mismatch đều fail-safe: một do validator nhãn final-total quá chặt và một do merchant extraction tương tác với vùng redaction. Không rerun để săn số đẹp; raw response, manifest, ảnh provider-ready và checksum được giữ riêng ngoài Git.`

`Addendum 2 receipt được khóa/chạy riêng đúng một lượt sau đó: Chidori khớp ESCALATE_FACT do Chủ nhật; Three O'Clock bị over-escalate dù merchant/date/identifier/items/total đều đúng vì warning che mờ bằng dải đen. Addendum đạt 1/2, 0 missed, 1 over, 0 system/provider/429 error; cả hai terminal trong 23,020 giây. Combined descriptive view là 9/12 nhưng không được gọi là holdout 12 ca mới. Đã lập kế hoạch evidence-region overlay không chỉnh ảnh gốc; chưa thay đổi source/prompt/policy/schema trong baseline.`

### Link GitHub

`https://github.com/BondPhuPhamzZ/AURA`

### Khó khăn hoặc Cần hỗ trợ

`Candidate đủ cơ sở cho supervised demo nhưng chưa đủ để claim production accuracy hoặc unattended rollout. Cần giữ một operator, video dự phòng và không chạy Verify song song upload do OpenRouter/Alibaba là upstream bên thứ ba. Sau submission freeze mới nên harden hai nguồn over-escalation bằng fixture âm/dương độc lập; không dùng lại 10 ảnh để tạo claim holdout mới. Ba user session có giám sát và rehearsal trên commit freeze vẫn là evidence vận hành cần hoàn tất.`

## Ranh giới claim

- Không đổi 8/10 thành 80% production accuracy.
- Không bỏ qua 2 over-escalation hoặc redaction confounder.
- Không gọi lượt chạy lại trên 10 ảnh là holdout mới.
- Không gọi phép ghép 10-case holdout + 2-case addendum là holdout 12 ca mới.
- Không commit receipt, raw response, API key hoặc dữ liệu vận hành riêng tư.
