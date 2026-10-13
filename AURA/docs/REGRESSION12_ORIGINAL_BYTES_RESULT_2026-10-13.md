# AURA — Regression 12 ảnh gốc, không transform — 13/10/2026

## Ranh giới phép đo

Đây là **post-holdout regression** trên 12 receipt đã biết, không phải blind holdout mới và không được
dùng để thay kết quả prospective 10 ca + addendum 2 ca. Chủ dữ liệu đã cho phép cụ thể việc gửi đúng
12 file gốc tới live AURA/OpenRouter-Alibaba. Runner đọc trực tiếp các path nguồn, kiểm SHA-256 trước
mỗi request và không copy, crop, resize, snapshot, annotate hoặc tạo provider-ready image.

- Manifest SHA-256: `DF09386196ABA789E32180FD6F5AD6C2176BC77C8CB2F52D2241BA23BF86D5E4`.
- Image mode: `DIRECT_ORIGINAL_BYTES_NO_COPY_NO_TRANSFORM`.
- Live config: OpenRouter `qwen/qwen3.8-flash`, reasoning `none`, fallback `false`, AI concurrency `1`.
- Health trước và sau: `ok`; DB/storage/migration sẵn sàng.
- 12/12 source hash khớp khóa trước chạy; 11/12 input khác provider-ready bytes của hai lượt
  prospective trước đó. Osaka là file duy nhất có source hash bằng final-upload hash cũ.
- Không replay ca lỗi và không sửa prompt, policy, threshold, schema hay model giữa lượt chạy.

## Kết quả quyết định

| Ca | File | Expected | Actual | Exact | E2E |
|---|---|---|---|---:|---:|
| R12-01 | EARLYMORNING | `ESCALATE_FACT` | `ESCALATE_FACT` | yes | 28.319 s |
| R12-02 | FAHASA | `AUTO_APPROVE` | `ESCALATE_FACT` | no — over | 58.459 s |
| R12-03 | GS25_2 | `AUTO_APPROVE` | `ESCALATE_FACT` | no — over | 67.780 s |
| R12-04 | GS25_1 | `ESCALATE_POLICY` | `ESCALATE_POLICY` | yes | 93.684 s |
| R12-05 | Lasimi | `AUTO_APPROVE` | `AUTO_APPROVE` | yes | 83.960 s |
| R12-06 | SundayBasic | `AUTO_APPROVE` | `AUTO_APPROVE` | yes | 13.052 s |
| R12-07 | NgoGia | `ESCALATE_FACT` | `ESCALATE_FACT` | yes | 21.720 s |
| R12-08 | Osaka | `ESCALATE_FACT` | `ESCALATE_FACT` | yes | 16.943 s |
| R12-09 | Pharmacity | `ESCALATE_FACT` | `ESCALATE_FACT` | yes | 33.959 s |
| R12-10 | PhuongNam | `ESCALATE_FACT` | `ESCALATE_FACT` | yes | 30.672 s |
| R12-11 | Chidori | `ESCALATE_FACT` | `ESCALATE_FACT` | yes | 40.706 s |
| R12-12 | ThreeO_Clock | `AUTO_APPROVE` | `AUTO_APPROVE` | yes | 27.639 s |

- Exact decision: **10/12 (83.33%)**.
- Missed escalation: **0**.
- Over-escalation: **2**.
- System/provider/HTTP 429 error: **0**.
- Fallback: **0**.
- Processing P50/P95/max: **15.609 / 34.554 / 34.554 s**.
- E2E P50/P95/max: **30.672 / 93.684 / 93.684 s**.
- Wall clock toàn lượt: **282.775 s**.

## Probe năm upload khác nhau đồng thời

Năm client đầu gửi năm receipt khác nhau gần như đồng thời. Cả năm nhận HTTP 202, được lưu trong
SQL và về terminal `COMPLETED`; không có global 409, client error, provider error hoặc 429.

- Completed: **5/5**.
- Upload-accept P95: **5.913 s**.
- E2E P95/max: **93.684 s**.
- Bốn trên năm ca terminal dưới 90 s; ca chậm nhất vượt ngưỡng 3.684 s.

Kết quả chứng minh đường upload bất đồng bộ chịu được năm người dùng trên deployment một process
mà không mất hồ sơ, nhưng **không chứng minh SLA năm kết quả dưới 90 giây**. AI vẫn tuần tự ở
concurrency 1 để tránh burst upstream; hàng đợi làm tăng latency của request cuối. Semaphore/cooldown
hiện là per-process, nên trước khi scale nhiều IIS worker/server vẫn cần distributed provider limiter.

## Field evidence

Đối chiếu lại với facts đã khóa trong worksheet prospective trước đó:

- Ngày: **12/12**.
- Canonical identifier: **11/12**.
- Final amount: **12/12**.
- Tổng field trọng yếu: **35/36 (97.22%)**.

PhuongNam là field miss duy nhất: expected không có canonical receipt ID nhưng model vẫn gán
`00218778/VHVK` vào `receiptNumber`. Quyết định vẫn đúng FACT nên lỗi này không đổi decision.

## Hai mismatch còn lại

1. **FAHASA — over-escalation:** merchant/date/identifier/total đều đúng, nhưng semantic validator
   chưa chấp nhận `Thanh toán 91.500 đ` là nhãn final-payable đủ tin cậy. Confidence bị hạ 0.69.
2. **GS25_2 — over-escalation:** date/identifier/total đúng, nhưng `merchantName=null`; model cho rằng
   tên cửa hàng bị che. Đây là fail-safe merchant miss, không phải provider failure.

Hai nguyên nhân giống prospective run; loại bỏ provider-ready transformation không giải quyết chúng.
Không nên nới guard trực tiếp trên hai ảnh đã biết. Cần synthetic positive/negative fixtures rồi mới
hardening validator/typed quality issue.

## So sánh mô tả với hai lượt prospective cũ

Combined prospective 10+2 trước đó là **9/12**, 0 missed, 3 over. Regression ảnh gốc này là
**10/12**, 0 missed, 2 over. Three O'Clock đổi từ FACT sang AUTO vì lần này facts đầy đủ,
`confidence=1`, không còn warning/validation issue về dải blackout. PhuongNam cũng đọc đúng ngày,
nâng critical fields mô tả từ 34/36 lên 35/36; identifier over-inference vẫn còn.

Đây chỉ là quan sát giữa hai input byte regimes và hai lần suy luận, không phải causal A/B: input khác
bytes, thứ tự/concurrency khác và model có tính biến thiên. Raw prospective evidence không bị thay thế.

## Quyết định

- **GO** cho demo có giám sát với OpenRouter/Alibaba và video dự phòng.
- **GO** cho tối đa năm upload khác nhau về tính toàn vẹn; UI phải hiển thị trạng thái hàng đợi.
- **NO-GO** cho claim mọi request trong wave năm người luôn dưới 90 s.
- **NO-GO** cho production multi-instance trước distributed concurrency/rate limiting và auth/RBAC.
- Không replay 12 ảnh này để săn số đẹp; lượt hiện tại đã được khóa làm regression evidence.

Evidence raw riêng tư nằm ngoài Git tại:
`D:\aura\demo_evidence\22_NewRealReceipt_ForHoldout\regression12_original_bytes_v1_2026-10-13`.
