# Measurement Plan (Chuẩn bị cho Sprint 2)

## 1. Missed-escalation rate
Tỷ lệ hóa đơn đáng lẽ phải bị chặn (Escalate) nhưng hệ thống lại Auto-Approve.
- **Cách đo**: Với tập độc lập có nhãn bởi hai người kiểm tra, tính `missed escalations / tổng ca bắt buộc escalate`; báo cáo khoảng tin cậy và bất đồng nhãn.

## 2. Over-escalation rate
Tỷ lệ hóa đơn hoàn toàn hợp lệ nhưng bị hệ thống ném vào hàng đợi Escalate, làm mất thời gian của sếp.
- **Cách đo**: `routine cases bị escalate / tổng routine cases` trên tập độc lập. Manager approve không tự động chứng minh AI đã over-escalate.

## 3. Extraction Accuracy
- **Cách đo**: Đối chiếu từng field với ground truth trên tối thiểu 100 ảnh đa dạng; báo precision/recall cho identifier và prohibited item, exact-match cho amount/date/currency.

## 4. Thời gian xử lý
- **Cách đo**: Ghi p50/p95 end-to-end từ upload đến decision và thời gian human review. Đo baseline thủ công với cùng người/cùng loại hồ sơ; không dùng ước tính cảm tính.

## 5. Tác động tiêu cực và gánh nặng mới

- Số phút quản lý dành cho queue và số lần phải mở lại chứng từ gốc.
- Tỷ lệ người dùng chấp nhận output mà không kiểm tra (automation bias).
- Chi phí thời gian sửa dữ liệu OCR sai và xử lý lỗi quota/API.
- Khảo sát 3 người dùng thực tế trước/sau, ghi chức danh và phản hồi nguyên văn khi có đồng thuận.

## 6. Ngân sách benchmark và quota

- Test Kit v2 có 30 ca nhưng Verify demo chỉ gọi 5 ca đại diện.
- Build và 142 automated test hiện tại (policy/workflow/audit + OpenRouter/Ollama evidence contract v2 + raw date/final-total provenance + semantic validation/repair + định danh chứng từ giấy + đối chiếu giảm giá/VAT đã gồm + guard dòng hàng + taxonomy personal-item/entertainment + OCR-loss policy disambiguation + executable R3-12 ground truth + UTF-8 runner guard + fallback/circuit breaker + worker backoff + Test Kit v2/v3/v3.1 integrity + initial-tab rendering) không gọi API AI.
- Sau deploy: 1 request smoke test; nếu pass mới chạy 1 lượt Verify = 5 request. Giữ tối thiểu 10 request dự phòng cho BGK/video.
- Không retry thủ công liên tục khi 429. Ghi lỗi và chờ đúng cửa sổ rate-limit của OpenRouter/provider.
- Benchmark 30 ca chỉ chạy trong một phiên đo riêng khi đã xác nhận quota/billing; không dùng trong luồng demo.
- Chạy gói 15/30 bằng `tools/Invoke-ExtendedDatasetEvaluation.ps1`; runner lưu manifest hash, commit, provider config, expected/actual, field match, P50/P95 và error code vào `test_kit/results`.
- Không dùng ảnh do generative AI tạo làm ground truth chính vì chữ/số có thể bị bịa. Dùng Python/Pillow với seed và manifest cho synthetic coverage; giữ hóa đơn thật đã ẩn danh làm holdout độc lập ngoài Git.

## 7. So sánh OpenRouter 8B và Ollama 4B

- Chạy cùng ảnh, policy, JSON Schema và expected result; ghi rõ provider, model/tag, context và cấu hình lượng tử hóa.
- Báo schema success, exact match của amount/date/currency/identifier, missed-escalation, over-escalation, P50/P95 latency, RAM tối đa và chi phí trên hồ sơ hợp lệ.
- Cổng tối thiểu trước khi cân nhắc đổi mặc định: Verify 5/5 trong ba lượt liên tiếp; 15 ca BGK đi đúng nhánh; không missed escalation trong ca rủi ro khóa; không có lỗi chưa bắt; có rollback cấu hình về OpenRouter.
- Nếu local 4B chậm hơn giới hạn Verify hoặc giảm chất lượng field quan trọng, giữ OpenRouter làm mặc định và ghi Ollama là phương án privacy/cost có giới hạn. Không sửa expected result để làm đẹp benchmark.
- Mọi benchmark so sánh provider phải đặt `Vision:FallbackEnabled=false`; benchmark fallback là kịch bản riêng, ghi cả primary failure, served provider, end-to-end latency và recovery về primary.
- Kết quả live ngày 29/09/2026 trên judge set tổng hợp 15 ca, fallback tắt: OpenRouter 8B đạt 15/15 decision, Ollama 4B đạt 14/15 và missed TK-12. Post-policy/contract v2 ngày 06/10, Ollama 4B đạt 13/15, vẫn missed TK-12 và thêm over TK-02 do repair vượt context/output; cả hai chỉ là synthetic regression. Official real holdout ngày 06/10 đã chạy đúng một lượt: raw 10/15 decision, missed 1/9, over-escalation 3/6, 0 system error; adjudicated view 11/15 vì BH-04 ground truth sai nhưng raw bất biến. Field 28/30 chỉ đo currency/total. Xem [postmortem official holdout](OFFICIAL_HOLDOUT_POSTMORTEM_2026-10-06.md); không dùng lại 15 ảnh để tạo blind claim mới.
- Post-holdout regression ngày 07/10 chạy lại đúng 15 receipt trên OpenRouter 8B với adjudicated manifest khóa trước request: 11/15 exact, 0/10 missed escalation, 4/5 over-escalation, 0 system error; P50/P95 end-to-end 12,348/26,277 giây. Exact không tăng nhưng lỗi fail-open BH-07 đã được chặn; đây là safety regression, không phải blind accuracy claim. Xem [kết quả real-receipt regression](REAL_RECEIPT_REGRESSION_RESULT_2026-10-07.md).
- Test Kit v3 post-holdout chạy một lượt OpenRouter ngày 06/10 tại commit/hash đã khóa: 11/15 decision, 142/145 field, missed 2, over 2, system error 0, P50/P95 9,235/37,917 giây. Raw không sửa; v3.1 là revision mới sau phân tích mismatch và chỉ được đo sau publish. Xem [raw v3 report](REGRESSION_TEST_KIT_V3_LIVE_RESULT_2026-10-06.md).
- Fallback regression cô lập ngày 02/10 chạy 1 fixture tổng hợp: primary lỗi `AI_NOT_CONFIGURED`, `ServedProvider=Ollama`, `FallbackUsed=true`, 0 validation issue, worker 83,649 giây và E2E 84,425 giây. Đây là functional evidence, không phải capacity/recovery benchmark; xem [fallback regression](FALLBACK_REGRESSION_2026-10-02.md).
