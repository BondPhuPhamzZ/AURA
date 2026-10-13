# AURA — Prospective real-receipt mini-holdout 10 ca ngày 13/10/2026

## Kết luận

AURA đã chạy đúng một lượt chính thức trên 10 hóa đơn thật mới do chủ project sở hữu, đã đồng thuận sử dụng, redaction và khóa nhãn/hash trước request đầu tiên. Live candidate dùng OpenRouter/Qwen3.8 Flash, route Alibaba, `reasoning=none`, AI concurrency 1 và fallback tắt.

- Quyết định đúng tuyệt đối: **8/10 (80%)**.
- Sai số an toàn: **0 missed-escalation**, **2 over-escalation**.
- Độ ổn định: **10/10 hoàn tất**, **0 system error**, **0 lỗi 429/provider quan sát được**, **0 fallback**, **0 duplicate**.
- Ba field trọng yếu `date + canonical identifier + final total`: **28/30**; riêng final total đạt **10/10**.
- Kỷ luật chạy: mỗi ảnh upload đúng một lần, không rerun để săn kết quả.

Đây là prospective mini-holdout độc lập ở quy mô nhỏ, không phải production accuracy hoặc bằng chứng cho unattended rollout. Kết quả hỗ trợ quyết định supervised demo và chỉ ra hai false-negative cần sửa có kiểm soát sau khi khóa raw evidence.

## Ma trận quyết định

| Expected đã khóa | `AUTO_APPROVE` | `ESCALATE_FACT` | `ESCALATE_POLICY` | `SYSTEM_ERROR` |
|---|---:|---:|---:|---:|
| `AUTO_APPROVE` (4) | 2 | 2 | 0 | 0 |
| `ESCALATE_FACT` (5) | 0 | 5 | 0 | 0 |
| `ESCALATE_POLICY` (1) | 0 | 0 | 1 | 0 |

Hai mismatch đều fail-safe:

1. `H10-02 / FAHASA`: model đọc đúng ngày, định danh, thuế và tổng tiền, nhưng deterministic evidence validator không chấp nhận nhãn in `Thanh toán 91.500 đ` là final-total label đáng tin cậy; confidence sau repair còn 0,69 nên chuyển FACT.
2. `H10-03 / GS25_2`: model để `merchantName=null` dù logo GS25 còn nhìn thấy, đồng thời cảnh báo vùng che gần logo; ngày, định danh và tổng tiền vẫn đúng. Đây là tương tác vision/redaction, không phải 429.

`H10-10 / PhuongNam` vẫn đúng quyết định FACT nhưng không phải extraction evidence sạch: vùng che an toàn đã cắt mất phần đầu ngày và model suy diễn một mã chất lượng thấp thành receipt number. Raw được giữ nguyên và ca không được gửi lại.

## Latency và capacity

- Processing AI: P50 **7,648 giây**, P95/max **22,806 giây**.
- End-to-end tính từ lúc từng request được nhận, gồm queue wait: P50 **64,763 giây**, P95/max **108,803 giây**.
- Năm request đầu đạt trạng thái cuối trong **64,763 giây**; cả 10 request trong **108,803 giây**.
- Runner wall-clock gồm setup và health cuối: **123,472 giây**.

Phép đo này chứng minh durable queue nhận và xử lý burst 10 ca ở concurrency 1. Nó không thay cổng Verify 5 ca chuyên dụng; kết quả Verify live hiện hành vẫn là 5/5 trong 65,004 giây.

## Field audit

| Field | Exact |
|---|---:|
| Ngày | 9/10 |
| Định danh canonical | 9/10 |
| Tổng thanh toán | 10/10 |
| Tổng ba field trọng yếu | 28/30 |
| Tiền tệ | 10/10 |
| Document type | 7/10 |

`H10-01` và `H10-09` vẫn đúng quyết định FACT nhưng cũng cho thấy validator nhãn final-total đang chặt hơn cần thiết. Đây là backlog giảm over-escalation; không được nới guard chỉ để cải thiện con số nếu chưa có test âm chống auto-approve sai.

## Evidence và tính toàn vẹn

Gói evidence riêng tư nằm ngoài Git:

`D:\aura\demo_evidence\22_NewRealReceipt_ForHoldout\holdout10_evidence_v3_2026-10-13`

Các thành phần cốt lõi:

- manifest nhãn và SHA-256 khóa trước chạy;
- exact provider-ready bytes đã redaction;
- raw HTTP 202/status/facts/decision cho 10 case;
- bảng latency, confusion matrix, field comparison và mismatch register;
- checksum trước/sau chạy;
- bản tóm tắt UTF-8 hiệu chỉnh chỉ sửa trình bày, không replay provider hoặc sửa raw.

Ảnh receipt, raw response và dữ liệu vận hành không được commit lên repository công khai.

## Quyết định tiếp theo

- **GO** cho supervised live demo một operator trên candidate đã freeze, kèm video/evidence dự phòng.
- **NO-GO** cho production accuracy claim hoặc unattended rollout.
- Sau submission freeze, thêm regression test âm/dương trước khi mở rộng từ điển final-total label; điều tra merchant/logo gần vùng redaction bằng tập fixture riêng.
- Không rerun 10 ảnh này như một holdout mới. Mọi lượt sau phải gọi đúng là post-holdout regression.

## Addendum 2 ca khóa sau đó

Chidori và Three O'Clock được thêm sau khi 10 ca trên đã hoàn tất, nên được khóa/chạy riêng như prospective addendum, không nhập ngược để tạo claim “holdout 12 ca mới”. Addendum đạt **1/2 exact, 0 missed, 1 over, 0 system/provider/429 error**; cả hai terminal trong 23,020 giây. Chidori đúng FACT vì ngày Chủ nhật. Three O'Clock có facts cốt lõi đúng nhưng warning `che mờ bằng dải đen` kích hoạt FACT. Combined descriptive view là **9/12**, chỉ dùng như tổng hợp mô tả và không thay raw 8/10 của tài liệu này.
