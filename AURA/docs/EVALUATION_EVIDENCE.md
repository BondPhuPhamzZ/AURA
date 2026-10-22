# Tổng hợp bằng chứng đánh giá AURA

**Mốc evidence hiện hành:** 14/10/2026

**Mục đích:** tạo một chỉ mục công khai duy nhất cho các phép đo được dùng trong proposal, slide, Build Log, README và gói Demo Day.

## Baseline hiện hành

| Nhóm evidence | Kết quả đã xác minh | Ranh giới claim |
|---|---|---|
| Release build | 0 warning, 0 error | Chỉ là gate build |
| Bộ test offline | 163/163 pass | Test code, không phải 163 hóa đơn |
| Verify Harness live | 5/5 quyết định kỳ vọng trong 65,004 giây | Đạt gate demo 5 ca dưới 90 giây trên lần đo này |
| Nhiều client bấm Verify trùng | 1 HTTP 200 + 4 HTTP 409 | Idempotency trong một process, không phải distributed locking |
| Candidate Qwen3.8 Flash | OpenRouter route tới Alibaba; reasoning `none`; fallback tắt | Candidate demo có giám sát đã đóng băng |
| Mini-holdout prospective | 8/10 exact, 0 missed, 2 over, 0 lỗi system/provider/429, 28/30 field trọng yếu | Một lần chạy nhỏ trên ảnh đã redaction, không phải production accuracy |
| Regression trên 12 source bytes | 10/12 exact, 0 missed, 2 over, 35/36 field trọng yếu | Evidence ổn định trên known data |
| Lặp tuần tự cùng 12 ảnh | 11/12 exact, 0 missed, 1 over, P95 32,903 giây | Không phải holdout mới |
| Năm upload khác nhau | 5/5 về trạng thái cuối; E2E P95 93,684 giây | Test queue integrity ở AI concurrency 1, không phải Verify SLA |
| Human workflow | Synthetic escalation, quyết định quản lý, Audit và Undo đều pass | Evidence workflow, không phải xác nhận từ người dùng thật |
| Evidence localization | Prototype overlay synthetic gắn với hash | Chưa nối extraction hoặc policy; chưa có benchmark annotation unseen |

## Bằng chứng lịch sử được giữ ngoài cây docs hiện hành

Các gói raw có timestamp vẫn nằm trong workspace evidence riêng tư và release history. Những phép đo lịch sử chính gồm:

- synthetic judge set đã khóa: OpenRouter 15/15 quyết định và Ollama 14/15;
- Test Kit v3 live: 11/15 quyết định, 142/145 field, 0 system error;
- Test Kit v3.1: 13/15 exact, 0 missed escalation, 0 system error;
- real holdout khóa một lần: raw 10/15 với một missed và ba over; phân tích adjudicated là 11/15 sau khi phát hiện một label sai mà không đổi raw evidence;
- benchmark Ollama 4B 16K cô lập: 15/15 quyết định synthetic nhưng processing P95 213,429 giây;
- so sánh Qwen3.8 Flash hậu chỉnh: 15/15 trên known receipts, sau đó mới chạy mini-holdout prospective độc lập 8/10 ở trên.

Các phép đo này không thể thay thế lẫn nhau. Regression sau không được ghi đè kết quả holdout trước.

## Các mismatch đã biết

- FAHASA vẫn là fail-safe over-escalation ổn định vì validator chưa xem nhãn `Thanh toán 91.500 đ` là evidence tổng cuối đủ chắc chắn.
- GS25_2 cho thấy extraction variance giữa các lần lặp known data.
- Three O'Clock cho thấy vùng privacy blackout cùng free-text warning có thể kích hoạt FACT review dù core fields đúng.

Dự án chủ động không nới final-total guard ngay trước ngày chấm vì việc đó có thể biến tiền khách đưa, cash received hoặc tiền thối thành payable amount được duyệt.

## Quy tắc toàn vẹn raw evidence

1. Giữ nguyên timestamp, SHA-256 manifest, expected label và raw outcome.
2. Không đưa hóa đơn, provider payload, secret, database hoặc ảnh API-key dashboard vào repository công khai.
3. Chỉ sửa lỗi hiển thị encoding/parser từ raw body đã giữ; ghi lại cả hai hash và không replay provider để ghi đè outcome.
4. Áp dụng yêu cầu dưới 90 giây cho Verify Harness 5 ca, không áp cho test concurrency năm người upload.
5. Không claim user evidence ba người cho tới khi có đủ record có consent, trích dẫn được xác nhận, một thay đổi truy vết được và một tác động tiêu cực.
6. Dùng `CURRENT_PROJECT_STATUS.md` cho trạng thái hiện tại; dùng Git history và release tag đóng băng cho báo cáo theo ngày.

## Bản đồ evidence riêng tư

Bản Demo Day đã tuyển chọn và gói nguồn được lưu trong các workspace riêng, ngoài repository công khai; ảnh hóa đơn thật và raw provider material bị loại khỏi bản tuyển chọn. Release tag và Git history giữ nguyên các snapshot công khai đã dọn khỏi cây hiện hành khi cần audit câu chữ cũ.
