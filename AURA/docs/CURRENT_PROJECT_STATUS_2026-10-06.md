# AURA — Current project status

Cập nhật: 06/10/2026. Đây là nguồn trạng thái hiện hành; các file ngày cũ hơn là lịch sử.

## Kết luận điều hành

AURA là **MVP demo candidate có điều kiện**, chưa phải production-ready hoặc accuracy-ready. Official holdout 15 hóa đơn thật đã hoàn tất một lượt và dẫn tới evidence contract v2. Deployment `c382ce7` đã pass health 5/5, security negative, upload guard, UI browser mobile và Verify OpenRouter 5/5. Kiểm tra hậu publish phát hiện một lỗi P1 ở phần hiển thị giờ Audit do timezone máy host; dữ liệu UTC không hỏng. Candidate source đã sửa về giờ Việt Nam và đạt 122/122 test, nhưng cần publish/recycle lại rồi xác nhận Audit trước khi freeze.

## Baseline đã kiểm

| Hạng mục | Trạng thái |
|---|---|
| Deployed revision | `c382ce7` — contract v2 đã live |
| Candidate kế tiếp | code fix `d9d357c` trên latest `master`; publish toàn bộ latest `master` |
| Release build | 0 warning, 0 error |
| Automated tests | 122/122 pass; offline, không gọi API AI |
| EF model | Không có pending model change |
| NuGet vulnerability scan | Không có advisory cho app/test tại thời điểm kiểm |
| Live health | 5/5 `ok`; DB/storage true; pending migration 0 |
| Live Verify | 5/5: 3 AUTO + FACT + POLICY |
| Live security negative | 6/6 nếu tính 2 upload guard + 4 route/CSRF checks |
| Official holdout raw | 10/15; missed 1; over 3; system error 0 |
| Holdout adjusted | 11/15; missed 1/10; over 2/5; raw không đổi |
| Holdout field metric | 28/30 chỉ currency + total; không phải full accuracy |
| Fallback | Tắt; Ollama chỉ opt-in/manual |

Evidence live hiện hành: [`LIVE_POST_HOLDOUT_VALIDATION_2026-10-06.md`](LIVE_POST_HOLDOUT_VALIDATION_2026-10-06.md).

## Gate còn mở

1. Publish/recycle bản vá timezone, health lại và xác nhận giờ Audit đúng Việt Nam.
2. Điện thoại thật qua 4G/5G, kiểm responsive và không nháy tab sau manager workflow.
3. Ba phiên người dùng thật có giám sát; thu consent, task, quan sát, thời gian, lỗi và feedback do người dùng xác nhận.
4. Ba dress rehearsal 8–10 phút trên cùng commit freeze, chuẩn bị local fallback nếu Internet/live URL lỗi.
5. Chỉ public live URL trong README sau khi bốn gate trên đạt hoặc ghi rõ trạng thái preview có giám sát.

Post-holdout regression bằng 15 ảnh cũ chưa được chạy lại vì quyền official upload một lượt đã hết. Nếu muốn chạy, cần xác nhận mới cho đúng 15 ảnh và gọi đúng là regression, không phải blind holdout.

## Ranh giới công bố

- Có thể nói: pipeline end-to-end chạy thật; policy C# quyết định; fail-safe; audit; holdout được báo trung thực; live technical gate phần lớn đã đạt.
- Không nói: 100% chính xác, production-ready, hỗ trợ PDF/nhiều trang, có RBAC production, hay Ollama fallback đã an toàn tương đương primary.
- GPU 16 GB từ BTC phù hợp để benchmark Ollama bằng cùng judge/regression gates, nhưng không phải điều kiện để AURA chạy demo với OpenRouter.
