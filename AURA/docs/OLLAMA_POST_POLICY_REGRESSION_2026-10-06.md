# Ollama 4B post-policy regression — 06/10/2026

## Kết luận

`qwen3-vl:4b-instruct` **không đạt safety gate** sau evidence contract v2. Một lượt duy nhất trên judge set synthetic 15 ca đạt 13/15: một missed escalation TK-12, một over-escalation TK-02 và 0 system error. Fallback tiếp tục tắt; OpenRouter 8B vẫn là primary.

Đây là post-policy synthetic regression, không phải blind holdout và không thay raw holdout 10/15.

## Cấu hình khóa

- Source commit trong runner: `d2b7bbb234602855ed0f189caf6ad4542cb46015`.
- Working tree lúc chạy có bản sửa CSS/test tài liệu chưa commit; không thay model, prompt, schema hoặc policy được đo.
- Policy SHA-256: `4FAB752409302F1F96577FB46BB8E8219D060A3501D4159AD3EA7819A0674DDA`.
- Judge manifest SHA-256: `D3EC52A8A9F09B1E7798ECD1C9947D9E4C78ECBA6E2FC8FB20B62EB117782007`.
- Source manifest SHA-256: `EA2DFFE3DD1C6E546886BFA3795D2473E368FC19B6277A5521F05FCB821472BD`.
- Provider/model: Ollama 0.35.1 / `qwen3-vl:4b-instruct` Q4_K_M.
- Model digest: `ee4b975b58c17ce268cd19d40db35d5edc64603035d2ffc1fee1968eb0947f7b`.
- Resource envelope: context 8192, output cap 2048, keep-alive 30m; `ollama ps` ghi 60% CPU / 40% GPU trên RTX 3050 Laptop 4 GB.
- Port/database/storage riêng: `5018`, `AuraOllamaPolicyRegression_20261006_01`, `App_Data/ollama-policy-regression-20261006`.
- Fallback tắt; 15 ảnh đều là synthetic `test_kit/images`.

## Kết quả

| Metric | Kết quả |
|---|---:|
| Decision exact | 13/15 — 86,67% |
| Missed escalation | 1/10 — TK-12 |
| Over-escalation | 1/5 — TK-02 |
| System error | 0/15 |
| Field exact | 56/75 — 74,67% |
| Accepted P50/P95 | 17/210 ms |
| End-to-end P50/P95 | 89,728/136,758 giây |
| Fallback | 0 |

### TK-12 — missed escalation

Ảnh cố ý làm mờ dòng tổng cuối. Model vẫn trả `totalAmount=336000`, `totalAmountSource=PRINTED_FINAL_TOTAL` và tự tạo `totalAmountEvidence="TỔNG THANH TOÁN 336.000 đ"`. Vì JSON tự nhất quán và model khẳng định evidence nhìn thấy, deterministic policy không có pixel/OCR signal độc lập để bác bỏ; kết quả sai là `AUTO_APPROVE` thay vì `ESCALATE_FACT`.

Không sửa bằng hard-code tên fixture hoặc merchant. Biện pháp đúng là không cho 4B làm fallback tự động, đo lại 8B local trên GPU 16 GB và tiếp tục ưu tiên fail-safe/model có safety evidence tốt hơn.

### TK-02 — over-escalation

Model đọc đúng số `HD-260921-002` nhưng gán vào `orderId` thay vì `invoiceNumber/receiptNumber/transactionReference`. Semantic validator yêu cầu repair. Repair không hoàn tất trong resource envelope mới: một payload vượt context 8192; một repair khác trong batch chạm output cap 2048. AURA giữ extraction đầu, đánh dấu validation issue và chuyển `ESCALATE_FACT`. Đây là fail-safe đúng kỹ thuật nhưng decision không khớp expected AUTO.

## Quyết định

1. Không bật Ollama 4B fallback mặc định.
2. Không rerun batch với cùng cấu hình để săn PASS.
3. Phiên GPU BTC dùng `qwen3-vl:8b-instruct-q4_K_M`, context 16384, output 4096, một request tại một thời điểm và synthetic data.
4. Kết quả GPU là benchmark mới; phải lưu cả fail và pass, không cộng/trộn với run này.
5. Evidence raw nằm ngoài Git tại `D:\aura\demo_evidence\18_setup_GPU_BTC\01_local_ollama_4b_policy_regression_2026-10-06`.
