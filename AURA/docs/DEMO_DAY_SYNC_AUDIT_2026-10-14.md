# Biên bản đồng bộ gói Demo Day ngày 14/10/2026

## Quy tắc đồng bộ

- `Evidence` giữ nguyên ngày phát sinh, nội dung đo và hash; ngày cũ không đồng nghĩa tài liệu đang được dùng làm trạng thái hiện hành.
- `Gia_Phu` chỉ chứa tài liệu học và phản biện bằng tiếng Việt, được tổng hợp theo trạng thái ngày 14/10.
- `Nop` chứa artifact chuẩn bị upload; video và ba-user evidence còn pending phải được ghi rõ, không tạo dữ liệu giả.
- Build Log và proposal tiếng Việt được tái tạo từ source để sửa layout, sau đó PDF được render kiểm tra trực quan và DOCX được audit cấu trúc/accessibility.
- `SHA256SUMS.txt` được tạo lại sau mỗi lần đồng bộ Evidence.

## Nguồn trạng thái hiện hành

1. `CURRENT_PROJECT_STATUS_2026-10-14.md`.
2. `FINAL_LIVE_HEALTH_RECHECK_2026-10-14.md`.
3. `FINAL_LOCAL_GATE_2026-10-14.md`.
4. Tag `aura-demo-day-2026-10-14` và commit được ghi trong `PACKAGE_STATUS.json`.

## Tài liệu lịch sử vẫn được giữ

Các file có ngày 06/10, 07/10, 12/10 hoặc 13/10 trong Evidence chứng minh từng lần kiểm thử, publish, benchmark hoặc holdout. Chúng không được sửa kết quả hay đổi ngày. Khi có số liệu khác nhau, ưu tiên chỉ mục hiện hành để hiểu phạm vi: prospective holdout, post-holdout regression và stability repeat là ba loại phép đo khác nhau.
