# Build Log (Sprint 1)

## Công cụ AI đã sử dụng
- **Gemini 1.5 Flash (via Google AI Studio)**: Dùng để trích xuất hóa đơn thành dạng JSON cấu trúc (Extract Facts).
- **GitHub Copilot / Agent**: Hỗ trợ code và refactoring kiến trúc từ MVC thuần sang kiến trúc tách bạch Extraction và Rule Engine.

## Lợi ích
- Tăng tốc độ code gấp 3 lần.
- Kiến trúc deterministic đảm bảo 100% không bị hallucination ở bước ra quyết định.

## Chi phí & Thời gian
- API Cost:  (Sử dụng Free Tier của Google AI Studio).
- Thời gian setup: ~10 giờ.

## Tính năng lớn nhất đã cắt giảm
- Hỗ trợ PDF nhiều trang.
- Fine-tuning model riêng (Sử dụng zero-shot extraction với cấu trúc JSON strict).
- Hệ thống user authentication (Hiện tại ai cũng có thể Verify để giám khảo dễ test).
