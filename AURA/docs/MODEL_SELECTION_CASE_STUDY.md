# Case study: chọn model vision vừa đủ cho AURA

Ngày cập nhật: 29/09/2026.

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
| Qwen3-VL-4B-Instruct qua Ollama local | Không tính phí theo request; dữ liệu ở máy local; official Verify và upload nhỏ ổn định; judge set đạt 73/75 field | Judge set chỉ đạt 14/15 quyết định, bỏ sót TK-12; P95 58,318 giây và không phù hợp 4–5 request đồng thời trên RTX 3050 4 GB | **Giữ tùy chọn offline/manual fallback, chưa bật auto-fallback** |
| Qwen3-VL-8B-Instruct qua OpenRouter | Structured output; judge set đạt 15/15 quyết định, không missed escalation; P95 16,459 giây; concurrency 5 request hoàn tất 5/5 | 70/75 field; dữ liệu đi qua dịch vụ ngoài; phụ thuộc Internet, credit và provider | **Chọn làm demo primary** |
| Model vision cỡ rất lớn (ví dụ 100B+) | Năng lực tổng quát cao | Compute, giá và độ trễ không tương xứng tác vụ OCR; khó biện minh trong bối cảnh doanh nghiệp | Không chọn |

## 4. Quyết định kiến trúc

Cấu hình mặc định vẫn là `Vision:Provider=OpenRouter` với `qwen/qwen3-vl-8b-instruct`. `ConfiguredVisionExtractor` cho phép đổi sang Ollama hoặc bật Ollama làm fallback có kiểm soát. `ReceiptExtractionContract` giữ cùng prompt, schema và parser; audit ghi provider thực sự phục vụ. Adapter local không đồng nghĩa model 4B đã đạt chất lượng trên dữ liệu thực.

Không mô tả 8B hay 4B là “đã chính xác trong thực tế”. Trên judge set 15 ca tổng hợp, OpenRouter đạt 15/15 quyết định và 70/75 field; Ollama đạt 14/15 quyết định và 73/75 field. Ollama đọc metadata tốt hơn một chút nhưng bỏ sót một escalation, nên safety gate ưu tiên OpenRouter. Xem số đo, sai lệch và giới hạn tại [live validation](LIVE_VALIDATION_2026-09-29.md).

## 5. Bộ kiểm thử bàn giao

- `wwwroot/test_data/expected-results.json`: đúng 5 ca chạy trực tiếp bằng Verify Harness, gồm 3 `AUTO_APPROVE`, 1 `ESCALATE_FACT`, 1 `ESCALATE_POLICY`.
- `test_kit/judge-manifest.json`: đúng 15 ca để BGK tham khảo/tạo biến thể, gồm 5 routine, 4 FACT, 3 POLICY và 3 AUTHORITY.
- `test_kit/manifest.json`: ngân hàng mở rộng 30 ca nội bộ; không tự chạy để tránh tiêu credit.
- `tools/Invoke-ExtendedDatasetEvaluation.ps1`: runner ngoài UI cho 15/30 ca, gọi cùng production upload endpoint và xuất evidence CSV/JSON; không thay Official Verify.
- Automated tests xác minh số lượng, phân phối expected status, liên kết manifest và sự tồn tại/kích thước ảnh mà không gọi AI.

## 6. Cổng chấp nhận trước deploy

1. Build sạch và toàn bộ automated tests đạt offline.
2. Secret key không nằm trong Git/log; model secret trùng `qwen/qwen3-vl-8b-instruct`.
3. Một ảnh smoke trên môi trường cần đánh giá phải xác nhận JSON, preview, policy và lịch sử hoạt động; đối chiếu OpenRouter Activity khi chẩn đoán provider.
4. Chạy một lượt Verify 5 ca sau smoke test; ghi lại expected/actual, latency, lỗi và chi phí thay vì chạy lặp để săn PASS.
5. Nếu không đạt, giữ kết quả thật trong proposal, phân loại lỗi OCR/schema/policy/provider rồi mới điều chỉnh. Không sửa expected result để khớp output.

## 7. Lộ trình tối ưu tiếp theo

- Giai đoạn 1: OpenRouter + Qwen3-VL-8B-Instruct là provider chính cho demo vì latency đã đo phù hợp hơn.
- Giai đoạn 2: Ollama local có thể làm fallback trên cùng laptop khi operator bật cờ; circuit breaker chỉ chuyển với lỗi hạ tầng, không chuyển với lỗi semantic/contract.
- Giai đoạn 3: chỉ đổi provider mặc định sau benchmark tập độc lập và kế hoạch hạ tầng GPU phù hợp; hosted deployment cần Ollama chạy trên chính server hoặc endpoint HTTPS được bảo vệ.

## Nguồn tham chiếu

- Qwen3-VL chính thức và danh sách open weights 4B: https://github.com/QwenLM/Qwen3-VL
- Model card Qwen3-VL-4B-Instruct: https://huggingface.co/Qwen/Qwen3-VL-4B-Instruct
- Endpoint Qwen3-VL-8B-Instruct trên OpenRouter: https://openrouter.ai/qwen/qwen3-vl-8b-instruct
- Structured Outputs của OpenRouter: https://openrouter.ai/docs/guides/features/structured-outputs
- Ollama vision và structured outputs: https://docs.ollama.com/capabilities/vision và https://docs.ollama.com/capabilities/structured-outputs
- Ollama Qwen3-VL tags: https://ollama.com/library/qwen3-vl/tags
