# AURA — Current project status

Cập nhật: 06/10/2026. Đây là nguồn trạng thái hiện hành; các file ngày cũ hơn là lịch sử.

## Kết luận điều hành

AURA là **MVP technical baseline sẵn sàng cho supervised demo**, chưa phải production-ready hoặc accuracy-ready. Official holdout 15 hóa đơn thật đã hoàn tất một lượt và dẫn tới evidence contract v2. Deployment `1f4b3c9` đã pass health 5/5, security negative 6/6, UI browser mobile, Verify OpenRouter 5/5, workflow fixture 2/2 và Audit timezone Việt Nam. Build/test/EF/package gates đều sạch.

## Baseline đã kiểm

| Hạng mục | Trạng thái |
|---|---|
| Deployed revision | `1f4b3c9` — contract v2 và bản vá Audit timezone đã live |
| Candidate kế tiếp | Chưa có code candidate mới; chỉ đồng bộ tài liệu/evidence sau xác minh live |
| Release build | 0 warning, 0 error |
| Automated tests | 122/122 pass; offline, không gọi API AI |
| EF model | Không có pending model change |
| NuGet vulnerability scan | Không có advisory cho app/test tại thời điểm kiểm |
| Live health | 5/5 `ok`; DB/storage true; pending migration 0 |
| Live Verify | 5/5: 3 AUTO + FACT + POLICY |
| Live human workflow | 2/2 hậu publish: policy reject/undo và fact approve/undo |
| Live security negative | 6/6 nếu tính 2 upload guard + 4 route/CSRF checks |
| Official holdout raw | 10/15; missed 1; over 3; system error 0 |
| Holdout adjusted | 11/15; missed 1/10; over 2/5; raw không đổi |
| Holdout field metric | 28/30 chỉ currency + total; không phải full accuracy |
| Fallback | Tắt; Ollama chỉ opt-in/manual |

Evidence live hiện hành: [`LIVE_POST_HOLDOUT_VALIDATION_2026-10-06.md`](LIVE_POST_HOLDOUT_VALIDATION_2026-10-06.md).

## Gate còn mở

1. Điện thoại thật qua 4G/5G, kiểm responsive, upload fixture, refresh và không nháy tab sau manager workflow.
2. Ba phiên người dùng thật có giám sát; thu consent, task, quan sát, thời gian, lỗi và feedback do người dùng xác nhận.
3. Ba dress rehearsal 8–10 phút trên cùng commit freeze, chuẩn bị local fallback nếu Internet/live URL lỗi.
4. Có thể public URL dưới nhãn supervised MVP/demo sau gate điện thoại; không public như dịch vụ production hoặc unattended pilot.

Post-holdout regression bằng 15 ảnh cũ chưa được chạy lại vì quyền official upload một lượt đã hết. Nếu muốn chạy, cần xác nhận mới cho đúng 15 ảnh và gọi đúng là regression, không phải blind holdout.

## Ranh giới công bố

- Có thể nói: pipeline end-to-end chạy thật; policy C# quyết định; fail-safe; audit; holdout được báo trung thực; live technical gate phần lớn đã đạt.
- Không nói: 100% chính xác, production-ready, hỗ trợ PDF/nhiều trang, có RBAC production, hay Ollama fallback đã an toàn tương đương primary.
- GPU 16 GB từ BTC phù hợp để benchmark Ollama bằng cùng judge/regression gates, nhưng không phải điều kiện để AURA chạy demo với OpenRouter.
