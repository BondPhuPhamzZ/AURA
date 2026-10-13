# Checklist vận hành Demo Day AURA

**Cập nhật:** 14/10/2026

## Trước khi quay hoặc thi

1. Dùng tag `aura-demo-day-2026-10-14`; không sửa prompt, policy, threshold, model hoặc schema.
2. Kiểm `/healthz`: HTTP 200, status ok, DB/storage/migration ready, Qwen3.8 Flash, reasoning none, fallback false.
3. Đóng OpenRouter dashboard, hosting portal, terminal có secret, Explorer chứa receipt thật và mọi notification.
4. Mở sẵn AURA, một hồ sơ ESCALATE synthetic, trang Quản lý và Audit.
5. Chỉ một người điều khiển; không chạy upload khác trong lúc Verify.
6. Quay 1920x1080, zoom 100%, con trỏ lớn, âm thanh rõ.

## Kịch bản ưu tiên

- Verify đúng một lần và chờ 5/5.
- Mở một ca ESCALATE, chỉ ra facts, reason và câu hỏi.
- Chuyển tiếp, quản lý quyết định, mở Audit rồi Undo.
- Nêu rõ giới hạn: một ảnh JPG/PNG, fallback live tắt, chưa có production RBAC/PDF/object storage.

## Nếu có sự cố

| Tình huống | Hành động |
|---|---|
| Verify không bắt đầu sau khoảng 10 giây | Dừng take; kiểm health và batch đang chạy, không click liên tục |
| HTTP 409 | Một Verify khác đang chạy; chờ batch hiện tại, không coi là provider failure |
| 429 hoặc timeout | Không spam retry; đợi cooldown hoặc dùng video dự phòng |
| Live URL lỗi | Chuyển video chính thức; chỉ dùng localhost nếu đã chuẩn bị và LocalDB healthy |
| Kết quả ESCALATE ngoài dự kiến | Giải thích fail-safe và mở evidence; không sửa policy tại chỗ |
| Mất Internet | Dùng video; Ollama chỉ dùng khi đã warm và chấp nhận latency |

## Sau khi quay

1. Xem video một lần có âm thanh và một lần tắt tiếng để rà secret/PII trên từng khung hình.
2. Xác nhận thời lượng không quá 180 giây.
3. Upload Team Drive và thử link bằng cửa sổ ẩn danh.
4. Giữ một bản bitrate thấp trên laptop hoặc USB.
5. Không xóa evidence hoặc đổi tag sau khi nộp.
