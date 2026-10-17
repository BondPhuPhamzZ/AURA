# Trạng thái hiện hành AURA ngày 17/10/2026

## Kết luận

Baseline live ngày 14/10 vẫn truy xuất được tại tag `aura-demo-day-2026-10-14`. Sau feedback doanh nghiệp ngày 17/10, candidate source đã bổ sung batch ingestion, số tiền AI suy ra có provenance, privacy triage và dashboard vận hành. Các thay đổi này đã qua toàn bộ test offline nhưng **chưa được gọi là live** cho tới khi publish, health check và synthetic smoke hoàn tất. Video cũ vẫn là bằng chứng của baseline trước feedback; không dùng video đó để khẳng định UI mới đã chạy trên production.

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
| Batch upload | Tối đa 10 JPG/PNG; mỗi ảnh tối đa 5 MB và tạo một job riêng |
| Amount | Người dùng nhập cho single upload hoặc AI suy ra từ `totalAmount` đã validation |
| Provider privacy | `data_collection=deny`; strict ZDR mặc định tắt chờ xác nhận route live |
| Privacy route | `ESCALATE_PRIVACY`; triage category, không lặp lại giá trị gốc |

Read-only health recheck ngày 14/10 trả HTTP 200, `status=ok`, database/storage/migration sẵn sàng. Health không gọi inference nên không thay thế Verify gate.

## Gate kỹ thuật đã đạt

- Debug build: 0 warning, 0 error.
- Offline tests sau feedback: 178/178 pass.
- Live Verify: 5/5 trong 65,004 giây.
- Duplicate Verify clients: 1 HTTP 200 và 4 HTTP 409 trên một process.
- Năm upload khác nhau: 5/5 hoàn tất, không mất job; E2E P95 93,684 giây do concurrency một.
- Live Verify 5/5 là bằng chứng baseline ngày 14/10, không phải phép đo lại prompt privacy ngày 17/10.

## Thay đổi sau feedback doanh nghiệp

1. **Nhiều hóa đơn:** UI nhận tối đa 10 ảnh và gọi tuần tự cùng endpoint HTTP 202. Mỗi ảnh vẫn qua kiểm extension/MIME/magic bytes/5 MB, private storage, SHA-256, SQL row, lease và audit độc lập. Danh sách job được lưu sau từng response 202 để lỗi giữa batch không làm mất job đã nhận.
2. **Không bắt buộc nhập tiền:** claimed amount trở thành tùy chọn. Nếu bỏ trống, worker chỉ dùng `facts.TotalAmount` do model trả về sau JSON Schema và semantic validation; nguồn `USER_PROVIDED`, `AI_EXTRACTED` hoặc `UNAVAILABLE` được lưu trong facts/audit. Thiếu total vẫn fail-safe về kiểm tra dữ kiện.
3. **Dashboard quản lý:** tab Tổng quan tổng hợp tháng hiện tại và từng ngày theo giờ Việt Nam từ SQL, gồm tiếp nhận, hoàn tất, tự duyệt, chuyển người, privacy review, lỗi, tổng giá trị và độ trễ.
4. **Privacy triage:** model chỉ gắn marker category trong `suspiciousSignals`; backend normalize category, không ghi lại giá trị cá nhân, và policy C# chuyển `ESCALATE_PRIVACY`. Request OpenRouter mặc định yêu cầu provider không dùng dữ liệu để thu thập/huấn luyện.
5. **Email/folder connector:** chưa nối trong candidate hôm nay. Kênh này cần OIDC/OAuth, least privilege, consent, idempotency, backfill, quarantine và audit; khi đủ điều kiện sẽ tái sử dụng ingestion boundary hiện có.

Privacy triage là công cụ hỗ trợ rà soát, không phải kết luận pháp lý hay chứng nhận tuân thủ. Strict zero-data-retention chỉ được bật sau khi route/provider live được xác nhận hỗ trợ; nếu bật mù có thể làm không còn provider đủ điều kiện.

## Evidence đánh giá

| Tập | Kết quả | Ranh giới claim |
|---|---|---|
| Prospective mini-holdout 10 | 8/10, 0 missed, 2 over, 0 system/provider/429, 28/30 field | Một official run; mẫu nhỏ có redaction; không phải production accuracy |
| Regression 12 original bytes | 10/12, 0 missed, 2 over, 35/36 field | Dữ liệu đã biết; stability/concurrency only |
| Sequential repeat 12 | 11/12, 0 missed, 1 over, P95 32,903 giây | Không phải holdout mới |

FAHASA còn over ổn định do validator chưa chấp nhận chắc chắn nhãn `Thanh toán 91.500 đ` là final-payable. Không vá keyword sát ngày thi vì có nguy cơ nhầm tender, tiền khách đưa hoặc tiền thối. Evidence localization vẫn là prototype cô lập và chưa ảnh hưởng pipeline/policy.

## Gate con người còn mở

1. Publish/recycle candidate sau feedback, kiểm `/healthz`, database migration và một synthetic smoke cho single/batch; không rerun holdout cũ để tạo accuracy claim mới.
2. Xác nhận route Alibaba/OpenRouter vẫn nhận `data_collection=deny`; chỉ cân nhắc bật strict ZDR sau một probe có kiểm soát.
3. Hoàn tất ba phiên user study thật, có consent, quote nguyên văn, một thay đổi truy vết được và một negative effect.
4. Rehearsal có bấm giờ theo demo mới: dashboard → batch không nhập tiền → privacy escalation → manager → audit.

## Cách đọc ngày trong gói Demo Day

Các gói evidence có timestamp ngoài repository là bằng chứng lịch sử bất biến, không được sửa ngày để tạo cảm giác đây là phép đo mới. Nguồn trạng thái hiện hành là tài liệu này; bản tổng hợp số liệu công khai nằm tại `EVALUATION_EVIDENCE.md`. Tài liệu ôn tập, kịch bản và Q&A cá nhân được giữ ngoài repository tại `D:\aura\phan_bien`; chúng không phải evidence đánh giá hay hồ sơ nộp.
