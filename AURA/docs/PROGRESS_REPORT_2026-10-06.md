# AURA — Nội dung báo cáo tiến độ 06/10/2026

## Mốc check-in

Sprint 2 — hậu holdout, xác minh deployment và chốt demo candidate.

## Tiến độ & công việc đã làm

Đội đã hoàn tất official holdout 15 hóa đơn thật một lượt với nhãn và SHA-256 khóa trước request đầu tiên. Kết quả raw là 10/15 quyết định đúng; review độc lập phát hiện một nhãn người sai nên adjusted view là 11/15, nhưng raw evidence được giữ nguyên. Từ lỗi holdout, đội bổ sung evidence contract v2: chỉ chấp nhận tổng cuối được in rõ, đối chiếu ngày/giờ gốc, tách money role và identifier, mở rộng taxonomy personal-item và hiển thị provenance trên UI.

Bản `c382ce7` đã được deploy lên SmarterASP. Health 5/5 đều `ok`, DB/storage/migration đạt; security negative và upload guard đạt; giao diện browser mobile, Quản lý và Audit render ổn; Verify thật qua OpenRouter đạt 5/5 đúng 3 AUTO, 1 FACT, 1 POLICY. Kiểm tra phát hiện giờ Audit phụ thuộc timezone máy host; DB UTC vẫn đúng. Code fix `d9d357c` đã chuyển đổi rõ sang giờ Việt Nam, thêm regression test; latest `master` đạt build sạch, 122/122 test, EF model sạch và không có package bị báo vulnerable. Cần publish/recycle latest `master` và xác nhận lại Audit trước khi freeze/public live URL.

## Link GitHub

https://github.com/BondPhuPhamzZ/AURA

## Khó khăn / cần hỗ trợ

Đội đang chờ BTC kết nối ba người dùng thực tế để chạy usability session có giám sát và chờ thông tin GPU 16 GB để benchmark Ollama. GPU không phải blocker của MVP vì OpenRouter là primary. Cần hỗ trợ lịch user/GPU đủ sớm; đội sẽ không tuyên bố production accuracy trước khi có user feedback, mobile 4G evidence và rehearsal cuối.

## Bản cực ngắn để dán form

Hoàn tất official holdout 15 hóa đơn thật (raw 10/15; adjusted 11/15 do một nhãn người sai, raw giữ nguyên) và triển khai contract v2 để chặn suy tổng/ngày/money role/identifier sai. Deploy `c382ce7` đạt health 5/5, security/upload guard, UI mobile browser và Verify OpenRouter 5/5. Hậu kiểm phát hiện giờ Audit dùng timezone host; DB UTC đúng, source đã sửa UTC→giờ Việt Nam và đạt 122/122 test, build/EF/package gate sạch. Việc tiếp theo: publish/recycle bản vá, xác nhận Audit + mobile 4G, ba user session và ba rehearsal. Đang chờ BTC hỗ trợ user thực tế và lịch GPU; GPU không chặn MVP.
