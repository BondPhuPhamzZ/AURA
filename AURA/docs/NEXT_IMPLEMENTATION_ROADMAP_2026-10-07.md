# AURA — Roadmap 07/10/2026

## P0 — candidate policy/vision đã đóng safety gate

Live v3.1 hoàn tất 15/15, exact 13/15, system error 0, missed escalation 0 và field
UTF-8-safe 140/146. Giữ raw, manifest và SHA-256; không chạy lại. Hai mismatch là duplicate
identifier fail-safe, không phải policy missed escalation. Xem `LIVE_REGRESSION_V31_RESULT_2026-10-07.md`.

V2 hậu-contract hoàn tất 15/15 request nhưng chỉ 14/15 exact do missed TK-12 blur cũ; 0 over,
0 system error, field 69/75. Không sửa policy hoặc chạy lại theo fixture này. Xem
`OPENROUTER_POSTCONTRACT_V2_RESULT_2026-10-07.md`; v3.1 tiếp tục là gate hiện hành.

## P1 — GPU BTC và fallback có kiểm soát

1. Chạy Ollama-only, database/storage/port riêng, 8B Q4, context 16384/output 4096.
2. Chứng minh processor bằng `ollama ps`; ghi GPU/VRAM bằng `nvidia-smi`.
3. Lưu token usage, semantic repair, latency, decision/field score và mọi error.
4. Chỉ sau khi Ollama-only đạt gate mới chạy một fallback smoke; baseline live vẫn OpenRouter,
   `FallbackEnabled=false` trong benchmark chính.

Runbook hiện hành dùng đúng `test_kit/v3_1`, không dùng v2 hay receipt thật trên máy BTC. Đặt
lịch ngay để còn biên sửa blocker trước hạn nộp 14/10.

## P1 — demo và người dùng

Live Verify 5/5, workflow forward/accept/undo và desktop/mobile UI smoke đã pass sau publish.

1. Ba user session có consent, task, thời gian, lỗi quan sát và feedback do user xác nhận.
2. Ba rehearsal cùng commit freeze; không đổi code ở lượt cuối và không chạy lại
   holdout/v2/v3/v3.1. Chỉ chạy Verify 5 ca và workflow/Audit smoke.
3. Chỉ public URL dưới nhãn supervised MVP/demo; không quảng bá unattended production service.

Nếu muốn chạy lại official 15 receipt, phải dùng tên `post-holdout real-receipt regression`,
consent mới và output folder mới; không ghi đè official raw. Receipt mới giữ unseen cho blind
holdout v2. Xem `REAL_RECEIPT_REGRESSION_PROTOCOL_2026-10-07.md`.

## P2 — sau Chung kết

PDF/multi-page, RBAC, retention/deletion, object storage, rate limiting và monitoring là backlog
production; không mở rộng trước khi P0/P1 hiện tại đóng.
