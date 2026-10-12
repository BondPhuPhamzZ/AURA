# Case study: chọn model vision vừa đủ cho AURA

Ngày cập nhật: 12/10/2026.

## 1. Bài toán doanh nghiệp

AURA cần đọc một ảnh hóa đơn/chứng từ và trả dữ kiện có cấu trúc. Model không quyết định hoàn ứng: `PolicyDecisionEngine` C# mới áp dụng quy tắc FACT → POLICY → AUTHORITY. Vì vậy, khả năng vision/OCR, độ ổn định của schema và chi phí vận hành quan trọng hơn năng lực suy luận tổng quát của model rất lớn.

## 2. Tiêu chí lựa chọn

1. Nhận ảnh, đọc tiếng Việt và tài liệu nhiều bố cục.
2. Trả JSON Schema để giảm output sai định dạng.
3. Đủ nhỏ để chi phí/độ trễ hợp lý cho demo và có lộ trình self-host.
4. Có endpoint thật trên hạ tầng đang dùng; không cấu hình một model slug chưa tồn tại.
5. Cho phép đổi model bằng cấu hình mà không thay policy engine.
6. Khi AI lỗi hoặc thiếu dữ kiện, hệ thống phải chuyển kiểm tra thủ công thay vì tự duyệt.

## 3. Các phương án đã cân nhắc

| Phương án | Điểm mạnh | Hạn chế | Quyết định |
|---|---|---|---|
| Qwen3-VL-4B-Instruct qua Ollama local | Không tính phí theo request; dữ liệu ở máy local; 16K synthetic regression ngày 12/10 đạt 15/15 decision, 0 missed/over/system error | Field 69/75; processing P50/P95 101,833/213,429 giây trên máy CPU/GPU split; chưa có capacity/endpoint production | **Fallback candidate; chưa bật auto-fallback** |
| Qwen3-VL-8B-Instruct qua OpenRouter/Parasail | Structured output; judge set lịch sử đạt 15/15 và real regression không missed escalation | Endpoint chỉ còn Parasail tại thời điểm audit; live phát sinh 429 xen kẽ; real regression tiền chỉnh 11/15 | **Baseline/rollback, không còn primary** |
| Qwen3.8 Flash qua OpenRouter/Alibaba, reasoning `none` | Hai Alibaba endpoints tại thời điểm đo; real regression 15/15, 0 missed/over/system error; năm upload E2E P95 41,079 giây | Dịch vụ ngoài; batch hậu chỉnh không phải blind A/B; reasoning mặc định làm JSON bị truncate | **Demo primary release candidate** |
| Model vision cỡ rất lớn (ví dụ 100B+) | Năng lực tổng quát cao | Compute, giá và độ trễ không tương xứng tác vụ OCR; khó biện minh trong bối cảnh doanh nghiệp | Không chọn |

## 4. Quyết định kiến trúc

Cấu hình mặc định là `Vision:Provider=OpenRouter` với `qwen/qwen3.8-flash` và `OpenRouter:ReasoningEffort=none`. `ConfiguredVisionExtractor` cho phép đổi sang Ollama hoặc bật Ollama làm fallback có kiểm soát. `ReceiptExtractionContract` giữ cùng prompt, schema và parser; audit ghi provider thực sự phục vụ. Adapter local không đồng nghĩa model 4B đã đạt chất lượng trên dữ liệu thực.

Không mô tả bất kỳ model nào là “đã chính xác trong thực tế”. Benchmark lịch sử trên judge set 15 ca tổng hợp: OpenRouter 8B đạt 15/15 quyết định và 70/75 field; Ollama 4B profile 16K đạt 15/15 và 69/75 field nhưng P95 213,429 giây. Candidate Qwen3.8 Flash đạt 15/15 receipt post-holdout và concurrency upload 5/5, song đây là regression hậu chỉnh chứ không phải blind A/B. Safety gate vận hành ưu tiên candidate Alibaba sau publish/health/smoke; local model chỉ được bật fallback sau benchmark trên đúng endpoint GPU sẽ dùng. Xem [`QWEN38_FLASH_BENCHMARK_2026-10-12.md`](QWEN38_FLASH_BENCHMARK_2026-10-12.md).

## 5. Bộ kiểm thử bàn giao

- `wwwroot/test_data/expected-results.json`: đúng 5 ca chạy trực tiếp bằng Verify Harness, gồm 3 `AUTO_APPROVE`, 1 `ESCALATE_FACT`, 1 `ESCALATE_POLICY`.
- `test_kit/judge-manifest.json`: đúng 15 ca để BGK tham khảo/tạo biến thể, gồm 5 routine, 4 FACT, 3 POLICY và 3 AUTHORITY.
- `test_kit/manifest.json`: ngân hàng mở rộng 30 ca nội bộ; không tự chạy để tránh tiêu credit.
- `tools/Invoke-ExtendedDatasetEvaluation.ps1`: runner ngoài UI cho 15/30 ca, gọi cùng production upload endpoint và xuất evidence CSV/JSON; không thay Official Verify.
- Automated tests xác minh số lượng, phân phối expected status, liên kết manifest và sự tồn tại/kích thước ảnh mà không gọi AI.

## 6. Cổng chấp nhận trước deploy

1. Build sạch và toàn bộ automated tests đạt offline.
2. Secret key không nằm trong Git/log; model config trùng `qwen/qwen3.8-flash` và reasoning effort là `none`.
3. Một ảnh smoke trên môi trường cần đánh giá phải xác nhận JSON, preview, policy và lịch sử hoạt động; đối chiếu OpenRouter Activity khi chẩn đoán provider.
4. Chạy một lượt Verify 5 ca sau smoke test; ghi lại expected/actual, latency, lỗi và chi phí thay vì chạy lặp để săn PASS.
5. Nếu không đạt, giữ kết quả thật trong proposal, phân loại lỗi OCR/schema/policy/provider rồi mới điều chỉnh. Không sửa expected result để khớp output.

## 7. Lộ trình tối ưu tiếp theo

- Giai đoạn 1: OpenRouter + Qwen3.8 Flash/Alibaba, reasoning `none`, là provider chính cho demo sau khi pass publish/health/smoke đúng revision.
- Giai đoạn 2: Ollama local có thể làm fallback trên cùng laptop khi operator bật cờ; circuit breaker chỉ chuyển với lỗi hạ tầng, không chuyển với lỗi semantic/contract.
- Giai đoạn 3: chỉ đổi provider mặc định sau benchmark tập độc lập và kế hoạch hạ tầng GPU phù hợp; hosted deployment cần Ollama chạy trên chính server hoặc endpoint HTTPS được bảo vệ.

## Nguồn tham chiếu

- Qwen3-VL chính thức và danh sách open weights 4B: https://github.com/QwenLM/Qwen3-VL
- Model card Qwen3-VL-4B-Instruct: https://huggingface.co/Qwen/Qwen3-VL-4B-Instruct
- Endpoint Qwen3-VL-8B-Instruct trên OpenRouter: https://openrouter.ai/qwen/qwen3-vl-8b-instruct
- Endpoint Qwen3.8 Flash trên OpenRouter: https://openrouter.ai/qwen/qwen3.8-flash
- Structured Outputs của OpenRouter: https://openrouter.ai/docs/guides/features/structured-outputs
- Ollama vision và structured outputs: https://docs.ollama.com/capabilities/vision và https://docs.ollama.com/capabilities/structured-outputs
- Ollama Qwen3-VL tags: https://ollama.com/library/qwen3-vl/tags
