# AURA — Roadmap 07/10/2026

## P0 — đóng candidate policy/vision

1. Giữ nguyên v3/v3.1 images, manifest và SHA-256.
2. Publish candidate 140/140 test; recycle/health; chạy Verify smoke.
3. Chạy đúng một batch live v3.1. Gate: completed 15/15, system error 0, missed escalation 0.
4. Nếu fail, phân loại extraction/model/semantic/policy/fixture từ raw facts; không sửa expected theo output.

## P1 — GPU BTC và fallback có kiểm soát

1. Chạy Ollama-only, database/storage/port riêng, 8B Q4, context 16384/output 4096.
2. Chứng minh processor bằng `ollama ps`; ghi GPU/VRAM bằng `nvidia-smi`.
3. Lưu token usage, semantic repair, latency, decision/field score và mọi error.
4. Chỉ sau khi Ollama-only đạt gate mới chạy một fallback smoke; baseline live vẫn OpenRouter,
   `FallbackEnabled=false` trong benchmark chính.

## P1 — demo và người dùng

1. Ba user session có consent, task, thời gian, lỗi quan sát và feedback do user xác nhận.
2. Ba rehearsal cùng commit freeze; không đổi code ở lượt cuối.
3. Chỉ public URL dưới nhãn supervised MVP/demo; không quảng bá unattended production service.

## P2 — sau Chung kết

PDF/multi-page, RBAC, retention/deletion, object storage, rate limiting và monitoring là backlog
production; không mở rộng trước khi P0/P1 hiện tại đóng.
