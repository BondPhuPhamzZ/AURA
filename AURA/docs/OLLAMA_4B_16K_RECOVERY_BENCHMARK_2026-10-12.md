# Ollama 4B 16K recovery benchmark — 12/10/2026

## Kết luận

`qwen3-vl:4b-instruct` digest `ee4b975b58c1` đã hoàn tất Test Kit v2 synthetic 15 ca với cấu hình `ContextTokens=16384`, `MaxOutputTokens=4096`, `Think=false`, concurrency 1 và fallback tắt:

- 15/15 quyết định đúng;
- 0 missed escalation;
- 0 over-escalation;
- 0 system error;
- 69/75 field đúng (92%);
- semantic repair ở TK-02 và TK-12;
- processing latency P50 101.833 ms, P95/max 213.429 ms.

Đây là synthetic regression, không phải blind holdout hay production accuracy. Kết quả chứng minh 4B có thể làm đường dự phòng về mặt chất lượng trên tập này khi đủ context; nó chưa chứng minh capacity cho nhiều người dùng và chưa thay OpenRouter 8B làm primary.

## Vì sao lượt 8K thất bại

Lượt 15 ca đầu dùng context 8.192 và output cap 4.096 chỉ hoàn tất 5/15; 10 ca còn lại là `AI_RESPONSE_TRUNCATED`. Log cho thấy prompt + completion chạm đúng cửa sổ 8.192 token. Đây là resource-envelope failure, không phải 10 quyết định policy sai và không phải bằng chứng CPU/GPU off-load làm giảm chất lượng ảnh.

Gate hai ca với context 16.384 pass 2/2 trước khi chạy batch đầy đủ. Vì vậy profile 4B hiện hành phải giữ tối thiểu 16K cho contract/policy hiện tại; không hạ context giữa batch chỉ để giảm latency.

## Khôi phục batch an toàn

Client runner kết thúc sau khi đã upload 14 ca, nhưng SQL và receipt storage vẫn còn nguyên:

1. 13 ca `COMPLETED` được giữ nguyên, không gửi lại.
2. TK-28 đang `PROCESSING` được worker reclaim bằng lease trên cùng database/storage và hoàn tất đúng `ESCALATE_AUTHORITY`.
3. Chỉ TK-30, ca chưa từng upload, được gửi đúng một lần sau khi TK-28 terminal.
4. Danh tính 15 ca được phục hồi bằng SHA-256 byte-identical giữa ảnh nguồn khóa và file đã lưu.
5. Stopwatch HTTP/E2E phía client của 14 ca đầu không thể phục hồi nên báo cáo chỉ dùng `processingLatencyMs`; không tạo số liệu giả.

Evidence local nằm tại `demo_evidence/27_local_ollama_4b_postpolicy_2026-10-12`. Manifest cấp thư mục có 56 file và đã được kiểm lại toàn bộ SHA-256.

## Sáu field mismatch được giữ nguyên

- TK-02: sai một ký tự tên merchant và thiếu invoice number.
- TK-06: thiếu invoice number.
- TK-12: thiếu invoice number.
- TK-21, TK-22: trả `RETAIL_RECEIPT` thay vì `RESTAURANT_BILL`.

Policy vẫn đúng 15/15 vì các field mismatch này không làm mất critical fact hoặc thay đổi nhánh nghiệp vụ trong fixture tương ứng. Không hard-code fixture để nâng field score.

## Quyết định vận hành

- OpenRouter/Qwen3-VL-8B-Instruct vẫn là primary của supervised demo.
- Ollama 4B 16K là fallback candidate, không phải fallback production đã bật.
- Chỉ bật auto-fallback sau khi endpoint GPU có TLS/xác thực hoặc private network, khóa đúng digest, pass gate TK-02/TK-12/TK-30, pass một batch 15 ca và một smoke concurrency 2 request.
- Không chọn thinking digest cho baseline extraction. Qwen mô tả Thinking là biến thể tối ưu cho suy luận STEM/toán; AURA cần transcription có căn cứ và JSON contract, nên thinking phải được benchmark riêng trước khi dùng.
- Hosted AURA không thể gọi Ollama trên laptop qua `127.0.0.1`; không public thẳng cổng 11434.

## Nguồn kỹ thuật

- Qwen3-VL-4B-Instruct model card: https://huggingface.co/Qwen/Qwen3-VL-4B-Instruct
- Ollama tag/digest: https://ollama.com/library/qwen3-vl:4b-instruct
- OpenRouter Qwen3-VL-8B-Instruct: https://openrouter.ai/qwen/qwen3-vl-8b-instruct/api

