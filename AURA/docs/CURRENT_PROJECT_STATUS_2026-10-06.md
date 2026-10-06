# AURA — Current project status

Cập nhật: 06/10/2026. Đây là nguồn trạng thái hiện hành; các file ngày cũ hơn là lịch sử.

## Kết luận điều hành

AURA là **MVP technical baseline sẵn sàng cho supervised demo**, chưa phải production-ready hoặc accuracy-ready. Official holdout 15 hóa đơn thật đã hoàn tất một lượt và dẫn tới evidence contract v2. Deployment `1f4b3c9` đã pass health 5/5, security negative 6/6, Verify OpenRouter 5/5, workflow fixture 2/2 và Audit timezone Việt Nam. Điện thoại thật qua 4G/5G đã pass luồng chính. Candidate hiện tại gồm bản vá mobile và hardening hậu Test Kit v3; full suite 133/133 pass nhưng phải publish/recycle rồi chạy live v3.1 trước khi đóng gate mới.

## Baseline đã kiểm

| Hạng mục | Trạng thái |
|---|---|
| Deployed revision | `1f4b3c9` — contract v2 và bản vá Audit timezone đã live |
| Candidate kế tiếp | logic `b69ff5d` — mobile alignment + policy/identifier hardening context-aware + Test Kit v3.1; chờ publish/live v3.1 |
| Release build | 0 warning, 0 error |
| Automated tests | 133/133 pass trên candidate; offline, không gọi API AI |
| EF model | Không có pending model change |
| NuGet vulnerability scan | Không có advisory cho app/test tại thời điểm kiểm |
| Live health | 5/5 `ok`; DB/storage true; pending migration 0 |
| Live Verify | 5/5: 3 AUTO + FACT + POLICY |
| Live human workflow | 2/2 hậu publish: policy reject/undo và fact approve/undo |
| Live security negative | 6/6 nếu tính 2 upload guard + 4 route/CSRF checks |
| Official holdout raw | 10/15; missed 1; over 3; system error 0 |
| Holdout adjusted | 11/15; missed 1/10; over 2/5; raw không đổi |
| Holdout field metric | 28/30 chỉ currency + total; không phải full accuracy |
| Phone 4G/5G | PASS luồng/refresh/Audit; P1 alignment đã sửa local, chờ republish |
| Ollama 4B post-policy | 13/15 synthetic judge set; missed TK-12, over TK-02, 0 system error; không đạt safety gate |
| OpenRouter Test Kit v3 raw | 11/15 decision; 142/145 field; missed R3-08/R3-12; over R3-03/R3-04; 0 system error; raw bất biến |
| Test Kit v3.1 candidate | Sửa deterministic `Bìa`/`bia`, short prefix `RCF`, precondition R3-12; offline pass, chưa live |
| Fallback | Tắt; Ollama chỉ opt-in/manual, không bật mặc định |

Evidence live hiện hành: [`LIVE_POST_HOLDOUT_VALIDATION_2026-10-06.md`](LIVE_POST_HOLDOUT_VALIDATION_2026-10-06.md).

## Gate còn mở

1. Publish current master có logic `b69ff5d`, recycle Pool, health `ok`, rồi chạy đúng một live batch v3.1; không chạy lại raw v3.
2. Chụp lại đúng tab quản lý trên điện thoại 4G/5G để đóng P1 alignment.
3. Ba phiên người dùng thật có giám sát; thu consent, task, quan sát, thời gian, lỗi và feedback do người dùng xác nhận.
4. Ba dress rehearsal 8–10 phút trên cùng commit freeze; local Ollama chỉ là phương án manual vì 4B chưa đạt safety gate.
5. Benchmark GPU BTC bằng `qwen3-vl:8b-instruct-q4_K_M`, context 16384/output 4096, synthetic-first; không public Ollama 11434.
6. Có thể public URL dưới nhãn supervised MVP/demo sau phone regression và live v3.1 không còn missed escalation; không public như dịch vụ production hoặc unattended pilot.

Official 15 ảnh thật không được chạy lại để làm đẹp claim. Lượt Ollama ngày 06/10 dùng **judge set synthetic 15 ca**, không phải blind holdout: 13/15, missed TK-12 và over TK-02. Evidence nằm ngoài Git tại `demo_evidence/18_setup_GPU_BTC/01_local_ollama_4b_policy_regression_2026-10-06`.

Test Kit v3 cũng là synthetic post-holdout regression: raw 11/15 và evidence tại `demo_evidence/19_regression_test_kit_v3_2026-10-06/01_live_openrouter`. Revision v3.1 là dataset/version mới; kết quả chỉ được ghi sau khi publish và chạy đúng một lượt.

## Ranh giới công bố

- Có thể nói: pipeline end-to-end chạy thật; policy C# quyết định; fail-safe; audit; holdout được báo trung thực; live technical gate phần lớn đã đạt.
- Không nói: 100% chính xác, production-ready, hỗ trợ PDF/nhiều trang, có RBAC production, hay Ollama fallback đã an toàn tương đương primary.
- GPU 16 GB từ BTC phù hợp để benchmark Ollama bằng cùng judge/regression gates, nhưng không phải điều kiện để AURA chạy demo với OpenRouter.
