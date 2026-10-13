# AURA — Regression lặp lại 12 ảnh gốc, chạy tuần tự — 13/10/2026

## Ranh giới phép đo

Đây là lượt **post-holdout regression lặp lại** trên 12 receipt đã biết. Lượt này không phải blind
holdout mới, không thay prospective 10+2 và không tạo thêm accuracy claim độc lập. Chủ dữ liệu đã
yêu cầu chạy lại 12 ca không có concurrency. Runner gửi trực tiếp source bytes theo SHA-256 đã khóa,
không copy/crop/resize/snapshot/annotate và không đổi prompt, policy, threshold, schema hoặc model.

- Manifest SHA-256: `DF09386196ABA789E32180FD6F5AD6C2176BC77C8CB2F52D2241BA23BF86D5E4`.
- Execution: `SEQUENTIAL_12`, một ca tại một thời điểm.
- Live config: OpenRouter `qwen/qwen3.8-flash`, reasoning `none`, fallback `false`, AI concurrency `1`.
- Health trước và sau: `ok`; DB/storage/migration sẵn sàng.
- Stop rule: dừng trước các ca còn lại nếu xuất hiện system/provider/client error.
- 12/12 được đánh dấu duplicate vì chính source bytes này đã chạy trước; demo policy ghi nhận nhưng
  không escalation duplicate. Đây là kết quả mong đợi của một lượt regression lặp lại.

## Kết quả

| Ca | File | Expected | Actual | Exact | E2E |
|---|---|---|---|---:|---:|
| R12-01 | EARLYMORNING | `ESCALATE_FACT` | `ESCALATE_FACT` | yes | 32.903 s |
| R12-02 | FAHASA | `AUTO_APPROVE` | `ESCALATE_FACT` | no — over | 24.396 s |
| R12-03 | GS25_2 | `AUTO_APPROVE` | `AUTO_APPROVE` | yes | 16.161 s |
| R12-04 | GS25_1 | `ESCALATE_POLICY` | `ESCALATE_POLICY` | yes | 18.555 s |
| R12-05 | Lasimi | `AUTO_APPROVE` | `AUTO_APPROVE` | yes | 19.085 s |
| R12-06 | SundayBasic | `AUTO_APPROVE` | `AUTO_APPROVE` | yes | 25.704 s |
| R12-07 | NgoGia | `ESCALATE_FACT` | `ESCALATE_FACT` | yes | 24.812 s |
| R12-08 | Osaka | `ESCALATE_FACT` | `ESCALATE_FACT` | yes | 17.651 s |
| R12-09 | Pharmacity | `ESCALATE_FACT` | `ESCALATE_FACT` | yes | 22.347 s |
| R12-10 | PhuongNam | `ESCALATE_FACT` | `ESCALATE_FACT` | yes | 28.671 s |
| R12-11 | Chidori | `ESCALATE_FACT` | `ESCALATE_FACT` | yes | 30.268 s |
| R12-12 | ThreeO_Clock | `AUTO_APPROVE` | `AUTO_APPROVE` | yes | 15.478 s |

- Exact decision: **11/12 (91.67%)**.
- Missed escalation: **0**.
- Over-escalation: **1**.
- System/provider/HTTP 429 error: **0**.
- Fallback: **0**.
- Processing P50/P95/max: **15.832 / 24.604 / 24.604 s**.
- E2E P50/P95/max: **22.347 / 32.903 / 32.903 s**.
- Upload-accept P95: **11.266 s**.
- Wall clock toàn lượt: **280.837 s**.
- Critical fields: ngày **12/12**, identifier **11/12**, final amount **12/12**, tổng **35/36**.

## Phân tích mismatch và biến thiên

### FAHASA — mismatch ổn định

Model đọc đúng merchant, ngày, receipt number và tổng `91.500`. Contract cũng trả
`totalAmountSource=PRINTED_FINAL_TOTAL`, nhưng deterministic validator không chấp nhận dòng evidence
`Thanh toán 91.500 đ` là nhãn final-payable. Repair lặp lại cùng lỗi, confidence bị kẹp còn 0.69 và
policy chuyển FACT. Ca này over ở cả wave concurrency và lượt tuần tự, nên nguyên nhân chính nằm ở
label taxonomy của validator, không phải concurrency.

Không nên thêm từ khóa `thanh toán` một cách rộng ngay trước demo vì cụm như `thanh toán tiền mặt`
có thể là phương thức/tender chứ không phải tổng phải trả. Bản sửa an toàn cần fixture dương cho dòng
final-payable và fixture âm cho tender/cash/change trước khi thay validator.

### GS25_2 — extraction variance

Lượt wave trước trả `merchantName=null` và FACT dù các field còn lại đúng. Lượt tuần tự trả
`merchantName="GS 25"`, không missing field và AUTO_APPROVE. Không có code/config change giữa hai lượt.
Do đó đây là biến thiên extraction trên vùng header/redaction, không phải bằng chứng rằng concurrency
làm sai policy. Không hard-code merchant từ tên file hoặc receipt number.

PhuongNam vẫn là field miss duy nhất vì model gán `00218778VHVK` thành canonical identifier dù
ground truth không khóa identifier. Quyết định FACT vẫn đúng.

## Verify và concurrency là hai gate khác nhau

- Gate BTC: **Verify Harness 5 ca dưới 90 giây**. Candidate hiện đạt **5/5 trong 65.004 giây**.
- Wave năm người upload: chỉ kiểm tính toàn vẹn/concurrency. Wave trước hoàn tất 5/5, không mất hồ sơ,
  không 429; E2E P95 93.684 giây được chấp nhận cho gate này và không phải Verify SLA.
- Lượt tuần tự này dùng để quan sát stability/latency khi không có wave; không thay hai gate trên.

## Quyết định

- **GO** quay video demo trên frozen candidate, với một operator và video dự phòng.
- **GO** giữ cấu hình OpenRouter/Qwen3.8 Flash, fallback tắt, AI concurrency 1.
- **GO** cho prototype overlay tiếp tục ở nhánh UX độc lập; chưa nối tọa độ vào extraction/policy.
- **NO-GO** nới validator FAHASA ngay trước demo nếu chưa có fixture dương/âm.
- **NO-GO** dùng lượt lặp 11/12 làm holdout/production accuracy claim.

Raw evidence riêng tư nằm ngoài Git tại:
`D:\aura\demo_evidence\22_NewRealReceipt_ForHoldout\regression12_sequential_repeat_v2_2026-10-13`.
