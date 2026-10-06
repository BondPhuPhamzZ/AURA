# AURA — Nội dung báo cáo tiến độ 07/10/2026

## Mốc check-in

Sprint 2 — hardening policy và chuẩn bị regression/GPU benchmark.

## Tiến độ & công việc đã làm

Đội giữ nguyên raw official holdout và Test Kit v3, không chạy lại để làm đẹp chỉ số. Từ phân
tích mismatch, đội bổ sung taxonomy policy hẹp cho vé xem phim/rạp chiếu phim, biến ground
truth R3-12 v3.1 thành automated test thực thi trực tiếp qua PolicyDecisionEngine, và siết prompt
giữ nguyên dấu tiếng Việt để giảm nhầm `Bìa hồ sơ` thành `Bia hồ sơ`. AURA tiếp tục gửi nguyên
byte ảnh, không resize ở ứng dụng. Backend đã log token/duration OpenRouter/Ollama để benchmark
đúng nguyên nhân context/output/off-load. Sau publish, health 5/5 và security negative 4/4 pass.
Live v3.1 hoàn tất 15/15 request: 13/15 exact decision, 0 missed escalation, 0 system error,
fallback 0 và field UTF-8-safe 140/146. Hai mismatch đều fail-safe do model lặp identifier.
Runner được vá đọc UTF-8 tường minh; full suite 142/142 pass. Tái xác nhận live Verify 5/5,
0 system error; workflow FACT chuyển quản lý, duyệt rồi UNDO trở lại FACT và Audit giữ đủ vết.
UI desktop/mobile 390x844 pass smoke, tên mặc định `Gia Phú`, console sạch. Raw evidence/hash
v3.1 giữ nguyên.

Đội cũng hoàn tất đúng một batch OpenRouter hậu-contract còn thiếu cho Test Kit v2 judge set:
15/15 request hoàn tất, 14/15 exact, 1 missed TK-12, 0 over/system error, field 69/75 và không
dùng fallback. TK-12 là fixture blur cũ vẫn bị VLM khẳng định đọc được tổng tiền; raw được giữ
nguyên và không sửa policy theo heuristic ảnh mờ. Safety gate hiện hành vẫn là v3.1 với physical
ink loss và 0 missed escalation.

## Link GitHub

https://github.com/BondPhuPhamzZ/AURA

## Khó khăn / cần hỗ trợ

Đội đang chờ kết nối ba người dùng thật và lịch GPU BTC. GPU 16 GB sẽ được dùng để benchmark
Ollama 8B riêng với context 16384/output 4096; fallback vẫn tắt cho tới khi đạt 0 system error,
0 missed escalation và evidence processor/token/latency đầy đủ.

## Bản ngắn để dán form

Đã harden policy C# cho nhóm vé xem phim/rạp chiếu phim, thêm test thực thi ground truth R3-12
v3.1 và guard giữ dấu tiếng Việt (`Bìa`/`Bia`). Sau publish, health 5/5, security 4/4, live
v3.1 15/15 completed (13/15 exact, 0 missed, 0 system error, field 140/146), Verify 5/5 và
workflow duyệt/hoàn tác có Audit đều pass. UI desktop/mobile 390x844 và console pass smoke;
142/142 test pass. Tiếp theo: benchmark Ollama 8B cùng v3.1 trên GPU BTC, 3 user session và
3 rehearsal; fallback vẫn tắt. OpenRouter hậu-contract v2 đạt 14/15 (missed TK-12 blur cũ,
0 system error), nên đội giữ raw và dùng v3.1 làm safety gate hiện hành.
