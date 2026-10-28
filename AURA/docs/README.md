# Tài liệu AURA

**Trạng thái 28/10/2026:** AURA là supervised MVP đã chạy một ca synthetic trọn luồng trên [SmartASP live demo](https://bondphupham-001-site1.ltempurl.com/). Trial hosting hết hạn **20/11/2026**. Không dùng hóa đơn thật trên site công khai hoặc coi yêu cầu Strict ZDR là chứng nhận bảo vệ dữ liệu.

## Bắt đầu từ đây

| Cần tìm | Tài liệu |
| --- | --- |
| Trạng thái, evidence và giới hạn hiện hành | [CURRENT_PROJECT_STATUS.md](CURRENT_PROJECT_STATUS.md) |
| Kiểm chứng live sau migration và đổi API key | [SMARTASP_LIVE_VALIDATION_2026-10-28.md](SMARTASP_LIVE_VALIDATION_2026-10-28.md) |
| Trình tự backup, biến SmartASP, publish và smoke | [DEPLOYMENT.md](DEPLOYMENT.md) |
| Chạy local, lỗi thường gặp và khôi phục | [RUNBOOK.md](RUNBOOK.md) |
| Thiết lập Drive synthetic trên localhost | [DRIVE_SANDBOX_LOCAL.md](DRIVE_SANDBOX_LOCAL.md) |
| Quy tắc nghiệp vụ | [BUSINESS_RULES.md](../BUSINESS_RULES.md) · [bản giải thích tiếng Việt](BUSINESS_RULES_VI.md) |
| Kịch bản kiểm thử và số liệu theo mốc | [TEST_CASES.md](TEST_CASES.md) · [EVALUATION_EVIDENCE.md](EVALUATION_EVIDENCE.md) |
| Mức sẵn sàng và giới hạn scale | [READINESS_AND_SCALE_ASSESSMENT_2026-10-23.md](READINESS_AND_SCALE_ASSESSMENT_2026-10-23.md) |

Các file khác trong `docs/` là protocol, phương án chưa bật hoặc **evidence theo ngày**. Chúng được giữ để truy vết kết quả và quyết định kiến trúc; ngày, model, số test và trạng thái publish trong đó chỉ đúng tại mốc ghi nhận. Tài liệu proposal, slide và build log đã nộp nằm tại [submission](../submission/README.md); không đọc chúng như thông số runtime của source mới.

Ảnh thật, dữ liệu định danh, raw provider response, SQL backup, secret và tài liệu ôn phản biện cá nhân được giữ ngoài Git. Không đưa các file đó vào `docs/`, `submission/`, gói publish hoặc đường dẫn web công khai.
