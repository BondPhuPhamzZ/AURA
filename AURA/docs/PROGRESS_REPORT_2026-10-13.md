# Báo cáo tiến độ sau prospective mini-holdout 13/10/2026

## Nội dung điền biểu mẫu

### Mốc Check-in hoặc Giai đoạn

`Check-in sau prospective real-receipt mini-holdout 13/10/2026`

### Tiến độ và Công việc đã làm

`Đã hoàn tất đúng một official run trên 10 hóa đơn thật mới do chủ project sở hữu, đã consent, redaction và khóa expected status/SHA-256 trước request đầu tiên. Live AURA dùng OpenRouter/Qwen3.8 Flash qua Alibaba, reasoning none, fallback tắt và AI concurrency 1. Kết quả đạt 8/10 quyết định đúng, 0 missed-escalation, 2 over-escalation, 0 system error, 0 lỗi 429/provider quan sát được, 0 fallback, 0 duplicate và 28/30 field date/identifier/final-total. Năm request đầu hoàn tất trong 64,763 giây; cả 10 request trong 108,803 giây. Hai mismatch đều fail-safe: một do validator nhãn final-total quá chặt và một do merchant extraction tương tác với vùng redaction. Không rerun để săn số đẹp; raw response, manifest, ảnh provider-ready và checksum được giữ riêng ngoài Git.`

`Addendum 2 receipt được khóa/chạy riêng đúng một lượt sau đó: Chidori khớp ESCALATE_FACT do Chủ nhật; Three O'Clock bị over-escalate dù merchant/date/identifier/items/total đều đúng vì warning che mờ bằng dải đen. Addendum đạt 1/2, 0 missed, 1 over, 0 system/provider/429 error; cả hai terminal trong 23,020 giây. Combined descriptive view là 9/12 nhưng không được gọi là holdout 12 ca mới. Đã lập kế hoạch evidence-region overlay không chỉnh ảnh gốc; chưa thay đổi source/prompt/policy/schema trong baseline.`

`Post-holdout regression ảnh gốc ngày 13/10 được chủ dữ liệu cho phép cụ thể và chạy đúng 12 file source, không copy/crop/resize/snapshot/annotate. Kết quả đạt 10/12 exact, 0 missed, 2 over, 0 system/provider/429 error, 0 fallback và 35/36 field ngày/identifier/final-total. Năm upload khác nhau đồng thời được tiếp nhận và hoàn tất 5/5; upload-accept P95 5,913 giây nhưng E2E P95 93,684 giây, nên chỉ chứng minh tính toàn vẹn hàng đợi trên một process, chưa chứng minh SLA 5 kết quả dưới 90 giây. Lượt này là regression trên dữ liệu đã biết, không thay prospective baseline 9/12 và không phải model-only A/B.`

`UX đã bổ sung tự cuộn tới bảng kết quả ngay khi bắt đầu upload hoặc Verify. Prototype evidence overlay tách biệt cũng đã chứng minh hướng khả thi: giữ nguyên image bytes, gắn region JSON theo SHA-256 và vẽ lớp SVG/canvas; prototype chưa nằm trong pipeline production và không được dùng để thay facts/policy. Candidate hiện đạt 163/163 automated tests, Release build sạch và không có EF model change chưa migration.`

### Link GitHub

`https://github.com/BondPhuPhamzZ/AURA`

### Khó khăn hoặc Cần hỗ trợ

`Candidate đủ cơ sở cho supervised demo nhưng chưa đủ để claim production accuracy, SLA 5 kết quả dưới 90 giây hoặc unattended rollout. Cần giữ một operator, video dự phòng và không chạy Verify song song upload do OpenRouter/Alibaba là upstream bên thứ ba. Multi-instance vẫn cần distributed provider limiter/cooldown; hiện gate là per-process. Sau submission freeze mới nên harden hai nguồn over-escalation bằng fixture âm/dương độc lập; không dùng lại 12 ảnh để tạo claim holdout mới. Ba user session có giám sát và rehearsal trên commit freeze vẫn là evidence vận hành cần hoàn tất.`

## Ranh giới claim

- Không đổi 8/10 thành 80% production accuracy.
- Không bỏ qua 2 over-escalation hoặc redaction confounder.
- Không gọi lượt chạy lại trên 10 ảnh là holdout mới.
- Không gọi phép ghép 10-case holdout + 2-case addendum là holdout 12 ca mới.
- Không gọi regression ảnh gốc 10/12 là blind holdout mới hoặc causal A/B.
- Không claim wave 5 người dùng đều dưới 90 giây; phép đo có một ca 93,684 giây.
- Không commit receipt, raw response, API key hoặc dữ liệu vận hành riêng tư.
