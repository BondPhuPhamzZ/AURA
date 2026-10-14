# Trạng thái hiện hành AURA ngày 14/10/2026

## Kết luận

Candidate kỹ thuật đã đủ điều kiện quay video và demo có giám sát. Release đang đóng băng tại tag `aura-demo-day-2026-10-14`, commit `efc65d835a3b271369ed07c0a129533d1a0b5264`. Không thay prompt, policy, threshold, schema hoặc model trước Demo Day trừ khi tái hiện được blocker và chấp nhận chạy lại toàn bộ gate.

## Cấu hình live

| Thành phần | Giá trị hiện hành |
|---|---|
| Provider gateway | OpenRouter |
| Model | `qwen/qwen3.8-flash` |
| Route | Alibaba |
| Reasoning | `none` |
| Processing | Durable SQL background queue |
| AI concurrency | 1 trong mỗi process |
| Provider cooldown | 120 giây |
| Verify Harness | Bật |
| Live fallback | Tắt; Ollama chỉ là contingency local |

Read-only health recheck ngày 14/10 trả HTTP 200, `status=ok`, database/storage/migration sẵn sàng. Health không gọi inference nên không thay thế Verify gate.

## Gate kỹ thuật đã đạt

- Release build: 0 warning, 0 error.
- Offline tests: 163/163 pass.
- Live Verify: 5/5 trong 65,004 giây.
- Duplicate Verify clients: 1 HTTP 200 và 4 HTTP 409 trên một process.
- Năm upload khác nhau: 5/5 hoàn tất, không mất job; E2E P95 93,684 giây do concurrency một.
- Working tree và GitHub đã đồng bộ tại tag đóng băng.

## Evidence đánh giá

| Tập | Kết quả | Ranh giới claim |
|---|---|---|
| Prospective mini-holdout 10 | 8/10, 0 missed, 2 over, 0 system/provider/429, 28/30 field | Một official run; mẫu nhỏ có redaction; không phải production accuracy |
| Regression 12 original bytes | 10/12, 0 missed, 2 over, 35/36 field | Dữ liệu đã biết; stability/concurrency only |
| Sequential repeat 12 | 11/12, 0 missed, 1 over, P95 32,903 giây | Không phải holdout mới |

FAHASA còn over ổn định do validator chưa chấp nhận chắc chắn nhãn `Thanh toán 91.500 đ` là final-payable. Không vá keyword sát ngày thi vì có nguy cơ nhầm tender, tiền khách đưa hoặc tiền thối. Evidence localization vẫn là prototype cô lập và chưa ảnh hưởng pipeline/policy.

## Gate con người còn mở

1. Quay video chính thức tối đa 180 giây và rà secret/PII theo từng khung hình.
2. Hoàn tất ba phiên user study thật, có consent, quote nguyên văn, một thay đổi truy vết được và một negative effect.
3. Upload Team Drive, kiểm quyền bằng cửa sổ ẩn danh và xác nhận deadline mới nhất.
4. Rehearsal có bấm giờ trên đúng release đóng băng.

## Cách đọc ngày trong gói Demo Day

Các gói evidence có timestamp ngoài repository là bằng chứng lịch sử bất biến, không được sửa ngày để tạo cảm giác đây là phép đo mới. Nguồn trạng thái hiện hành là tài liệu này; bản tổng hợp số liệu công khai nằm tại `EVALUATION_EVIDENCE.md`. Bộ `personal/` chỉ chứa tài liệu ôn tập tiếng Việt, không phải evidence đánh giá.
