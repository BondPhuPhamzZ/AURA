# AURA — Live post-holdout validation

Cập nhật: 06/10/2026. Live URL được kiểm trên deployment `c382ce7`; source candidate sau kiểm tra có thêm bản vá timezone Audit và 122/122 test pass.

## 1. Kết luận

Deployment `c382ce7` vẫn vận hành ổn định sau thay đổi evidence contract v2: health, database, migration, storage, UI responsive, Verify 5 ca, audit persistence và các kiểm tra bảo mật âm đều đạt. Tuy nhiên, kiểm tra Audit phát hiện giờ hiển thị đang dùng timezone của máy chủ SmarterASP thay vì giờ Việt Nam. Dữ liệu UTC trong database đúng; chỉ lớp hiển thị sai 14 giờ. Source đã thay `ToLocalTime()` bằng chuyển đổi UTC → `Asia/Ho_Chi_Minh`/`SE Asia Standard Time`, có fallback UTC+7 và test tự động.

Vì bản vá timezone chưa được publish tại thời điểm tài liệu này được tạo, trạng thái là **CONDITIONAL GO cho rehearsal, NO-GO để freeze/public live URL**. Cần publish commit mới, recycle Pool và kiểm lại Audit trước khi ký demo GO.

## 2. Evidence live

Thư mục local, không commit ảnh hay secret:

`D:\aura\demo_evidence\15_smarterasp_post_holdout_v2_c382ce7_2026-10-06`

| Nhóm | Kết quả | Bằng chứng |
|---|---|---|
| Health/public endpoint | PASS | Home HTTP 200; `/healthz` 5/5 HTTP 200 và `status=ok`; DB/storage true; pending migration 0 |
| Upload validation | PASS | JPG giả và file >5 MB đều HTTP 400 trước storage/DB/AI |
| Security negative | PASS 4/4 | `BUSINESS_RULES.md` và `appsettings.json` trả 404; POST Verify/Upload không token trả 400 |
| UI mobile browser | PASS trong viewport kiểm tra | Không vỡ ngang; ba tab, form upload, Quản lý và Audit render được; mặc định `Gia Phú` |
| Verify thật qua OpenRouter | PASS 5/5 | TC01–03 AUTO, TC04 FACT, TC05 POLICY; contract-v2 hiển thị raw date và printed-total provenance |
| Audit persistence | PASS | Năm event Verify mới tồn tại và xuất hiện trong Audit sau điều hướng |
| Audit business timezone | FAIL trên deployment, FIXED trong source | DB UTC đúng; live dùng timezone host. Candidate chuyển rõ sang UTC+7, có unit test |
| Offline regression | PASS | Build Release 0 warning/0 error; 122/122 test; EF model clean |
| Package advisory | PASS | App và test project không có package bị NuGet báo vulnerable tại thời điểm kiểm tra |

Mã năm request Verify live mới nhất:

- `0e449507`: TC01 `VERIFY_AUTO_APPROVE`;
- `ea9395ee`: TC02 `VERIFY_AUTO_APPROVE`;
- `763cf029`: TC03 `VERIFY_AUTO_APPROVE`;
- `a7173375`: TC04 `VERIFY_ESCALATE_FACT`;
- `0089a999`: TC05 `VERIFY_ESCALATE_POLICY`.

Latency quan sát lần lượt khoảng 5,415; 14,242; 9,197; 8,179; 13,797 giây. Đây là một lần smoke, không phải SLO thống kê.

## 3. Những gì không được chạy lại

Không re-upload 15 ảnh official holdout trong lượt kiểm này. Quyền cho một official run đã được sử dụng và 15 ảnh không còn blind. Re-upload cần một xác nhận mới nêu rõ đúng tập ảnh, live URL, mục đích post-holdout regression và việc ảnh tiếp tục được gửi tới SmarterASP/OpenRouter. Không dùng Verify 5 fixture để thay kết quả holdout thật.

Official holdout bất biến vẫn là:

- raw 10/15 decision match (66,67%), một missed escalation, ba over-escalation, không system error;
- adjusted 11/15 (73,33%) sau khi xác nhận BH-04 bị khóa nhãn người sai; raw không đổi;
- field 28/30 chỉ đo `currency` và `totalAmount`, không phải full extraction accuracy;
- tập 15 ca hiện là regression set, không còn là blind set.

## 4. Gate sau khi publish bản vá timezone

1. Recycle Pool ngay trước publish để nhả file lock.
2. Publish Release bằng Visual Studio và chờ `Publish succeeded`.
3. Recycle Pool lần nữa, chờ 10–30 giây.
4. `/healthz` phải HTTP 200, `status=ok`, database/storage true, pending migration 0, fallback false.
5. Chạy một Verify Harness; Audit phải hiển thị giờ Việt Nam đúng với thời điểm chạy và timeline cùng timezone.
6. Chạy `tools/Test-PublicEndpointSmoke.ps1` và `tools/Test-LiveSecuritySmoke.ps1`; không cần upload ảnh thật.
7. Trên điện thoại thật qua 4G/5G, kiểm ba tab, upload form, manager/audit và không nháy tab sai sau workflow.
8. Sau khi đạt, thực hiện ba user session có giám sát và ba dress rehearsal có bấm giờ.

Không bật `Database__ApplyMigrationsOnStartup` hoặc fallback chỉ để làm smoke. GPU/Ollama và ba user đang chờ BTC là external dependency, không phải blocker của bản vá live.
