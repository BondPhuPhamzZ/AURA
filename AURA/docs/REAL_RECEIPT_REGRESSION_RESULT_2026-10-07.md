# AURA — Kết quả post-holdout real-receipt regression 07/10/2026

## Phạm vi và tính toàn vẹn

Đây là lần chạy lại đúng 15 receipt thật đã dùng trong official holdout. Tập này **không còn
blind**; mục tiêu là kiểm tra bản evidence-contract/policy hiện hành trên cùng input. Raw official
10/15 và adjudicated view 11/15 được giữ bất biến.

- Live URL: `https://bondphupham-001-site1.ltempurl.com`.
- Provider/model: OpenRouter / `qwen/qwen3-vl-8b-instruct`.
- Fallback: tắt; 0 attempt, 0 success.
- Health trước/sau: `ok`, DB/storage true, pending migration 0.
- Official manifest SHA-256:
  `B13EB77621A89DCFABC3E09F27810F9E7921F5E15433F03E3F9250115BA9CDE9`.
- Regression adjudicated manifest SHA-256:
  `850479750F65BB201ABBE9CAEDF7F4ED5C859A39110CB05C959F8185F2476FB0`.
- Ground truth regression dùng adjudicated v1 khóa trước request: chỉ BH-04 đổi expected từ
  AUTO sang FACT vì ảnh in ngày Chủ nhật 04/10/2026.

Evidence private:

`D:\aura\demo_evidence\23_real_receipt_regression_2026-10-07`

## Kết quả

| View | Exact | Missed escalation | Over-escalation | System error |
|---|---:|---:|---:|---:|
| Official raw | 10/15 = 66,67% | 1/9 | 3/6 | 0 |
| Official adjudicated | 11/15 = 73,33% | 1/10 | 2/5 | 0 |
| Regression hiện tại | **11/15 = 73,33%** | **0/10** | **4/5** | **0** |

Field metric vẫn 28/30 = 93,33%, nhưng chỉ đo `currency` và `totalAmount`; không phải full-schema
extraction accuracy. Accepted P50/P95 là 657/2.051 ms; end-to-end P50/P95 là
12.348/26.277 ms. Latency khác official run nhưng hai lượt khác thời điểm/tải hệ thống nên không
suy ra contract là nguyên nhân.

## Kết quả từng ca

| Case | Expected adjudicated | Actual | Pass |
|---|---|---|---:|
| BH-01 | AUTO_APPROVE | ESCALATE_FACT | Không |
| BH-02 | ESCALATE_FACT | ESCALATE_FACT | Có |
| BH-03 | AUTO_APPROVE | ESCALATE_FACT | Không |
| BH-04 | ESCALATE_FACT | ESCALATE_FACT | Có |
| BH-05 | ESCALATE_FACT | ESCALATE_FACT | Có |
| BH-06 | ESCALATE_POLICY | ESCALATE_POLICY | Có |
| BH-07 | ESCALATE_FACT | ESCALATE_FACT | Có |
| BH-08 | ESCALATE_FACT | ESCALATE_FACT | Có |
| BH-09 | ESCALATE_FACT | ESCALATE_FACT | Có |
| BH-10 | AUTO_APPROVE | AUTO_APPROVE | Có |
| BH-11 | ESCALATE_FACT | ESCALATE_FACT | Có |
| BH-12 | ESCALATE_FACT | ESCALATE_FACT | Có |
| BH-13 | ESCALATE_FACT | ESCALATE_FACT | Có |
| BH-14 | AUTO_APPROVE | ESCALATE_FACT | Không |
| BH-15 | AUTO_APPROVE | ESCALATE_FACT | Không |

## Thay đổi quan trọng so với official run

- **BH-07 fixed critical fail-open:** total cuối bị rách nay đi FACT thay vì AUTO. Contract v2
  không cho phép suy tổng từ subtotal/items khi không có printed-final-total evidence.
- **BH-06 đúng branch:** giờ có giây được canonicalize và bia đi POLICY thay vì FACT.
- **BH-04:** FACT đúng adjudicated ground truth do Chủ nhật; official raw label vẫn không đổi.
- **BH-03 new safe over-escalation:** model lặp/đếm line item thành 24.000 trong khi total là
  12.000 và không đọc discount; semantic repair không giải quyết nên FACT.
- **BH-14 new safe over-escalation:** PTT bị gán đồng thời vào receipt/reference; duplicate
  identifier validator fail-safe.
- **BH-01 persistent safe over-escalation:** model vẫn nhầm tiền mặt/tiền thối/VAT thành
  total/discount; semantic guard chặn AUTO.
- **BH-15 persistent safe over-escalation:** PTT tiếp tục bị lặp vào hai identifier.

Exact accuracy không tăng so với adjudicated view, nhưng safety profile tốt hơn: missed giảm từ
1 xuống 0, đổi lại over-escalation tăng từ 2 lên 4. Đây là hướng **an toàn hơn nhưng cần người
duyệt nhiều hơn**, không phải production accuracy.

## Quyết định

- Safety gate `0 missed + 0 system error`: PASS.
- Supervised demo: CONDITIONAL GO.
- Unattended production/accuracy claim: NO-GO.
- Không nới duplicate/money/line-item validators trước hạn nộp chỉ để tăng AUTO rate.
- Receipt mới giữ unseen cho một blind holdout v2 riêng; không trộn vào regression này.

## Một ảnh cho mỗi request

UI hiện có đúng một `receiptFile` và một `claimedAmount` cho mỗi request. Đây là thiết kế đúng vì
mỗi receipt cần claim, expected facts, quyết định và audit riêng. Runner tự động gửi tuần tự 15
request độc lập; không gộp 15 ảnh vào một hồ sơ.
