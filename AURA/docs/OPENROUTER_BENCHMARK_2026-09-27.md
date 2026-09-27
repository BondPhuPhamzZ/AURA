# OpenRouter benchmark — 27/09/2026

## Phạm vi và nguồn bằng chứng

Benchmark này chạy trên cùng build `3dec6e4`, cùng JSON Schema, semantic validator và `PolicyDecisionEngine` đang dùng trong Sprint 1. Provider là OpenRouter, model `qwen/qwen3-vl-8b-instruct`, `temperature=0`, timeout 90 giây. Ảnh và expected result đều là fixture tổng hợp đã khóa; vì vậy kết quả chỉ chứng minh pipeline có thể tái lập trên bộ kiểm soát, không phải độ chính xác trên hóa đơn thực tế độc lập.

Bằng chứng gốc được giữ ngoài Git để tránh đưa ảnh/log vận hành vào repository:

- Upload thủ công 3 lượt: `D:\aura\benchmark_2times_img\OpenRouter_test\real\F12_3times`.
- Verify Harness 3 batch: `D:\aura\benchmark_2times_img\OpenRouter_test\verify\f12_test`.

Tên folder `real` chỉ biểu thị đường upload thủ công qua UI; ảnh `HoaDon1.jpg` vẫn là fixture tổng hợp trong `wwwroot/test_data`, không phải hóa đơn thực tế độc lập.

## Upload thủ công `HoaDon1.jpg`

| Lượt | Quyết định | Backend latency | Network duration | Các field khóa |
|---:|---|---:|---:|---|
| 1 | `AUTO_APPROVE` | 10.507 s | 10.81 s | Đúng |
| 2 | `AUTO_APPROVE` | 5.052 s | 5.08 s | Đúng |
| 3 | `AUTO_APPROVE` | 6.431 s | 6.44 s | Đúng |

- 3/3 quyết định đúng và đồng nhất.
- Backend latency: min 5.052 s, median 6.431 s, mean 7.330 s, P95 10.099 s, max 10.507 s.
- Network overhead so với latency backend lần lượt khoảng 303 ms, 28 ms và 9 ms.
- Các field khóa đều khớp: loại `ECOMMERCE`, merchant `Double Fish Việt Nam`, tổng tiền 295,199 VND, ngày 18/09/2026 và mã vận đơn `SPX-VN2693231211394`.
- `DuplicateDetected=true` vì database benchmark đã có ảnh cùng SHA-256; `DuplicatePolicyEnabled=false`, nên đây không phải lỗi model hoặc lý do chuyển tiếp.

## Verify Harness — 15 lần suy luận

Ba batch liên tiếp, mỗi batch gồm 5 fixture và có delay 4 giây giữa TC-02 đến TC-05 để giảm rate-limit.

| Chỉ số quyết định | Kết quả |
|---|---:|
| Tổng PASS | 15/15 |
| Routine được auto-approve | 9/9 |
| FACT escalation đúng | 3/3 |
| POLICY escalation đúng | 3/3 |
| Over-escalation trên bộ khóa | 0/9 |
| Missed escalation trên bộ khóa | 0/6 |
| System error | 0/15 |

| Batch | F12 duration | Tổng raw latency | Tổng adjusted latency |
|---:|---:|---:|---:|
| 1 | 44.17 s | 44.106 s | 28.106 s |
| 2 | 42.92 s | 42.880 s | 26.880 s |
| 3 | 50.81 s | 50.781 s | 34.781 s |

`AdjustedLatencyMs = RawLatencyMs - HarnessDelayMs`; mỗi batch trừ 16,000 ms do bốn khoảng nghỉ 4 giây. Trên 15 ca adjusted: min 2.954 s, median 4.371 s, mean 5.984 s, P95 11.317 s và max 11.475 s. P95 dùng nội suy tương đương `PERCENTILE.INC`.

F12 duration gần như trùng tổng raw latency: overhead batch chỉ khoảng 64 ms, 40 ms và 29 ms. Điều này cho thấy thời gian chủ yếu nằm trong pipeline server/provider, không phải render UI.

## Exact match theo field

Ground truth lấy từ `test_kit/manifest.json`, 5 field khóa trên 15 lần suy luận:

| Field | Exact match |
|---|---:|
| Merchant | 15/15 |
| Amount | 15/15 |
| Date | 15/15 |
| Identifier | 15/15 |
| Document type | 12/15 |
| Tổng | 72/75 = 96% |

Sai khác duy nhất là TC-04: model trả `RESTAURANT_BILL`, manifest khóa `RETAIL_RECEIPT` trong cả ba lượt. Merchant, amount, date và identifier vẫn đúng, và policy vẫn đi đúng `AUTO_APPROVE`. Vì chứng từ mang nội dung nhà hàng, đây có thể là vấn đề taxonomy/nhãn mơ hồ; tuy nhiên theo manifest đã khóa vẫn phải báo là mismatch, không được sửa expected result sau khi thấy output.

Giá trị `confidence` do model tự báo không phải xác suất đã hiệu chỉnh, nên không dùng làm bằng chứng accuracy độc lập.

## Kết luận lựa chọn provider

- OpenRouter 8B giữ vai trò mặc định cho demo/Sprint 1: cùng build đạt 15/15 quyết định, upload 3/3 và adjusted P95 khoảng 11.317 giây, thấp hơn giới hạn 90 giây của luồng demo.
- Ollama 4B giữ vai trò local tùy chọn cho privacy/offline và học tập: đạt 25/25 quyết định trên cùng 5 fixture nhưng ba batch có đo chi tiết mất khoảng 303–304 giây mỗi batch trên RTX 3050 Laptop 4 GB.
- Đây là lựa chọn theo bối cảnh phần cứng, độ trễ và vận hành; chưa có cơ sở kết luận OpenRouter 8B chính xác hơn trên dữ liệu thực.
- AURA hiện không tự động fallback giữa hai provider. Mỗi lần chạy dùng đúng provider cấu hình; lỗi được audit và chuyển `ESCALATE_SYSTEM_ERROR`.

## Khoảng trống trước pilot thực tế

1. Tập độc lập tối thiểu 100 ảnh đã ẩn danh và được hai người gán nhãn; không dùng lại fixture dùng để sửa prompt/policy.
2. Đo exact match/precision/recall theo field, P50/P95 latency, missed escalation, over-escalation và khoảng tin cậy.
3. Lưu OpenRouter Activity metadata: token, cost, served provider và generation id; benchmark hiện chưa có các trường này.
4. Thu server log về schema/semantic repair, lỗi 429/5xx và số lần repair; F12 response không đủ chứng minh các nhánh này.
5. Kiểm thử mạng mất/chậm, provider outage, concurrency, privacy/retention và 15 ca tham chiếu BGK qua pipeline live.

Không dùng 15/15 hoặc 96% ở trên làm “độ chính xác sản phẩm”. Cách diễn đạt đúng là **decision consistency trên fixture tổng hợp đã khóa** và **field exact match trên 15 lần suy luận của 5 ảnh lặp lại**.
