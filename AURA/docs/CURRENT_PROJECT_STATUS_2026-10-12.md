# AURA — Current project status 12/10/2026

## Kết luận điều hành

AURA đang ở trạng thái **supervised demo candidate**, chưa phải production/accuracy-ready. Runtime/policy baseline là `c147e1915ae7e6deab26a5eea1b4402c4e6cdd46`; checkpoint hiện hành chỉ đồng bộ default Ollama và tài liệu, không đổi pipeline/policy/schema. Production health sau publish/recycle đã trả `ok`, database/storage/migration đều đạt và capacity guard hiển thị concurrency 1, cooldown 120 giây, fallback tắt.

Hai rủi ro còn mở không phải lỗi policy C#:

1. OpenRouter/Qwen3-VL-8B đôi lúc nhận HTTP 429 từ upstream Parasail dù tài khoản còn credit. Benchmark 5 client ngày 12/10 chặn đúng 4 lượt Verify phụ ở cả 2/2 lần, nhưng batch chính lần lượt chỉ đạt 4/5 và 2/5; live provider chưa đạt reliability gate cho 5 ca dưới 90 giây.
2. Ollama 4B 16K đạt safety regression synthetic nhưng chậm trên máy hiện tại; hosted AURA chưa có endpoint Ollama riêng được bảo vệ để bật fallback thật.

## Evidence hiện hành

| Hạng mục | Kết quả và ranh giới |
|---|---|
| Offline suite/capacity hardening | 159/159 và Release build sạch tại candidate capacity-hardening; xem `PROVIDER_CAPACITY_HARDENING_2026-10-12.md` |
| Live health | `ok`; AI/policy/database/storage/migration đạt; max AI concurrency 1; cooldown 120 giây; Verify bật; fallback tắt |
| Live Verify 5-client concurrency | 2/2 lượt đúng 1×HTTP 200 + 4×HTTP 409; batch chính 84.545/46.584 ms nhưng fail 429, nên chưa có bằng chứng 5 inference thành công dưới 90 giây |
| Official holdout thật | Raw 10/15, adjudicated 11/15; 1 missed raw; không chạy lại để tạo blind claim |
| Post-holdout real regression | Lượt hoàn chỉnh 07/10: 11/15 exact, 0 missed, 4 over, 0 system error; batch 12/10 bị 429 nên không thay số này |
| Test Kit v3.1 OpenRouter | 13/15 exact, 0 missed, 0 system error, field 140/146; hai mismatch fail-safe |
| Test Kit v2 OpenRouter hậu-contract | 14/15, missed TK-12 blur cũ, field 69/75 |
| Test Kit v2 Ollama 4B 16K | 15/15, 0 missed/over/system error, field 69/75; processing P50/P95 101,833/213,429 giây |
| Real receipt regression Ollama 4B 16K | 15/15 decision, 0 missed/over/system error, field 29/30; E2E P50/P95 191,261/384,539 giây; an toàn nhưng quá chậm cho Verify 90 giây |
| Manual mobile 4G/5G | health, Verify, workflow, Audit, refresh và receipt persistence pass |

Evidence Ollama nằm ngoài Git tại `demo_evidence/27_local_ollama_4b_postpolicy_2026-10-12` (synthetic) và `demo_evidence/28_local_ollama_4b_real_regression_2026-10-12` (15 receipt regression); tài liệu công khai synthetic là `OLLAMA_4B_16K_RECOVERY_BENCHMARK_2026-10-12.md`.

## Quyết định cấu hình

- Primary: `OpenRouter` / `qwen/qwen3-vl-8b-instruct`.
- Hosted fallback: `false`.
- Local fallback có điều kiện: `qwen3-vl:4b-instruct` digest `ee4b975b58c1`, context 16384, output 4096, timeout 600 giây, keep-alive 30 phút, `Think=false`, concurrency 1. Chỉ bật khi AURA và Ollama cùng laptop hoặc có endpoint riêng được bảo vệ; xem `COMPETITION_FAILOVER_AND_429_STRATEGY_2026-10-12.md`.
- Không chuyển sang thinking digest trước hạn nộp; thinking phải có benchmark riêng và không được gộp với instruct baseline.
- Không public port Ollama 11434. Remote GPU cần private network hoặc TLS + auth + rate limit; app hiện chưa có bearer header riêng cho Ollama.

## Việc còn lại trước freeze/nộp

1. Không chạy thêm batch provider dài chỉ để săn PASS. Hai lượt Verify concurrent mới đều dính 429; giữ nguyên cả evidence fail và chỉ chạy một smoke nhỏ khi thật sự cần xác nhận môi trường.
2. Quay video/rehearsal bằng OpenRouter primary, một operator, không chạy Verify song song upload; có ảnh/video dự phòng nếu upstream 429. Local contingency có thể bật Ollama fallback sau one-image smoke, nhưng public SmarterASP tiếp tục để fallback tắt.
3. Chỉ benchmark GPU BTC tiếp khi endpoint ổn định và có thời gian: gate TK-02/TK-12/TK-30, full 15 serial, rồi concurrency 2. Không dùng receipt thật trên máy mượn.
4. Giữ 8–10 receipt mới unseen: khóa ground truth/hash trước request đầu tiên và chỉ chạy official mini-holdout khi code/policy đã freeze. Không điều chỉnh nhãn sau output.
5. Chỉ sửa code nếu có P0 tái hiện được. Các thay đổi tài liệu/evidence không yêu cầu publish lại ứng dụng.

## Claim an toàn khi trình bày

Có thể nói AURA tách extraction khỏi policy, fail-safe khi dữ kiện/provider lỗi, durable queue sống qua restart, và Ollama 4B 16K vừa đạt 15/15 trên regression synthetic. Phải nói rõ đây không phải production accuracy; OpenRouter còn rủi ro upstream 429, Ollama còn rủi ro latency/capacity, và official real holdout raw chỉ đạt 10/15.

