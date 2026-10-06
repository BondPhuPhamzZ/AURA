# AURA — Nội dung báo cáo tiến độ 06/10/2026

## Mốc check-in

Sprint 2 — hậu holdout, xác minh deployment và chốt demo candidate.

## Tiến độ & công việc đã làm

Đội đã hoàn tất official holdout 15 hóa đơn thật một lượt với nhãn và SHA-256 khóa trước request đầu tiên. Kết quả raw là 10/15 quyết định đúng; review độc lập phát hiện một nhãn người sai nên adjusted view là 11/15, nhưng raw evidence được giữ nguyên. Từ lỗi holdout, đội bổ sung evidence contract v2: chỉ chấp nhận tổng cuối được in rõ, đối chiếu ngày/giờ gốc, tách money role và identifier, mở rộng taxonomy personal-item và hiển thị provenance trên UI.

Bản `1f4b3c9` đã được deploy lên SmarterASP và xác minh hậu publish. Health 5/5 đều `ok`, DB/storage/migration đạt; security negative 6/6; viewport mobile 390×844 không tràn ngang; Verify thật qua OpenRouter đạt 5/5 đúng 3 AUTO, 1 FACT, 1 POLICY. Hai luồng quản lý bằng fixture tổng hợp cũng đạt: từ chối policy rồi hoàn tác và duyệt fact rồi hoàn tác. Audit hiển thị đúng giờ Việt Nam cho event mới/cũ. Build Release sạch, 122/122 test, EF model sạch và không có package bị báo vulnerable.

## Link GitHub

https://github.com/BondPhuPhamzZ/AURA

## Khó khăn / cần hỗ trợ

Đội đang chờ BTC kết nối ba người dùng thực tế để chạy usability session có giám sát và chờ thông tin GPU 16 GB để benchmark Ollama. GPU không phải blocker của MVP vì OpenRouter là primary. Cần hỗ trợ lịch user/GPU đủ sớm; đội sẽ không tuyên bố production accuracy trước khi có user feedback, mobile 4G evidence và rehearsal cuối. Technical baseline đã sẵn sàng cho supervised demo.

## Bản cực ngắn để dán form

Hoàn tất official holdout 15 hóa đơn thật (raw 10/15; adjusted 11/15 do một nhãn người sai, raw giữ nguyên) và triển khai contract v2. Bản live `1f4b3c9` hậu publish đạt health 5/5, security 6/6, Verify OpenRouter 5/5, workflow/undo 2/2, mobile-browser 390 px và Audit UTC→giờ Việt Nam; build 0 warning/error, 122/122 test, EF/package gate sạch. Technical baseline sẵn sàng cho supervised demo. Tiếp theo: điện thoại thật qua 4G/5G, ba user session và ba rehearsal. Đang chờ BTC hỗ trợ user thực tế và lịch GPU; GPU không chặn MVP.
