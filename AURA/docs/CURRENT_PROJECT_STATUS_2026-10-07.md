# AURA — Current project status

Cập nhật: 07/10/2026. Đây là nguồn trạng thái hiện hành; các file ngày cũ hơn là lịch sử.

## Kết luận điều hành

AURA vẫn là **MVP technical baseline cho supervised demo**, chưa phải production/accuracy-ready.
Raw official holdout và raw Test Kit v3 được giữ bất biến. Candidate local mới xử lý gap policy
`Vé xem phim`, giữ dấu tiếng Việt, bổ sung token/duration observability và tài liệu phân biệt
off-load/context/output cap. Release publish và 140/140 automated test pass; EF không có pending
model change. Candidate này chưa được coi là live cho tới khi publish/recycle/health và chạy v3.1.

## Trạng thái kiểm chứng

| Hạng mục | Trạng thái |
|---|---|
| Release build/publish local | PASS, 0 compile error |
| Automated tests | 140/140 PASS, offline, không gọi AI |
| EF model | Không có pending model change |
| Policy entertainment | `Vé xem phim`/`rạp chiếu phim` và English variants → `ESCALATE_POLICY` |
| R3-12 v3.1 | Ground truth được thực thi thật qua `PolicyDecisionEngine` |
| OCR diacritic guard | Prompt cấm đoán `Bìa hồ sơ` thành `Bia hồ sơ`; policy vẫn có context exemption hẹp |
| Vision input | AURA đọc nguyên byte và base64; không resize/crop/nén tại ứng dụng |
| Token observability | Log prompt/completion/token cap cho OpenRouter; prompt/completion/context/duration cho Ollama |
| Raw holdout | Giữ 10/15 raw, 11/15 adjudicated; không chạy lại như blind |
| Raw Test Kit v3 | Giữ 11/15 decision, 142/145 field, 0 system error |
| V3.1 live | Chưa chạy; chỉ chạy đúng một batch sau publish candidate |
| Fallback | Vẫn tắt; Ollama chưa đạt gate để bật mặc định |

## Gate kế tiếp

1. Commit/push candidate này.
2. Publish đúng commit, recycle Pool, đợi 10–30 giây và xác nhận `/healthz` trả `ok`, DB/storage
   true, pending migration 0, provider/model đúng, fallback false.
3. Chạy Verify 5 ca và security/workflow smoke tối thiểu.
4. Chạy đúng một live batch v3.1; không chạy lại v3 raw hoặc 15 ảnh official holdout.
5. Chỉ đóng gate khi 15 request hoàn tất, 0 system error và 0 missed escalation; mọi mismatch
   khác phải giữ raw và phân tích, không relabel hậu nghiệm.
6. GPU BTC: benchmark Ollama-only 8B với context 16384/output 4096, ghi `ollama ps`, `nvidia-smi`,
   token/duration và decision/field metrics; không bật fallback trước khi đạt gate.

## Ranh giới claim

Có thể nói policy C# là authority tất định và candidate offline đã pass 140 test. Chưa được nói
v3.1 live pass, Ollama tương đương OpenRouter, hoặc hệ thống production-ready trước evidence mới.
