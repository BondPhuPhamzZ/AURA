# AURA — Live post-holdout validation

Cập nhật: 06/10/2026. Live URL được kiểm lại sau publish deployment `1f4b3c9`; đây là baseline kỹ thuật hiện hành.

## 1. Kết luận

Deployment `1f4b3c9` vận hành ổn định sau evidence contract v2 và bản vá timezone Audit. Health, database, migration, storage, upload guard, security negative, UI responsive, Verify 5 ca, audit persistence, human workflow và hoàn tác đều đạt. Audit hiển thị đúng giờ Việt Nam cho cả event mới và event cũ; UTC vẫn là nguồn dữ liệu trong database.

Trạng thái hiện tại là **GO cho technical baseline và supervised demo**. Chưa công bố production-ready hoặc accuracy-ready; trước khi đưa URL vào README cần thêm kiểm tra điện thoại thật qua 4G/5G. Ba user session và ba rehearsal vẫn là gate của vòng Chung kết.

## 2. Evidence live

Thư mục local, không commit ảnh hay secret:

`D:\aura\demo_evidence\16_smarterasp_final_1f4b3c9_2026-10-06`

| Nhóm | Kết quả | Bằng chứng |
|---|---|---|
| Health/public endpoint | PASS | Home HTTP 200; `/healthz` 5/5 HTTP 200 và `status=ok`; DB/storage true; pending migration 0 |
| Upload validation | PASS | JPG giả và file >5 MB đều HTTP 400 trước storage/DB/AI |
| Security negative | PASS 4/4 | `BUSINESS_RULES.md` và `appsettings.json` trả 404; POST Verify/Upload không token trả 400 |
| UI mobile browser | PASS ở 390×844 | Không tràn ngang trên Employee/Manager/Audit; form, card, ảnh và nút thao tác render được; đây chưa thay điện thoại thật |
| Verify thật qua OpenRouter | PASS 5/5 | TC01–03 AUTO, TC04 FACT, TC05 POLICY; contract-v2 hiển thị raw date và printed-total provenance |
| Audit persistence | PASS | Năm event Verify mới tồn tại và xuất hiện trong Audit sau điều hướng |
| Human workflow bằng fixture tổng hợp | PASS 2/2 | POLICY: chuyển tiếp → từ chối → Audit → hoàn tác; FACT: chuyển tiếp → duyệt → Audit → hoàn tác. Trạng thái cuối trở lại đúng trạng thái escalation |
| Audit business timezone | PASS live | Event Verify mới hiển thị `06/10/2026 17:13:30`; event lịch sử cũng chuyển đúng giờ Việt Nam |
| Offline regression | PASS | Build Release 0 warning/0 error; 122/122 test; EF model clean |
| Package advisory | PASS | App và test project không có package bị NuGet báo vulnerable tại thời điểm kiểm tra |

Mã năm request Verify live hậu publish:

- `018a03f5`: TC01 `VERIFY_AUTO_APPROVE`;
- `3ef00103`: TC02 `VERIFY_AUTO_APPROVE`;
- `c783807d`: TC03 `VERIFY_AUTO_APPROVE`;
- `2b80bf9e`: TC04 `VERIFY_ESCALATE_FACT`;
- `338e9d12`: TC05 `VERIFY_ESCALATE_POLICY`.

Latency quan sát lần lượt khoảng 8,049; 10,220; 21,684; 10,346; 17,187 giây. Đây là một lần smoke, không phải SLO thống kê.

Workflow live sử dụng đúng hai fixture server-owned ở trên, không upload lại hóa đơn thật. Ca `338e9d12` chuyển `ESCALATE_POLICY → REJECTED_POLICY_EXCEPTION → ESCALATE_POLICY`; ca `2b80bf9e` chuyển `ESCALATE_FACT → MANUAL_REVIEW_ACCEPTED → ESCALATE_FACT`. Audit và undo đều bền vững qua điều hướng. Trong lượt browser tự động không quan sát thấy nháy sang tab upload, nhưng vẫn giữ kiểm tra điện thoại thật trong final gate.

## 3. Những gì không được chạy lại

Không re-upload 15 ảnh official holdout trong lượt kiểm này. Quyền cho một official run đã được sử dụng và 15 ảnh không còn blind. Re-upload cần một xác nhận mới nêu rõ đúng tập ảnh, live URL, mục đích post-holdout regression và việc ảnh tiếp tục được gửi tới SmarterASP/OpenRouter. Không dùng Verify 5 fixture để thay kết quả holdout thật.

Official holdout bất biến vẫn là:

- raw 10/15 decision match (66,67%), một missed escalation, ba over-escalation, không system error;
- adjusted 11/15 (73,33%) sau khi xác nhận BH-04 bị khóa nhãn người sai; raw không đổi;
- field 28/30 chỉ đo `currency` và `totalAmount`, không phải full extraction accuracy;
- tập 15 ca hiện là regression set, không còn là blind set.

## 4. Gate sau technical baseline

Các gate publish/health/Verify/security/workflow/timezone đã đóng trên `1f4b3c9`. Việc còn lại:

1. Trên điện thoại thật qua 4G/5G, kiểm ba tab, form upload bằng một fixture không nhạy cảm, manager/audit, refresh và không nháy tab sai.
2. Tổ chức ba user session có giám sát; ghi consent, task, thiết bị/mạng, thời gian, lỗi quan sát được, phản hồi nguyên văn và action item.
3. Chạy ba dress rehearsal 8–10 phút trên cùng commit freeze; sau lượt thứ ba không đổi code nếu không có blocker.
4. Chỉ gọi 15 ảnh cũ là post-holdout regression nếu có quyền upload mới; không thay đổi raw holdout 10/15.

Không bật `Database__ApplyMigrationsOnStartup` hoặc fallback chỉ để làm smoke. GPU/Ollama và ba user đang chờ BTC là external dependency, không phải blocker của bản vá live.
