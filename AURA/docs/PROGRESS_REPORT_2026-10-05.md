# AURA — Báo cáo tiến độ 05/10/2026

## Mốc check-in

Sprint 2 — chốt deployment candidate và chuẩn bị blind holdout/user validation.

## Tiến độ và công việc đã làm

Đội đã hoàn thiện MVP xử lý hoàn ứng theo kiến trúc AI đọc dữ kiện, policy C# quyết định và con người xử lý ngoại lệ. Upload trả HTTP 202 và được xử lý bằng hàng đợi SQL có lease/reclaim; trạng thái, ảnh và audit tồn tại qua refresh/recycle. Hệ thống có semantic validation, tối đa một repair, đối chiếu ngày/identifier/tổng sau giảm giá, fail-safe khi vùng món bị che, chuyển tiếp cho quản lý và YES/NO/UNDO có audit.

Feature revision `e94af11` với cập nhật responsive/holdout ngày 05/10 build 0 warning/0 error, 108/108 automated tests pass. Deployment SmartASP trước bản UI này đã đạt health/DB/storage/migration, security-negative, AUTO smoke, FACT fail-safe smoke, Verify 5/5 đúng 3 auto/2 escalate, human workflow, persistence và concurrent smoke 2 + 5 request; sau khi publish `e94af11` vẫn phải chụp lại mobile/4G smoke. OpenRouter tiếp tục là primary; Ollama fallback đã chứng minh 1/1 nhưng mặc định tắt do latency và một missed escalation trên judge set.

Đội đã tạo quy trình holdout có kiểm hash trước request đầu tiên và ma trận 15 ca chi tiết theo 5 AUTO/4 FACT/3 POLICY/3 AUTHORITY. Tập private hiện có 12 candidate, chưa chạy model; đối chiếu hash không tìm thấy bản byte-identical trong evidence cũ. Hai receipt Vinamilk/Katinat mới vẫn có thể là holdout vì khác giao dịch; điều kiện là chính ảnh/giao dịch đó chưa từng được model xử lý hoặc dùng để tune. UI mobile đã chuyển header thành 3 tab vừa màn hình, form thành một cột, bảng kết quả/quản lý/audit thành card, đổi tên mặc định thành Gia Phú và đồng bộ màu input. Browser local xác nhận không tràn ngang ở 360/390/1440 px; Audit 148 dòng render dạng card ở 360 px.

Việc tiếp theo là phân loại 12 candidate theo ma trận, thu thập thêm số candidate còn thiếu theo **coverage thực tế** (không mặc định chỉ thiếu 3), consent/redaction, khóa claimed amount/expected decision/expected facts/SHA-256, chạy một lượt OpenRouter, thực hiện ba user session và ba dress rehearsal.

## Link GitHub

https://github.com/BondPhuPhamzZ/AURA

## Khó khăn / cần hỗ trợ

Cần hỗ trợ kết nối ba người dùng thực tế từng nộp hoặc duyệt hoàn ứng để thực hiện usability session và xác nhận feedback. Nếu BTC hỗ trợ GPU, đội cần lịch/cấu hình sớm để chuẩn bị POC Ollama bằng synthetic data; GPU không phải blocker của MVP. Responsive local đã pass; còn thiếu publish lại SmartASP và evidence điện thoại thật/4G trước khi public live URL.

## Ranh giới claim

Các kết quả 15/15 OpenRouter và 14/15 Ollama là judge-set tổng hợp; các smoke receipt thật là development evidence. Đội chưa tuyên bố production accuracy hoặc production readiness cho tới khi hoàn tất holdout, user validation và các kiểm soát production như auth/RBAC, storage/retention, malware scan, monitoring và backup/restore.
