# AURA — Current project status

Cập nhật: 07/10/2026. Đây là nguồn trạng thái hiện hành; các file ngày cũ hơn là lịch sử.

## Kết luận điều hành

AURA vẫn là **MVP technical baseline cho supervised demo**, chưa phải production/accuracy-ready.
Raw official holdout và raw Test Kit v3 được giữ bất biến. Logic commit `0241dfa` xử lý gap policy
`Vé xem phim`, giữ dấu tiếng Việt, bổ sung token/duration observability và tài liệu phân biệt
off-load/context/output cap. Bản mới đã publish; health/public endpoint/security smoke và live
v3.1 đều hoàn tất. Live Verify tái xác nhận 5/5 và workflow thật đã đi qua chuyển quản lý,
đồng ý duyệt rồi hoàn tác về đúng `ESCALATE_FACT`. Kiểm tra trực quan desktop/mobile 390x844
không thấy vỡ layout; console không có warning/error. Sau các guard runner, full offline suite
là 142/142; EF không có pending model change. Batch OpenRouter hậu-contract còn thiếu của v2
đã chạy đúng một lần: 14/15 exact, 1 missed TK-12, 0 over/system error, field 69/75. Kết quả
không còn tối ưu 15/15 như lịch sử và được giữ nguyên; v3.1 vẫn là safety gate hiện hành.

## Trạng thái kiểm chứng

| Hạng mục | Trạng thái |
|---|---|
| Release build/publish local | PASS, 0 compile error |
| Automated tests | 142/142 PASS, offline, không gọi AI |
| EF model | Không có pending model change |
| Policy entertainment | `Vé xem phim`/`rạp chiếu phim` và English variants → `ESCALATE_POLICY` |
| R3-12 v3.1 | Ground truth được thực thi thật qua `PolicyDecisionEngine` |
| OCR diacritic guard | Prompt cấm đoán `Bìa hồ sơ` thành `Bia hồ sơ`; policy vẫn có context exemption hẹp |
| Vision input | AURA đọc nguyên byte và base64; không resize/crop/nén tại ứng dụng |
| Token observability | Log prompt/completion/token cap cho OpenRouter; prompt/completion/context/duration cho Ollama |
| Raw holdout | Giữ 10/15 raw, 11/15 adjudicated; không chạy lại như blind |
| Raw Test Kit v3 | Giữ 11/15 decision, 142/145 field, 0 system error |
| V3.1 live | 15/15 completed; 13/15 exact; 0 missed; 0 system error; field UTF-8-safe 140/146 |
| V2 live hậu-contract | 15/15 completed; 14/15 exact; missed TK-12; 0 over/system error; field 69/75 |
| Real receipt regression | 15/15 completed; 11/15 exact adjudicated; 0 missed; 4 over; 0 system error; field 28/30 chỉ currency/total |
| Verify live hậu publish | 5/5: 3 AUTO + FACT + POLICY; 0 system error |
| Human workflow | FACT → forward → `MANUAL_REVIEW_ACCEPTED` → UNDO → FACT; Audit lưu vết |
| UI smoke | Desktop reviewer và mobile 390x844 applicant/reviewer/audit pass; mặc định `Gia Phú`; console sạch |
| Fallback | Vẫn tắt; Ollama chưa đạt gate để bật mặc định |

## Gate kế tiếp

1. Giữ raw v2/v3/v3.1. Báo cáo TK-12 v2 là fixture blur còn đọc/suy đoán được; không sửa policy
   hoặc chạy lại để làm đẹp. Dùng physical ink-loss của v3.1 làm safety regression hiện hành.
2. GPU BTC: benchmark Ollama-only 8B bằng đúng v3.1, context 16384/output 4096, ghi `ollama ps`, `nvidia-smi`,
   token/duration và decision/field metrics; không bật fallback trước khi đạt gate.
3. Tổ chức ba user session và ba rehearsal trên cùng commit freeze. Không cần chạy lại official
   holdout hoặc v3.1; mỗi lần rehearsal chỉ chạy Verify theo kế hoạch đã chốt.
4. Post-holdout real-receipt regression đã hoàn tất và giữ riêng: exact 11/15, missed 0,
   over 4, system error 0. Không nới guard để săn AUTO; giữ receipt mới unseen cho holdout v2.

## Ranh giới claim

Có thể nói policy C# là authority tất định, 142 code test pass và live v3.1 đạt safety gate
0 missed/0 system error. Phải nói rõ v2 hậu-contract chỉ đạt 14/15 với một missed TK-12.
Không nói 13/15 là production accuracy hoặc Ollama tương đương OpenRouter.
