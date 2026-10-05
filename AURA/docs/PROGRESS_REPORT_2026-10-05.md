# AURA — Báo cáo tiến độ 05/10/2026

## Mốc check-in

Sprint 2 — chốt deployment candidate và chuẩn bị blind holdout/user validation.

## Tiến độ và công việc đã làm

Đội đã hoàn thiện MVP xử lý hoàn ứng theo kiến trúc AI đọc dữ kiện, policy C# quyết định và con người xử lý ngoại lệ. Upload trả HTTP 202 và được xử lý bằng hàng đợi SQL có lease/reclaim; trạng thái, ảnh và audit tồn tại qua refresh/recycle. Hệ thống có semantic validation, tối đa một repair, đối chiếu ngày/identifier/tổng sau giảm giá, fail-safe khi vùng món bị che, chuyển tiếp cho quản lý và YES/NO/UNDO có audit.

Baseline `c42c5ef` build 0 warning/0 error, 108/108 automated tests pass và không có pending EF model change. Deployment SmartASP sau bản vá đạt health/DB/storage/migration, security-negative, AUTO smoke, FACT fail-safe smoke, Verify 5/5 đúng 3 auto/2 escalate, human workflow, persistence và concurrent smoke 2 + 5 request. OpenRouter tiếp tục là primary; Ollama fallback đã chứng minh 1/1 nhưng mặc định tắt do latency và một missed escalation trên judge set.

Đội đã tạo quy trình holdout có kiểm hash trước request đầu tiên. Tập candidate hiện chưa được chạy chính thức vì inventory chưa đồng bộ với 9 file hiện có và một số ảnh đã dùng trong development phải loại. Việc tiếp theo là thu thập đủ 15 ảnh eligible, consent/redaction, khóa claimed amount/expected decision/expected facts/SHA-256, chạy một lượt OpenRouter, thực hiện ba user session và ba dress rehearsal.

## Link GitHub

https://github.com/BondPhuPhamzZ/AURA

## Khó khăn / cần hỗ trợ

Cần hỗ trợ kết nối ba người dùng thực tế từng nộp hoặc duyệt hoàn ứng để thực hiện usability session và xác nhận feedback. Nếu BTC hỗ trợ GPU, đội cần lịch/cấu hình sớm để chuẩn bị POC Ollama bằng synthetic data; GPU không phải blocker của MVP. Mobile layout 390 px và Audit horizontal scroll còn là UX gap nhỏ; chưa ảnh hưởng logic quyết định nhưng cần xử lý/ghi evidence trước khi public live URL.

## Ranh giới claim

Các kết quả 15/15 OpenRouter và 14/15 Ollama là judge-set tổng hợp; các smoke receipt thật là development evidence. Đội chưa tuyên bố production accuracy hoặc production readiness cho tới khi hoàn tất holdout, user validation và các kiểm soát production như auth/RBAC, storage/retention, malware scan, monitoring và backup/restore.
