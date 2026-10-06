# AURA — Nội dung báo cáo tiến độ 07/10/2026

## Mốc check-in

Sprint 2 — hardening policy và chuẩn bị regression/GPU benchmark.

## Tiến độ & công việc đã làm

Đội giữ nguyên raw official holdout và Test Kit v3, không chạy lại để làm đẹp chỉ số. Từ phân
tích mismatch, đội bổ sung taxonomy policy hẹp cho vé xem phim/rạp chiếu phim, biến ground
truth R3-12 v3.1 thành automated test thực thi trực tiếp qua PolicyDecisionEngine, và siết prompt
giữ nguyên dấu tiếng Việt để giảm nhầm `Bìa hồ sơ` thành `Bia hồ sơ`. AURA tiếp tục gửi nguyên
byte ảnh, không resize ở ứng dụng. Backend đã log token/duration OpenRouter/Ollama để benchmark
đúng nguyên nhân context/output/off-load. Release build, 140/140 test và EF model gate đều pass;
v3/v3.1 images/hash không đổi. Logic commit `0241dfa` đã push lên `origin/master`; bước kế tiếp
là publish đúng master đó, recycle/health, Verify smoke
và chạy đúng một live batch v3.1 trước khi đóng gate.

## Link GitHub

https://github.com/BondPhuPhamzZ/AURA

## Khó khăn / cần hỗ trợ

Đội đang chờ kết nối ba người dùng thật và lịch GPU BTC. GPU 16 GB sẽ được dùng để benchmark
Ollama 8B riêng với context 16384/output 4096; fallback vẫn tắt cho tới khi đạt 0 system error,
0 missed escalation và evidence processor/token/latency đầy đủ.

## Bản ngắn để dán form

Đã harden policy C# cho nhóm vé xem phim/rạp chiếu phim, thêm test thực thi ground truth R3-12
v3.1 và guard giữ dấu tiếng Việt (`Bìa`/`Bia`). Bổ sung log token/duration để đo đúng ảnh,
context, output cap và CPU/GPU off-load. Candidate Release đạt 140/140 test, EF sạch; giữ nguyên
raw holdout/Test Kit v3 và SHA-256. Tiếp theo: publish/recycle/health, Verify smoke, chạy đúng một
batch v3.1; sau đó benchmark Ollama 8B trên GPU BTC và tổ chức 3 user session.
