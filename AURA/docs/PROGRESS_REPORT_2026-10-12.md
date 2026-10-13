# Báo cáo tiến độ lịch sử sau publish và live benchmark 12/10/2026

> Đây là checkpoint lịch sử. Báo cáo nộp cuối phải dùng `docs/CURRENT_PROJECT_STATUS_2026-10-13.md`, `docs/OFFICIAL_HOLDOUT10_2026-10-13.md` và `docs/FINAL_READINESS_GAPS_2026-10-13.md`.

## Nội dung điền biểu mẫu

### Mốc Check-in hoặc Giai đoạn

`Check-in sau publish và live benchmark 12/10/2026`

### Tiến độ và Công việc đã làm

`Đã chốt release candidate AURA dùng OpenRouter/Qwen3.8 Flash qua Alibaba, reasoning none, fallback tắt, durable SQL queue và AI concurrency 1. Release build sạch, 161/161 automated test pass. Regression hậu chỉnh trên 15 hóa đơn thật cũ đạt 15/15 quyết định, 0 missed escalation, 0 over-escalation, 0 system error và 30/30 field trọng yếu; đây là regression, không phải blind holdout mới. Burst 5 upload hoàn tất 5/5 với E2E P95 41,079 giây. Sau publish/recycle, live health đạt status ok và Verify Harness đạt 5/5 trong 65,004 giây; mô phỏng 5 người cùng bấm chỉ tạo 1 batch thật (1 HTTP 200, 4 HTTP 409), không có 429/system error. Đã giữ raw evidence, sửa lỗi tổng hợp JSON của runner Windows PowerShell 5.1 mà không replay provider, đồng bộ source, runbook, benchmark, workflow, slide, Build Log và tài liệu phản biện cá nhân.`

### Link GitHub

`https://github.com/BondPhuPhamzZ/AURA`

### Khó khăn hoặc Cần hỗ trợ

`OpenRouter/Alibaba vẫn là dịch vụ bên thứ ba nên availability không thể được coi là SLA production; nhóm giữ video/evidence dự phòng và demo một operator. Cần hỗ trợ kết nối 3 người dùng có giám sát để hoàn tất usability evidence, và một người độc lập rà soát ground truth cho bộ 11 hóa đơn mới trước khi khóa manifest. Không cần hỗ trợ sửa lỗi blocker kỹ thuật ở thời điểm check-in; việc còn lại là rehearsal trên commit freeze, kiểm link từ thiết bị khác và hoàn tất consent/redaction/nhãn độc lập nếu chạy holdout mới.`

## Ranh giới claim khi gửi

- Không đổi `15/15 regression` thành accuracy production hoặc blind holdout.
- Không nói năm giám khảo cùng chạy năm batch; gate chỉ cho một batch chạy và bốn lượt còn lại nhận 409.
- Không công bố raw receipt, API key, connection string hoặc dashboard billing.
- Nếu bộ 11 ảnh chưa khóa nhãn/hash trước request đầu tiên, chưa gọi là holdout.
