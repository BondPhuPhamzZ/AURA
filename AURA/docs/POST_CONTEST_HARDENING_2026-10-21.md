# AURA sau cuộc thi: hardening và ranh giới pilot (21/10/2026)

## Kết luận hiện tại

Source có thêm hai biện pháp an toàn **cho bản demo công khai**: Verify Harness chỉ chạy trong `Development` khi được bật rõ ràng, và hai endpoint upload/Verify có admission rate limit trong từng process. Đây **không** phải xác nhận production-ready. Chưa có đăng nhập/ủy quyền thật, chưa xác nhận route AI đáp ứng Strict ZDR, chưa có chính sách lưu/xóa dữ liệu được đơn vị kiểm soát dữ liệu phê duyệt, và chưa có phép thử tải/khôi phục nhiều instance. Không đưa hóa đơn thật của người dùng lên bản public hiện tại.

## Thay đổi mã nguồn đã kiểm chứng

| Gate | Hành vi | Ranh giới |
|---|---|
| Verify | Mặc định tắt; trong Production/Staging luôn bị chặn kể cả `Verify__Enabled=true`. Nút Verify không render; `/healthz.verifyHarnessEnabled=false`. | Development vẫn cần bật có chủ đích. Không phải auth cho các endpoint còn lại. |
| Admission upload | Tối đa 15 HTTP request upload/phút/process, quá ngưỡng trả 429 JSON; không xếp request HTTP trong memory. | Không chia sẻ quota giữa instance, không chống spam phân tán. Mỗi ảnh trong lượt chọn 10 ảnh là một request. |
| Admission Verify | Tối đa một lần/2 phút/process, thêm vào gate chống chạy trùng có sẵn. | Chỉ hữu ích ở Development. |
| Strict ZDR contract | `OpenRouter__RequireZeroDataRetention=true` sinh `provider.zdr=true` cùng `data_collection=deny`; có unit test. | Chưa xác nhận model/route live còn endpoint đủ điều kiện; mặc định vẫn false và không có claim ZDR cho bản live. |

## Evidence tái lập được

- `dotnet test tests/AURA.Tests/AURA.Tests.csproj -c Release --no-restore`: **196 passed, 0 failed, 0 skipped**. Trong đó có 5 trường hợp policy Verify và 1 test payload ZDR mới.
- `dotnet build AURA.csproj -c Release --no-restore`: **0 warning, 0 error**.
- `dotnet ef migrations has-pending-model-changes --no-build`: **No changes have been made to the model since the last migration**. Không có migration mới; không chạy `database update`.
- HTTP local `ASPNETCORE_ENVIRONMENT=Production`, `Verify__Enabled=true`: app lắng nghe; `/healthz` trả `verifyHarnessEnabled=false`. Database giả dùng riêng cho smoke nên `status=degraded`, `databaseAvailable=false`; đây là test gate Verify, **không** phải health pass hay pipeline pass.
- Live URL từ môi trường kiểm thử này không truy cập được qua công cụ hiện có; **chưa publish/smoke bản hardening này trên live**. Evidence live tháng 10 trong tài liệu khác thuộc commit cũ, không được cộng gộp vào bản này.

## Chọn identity cho pilot

Chọn **một nhà cung cấp danh tính của chính doanh nghiệp**, không tự dựng mật khẩu. Nếu doanh nghiệp dùng Microsoft 365/Outlook công ty, ưu tiên Microsoft Entra ID với OpenID Connect authorization-code + PKCE; nếu dùng Google Workspace, dùng Google OIDC và kiểm tra tổ chức (`hd`) ở server. Gmail/Outlook cá nhân chỉ chứng minh chủ tài khoản email, không tự chứng minh vai trò nhân viên/quản lý. Sau đăng nhập vẫn cần RBAC/least privilege ở từng API, tenant allow-list, audit actor thật và chống truy cập chéo hồ sơ. Hiện chưa biết tenant, app registration và ma trận quyền; vì vậy **chưa bật pilot dữ liệu thật**.

## Privacy, retention và khả năng đổi model

`data_collection=deny` và Strict ZDR là hai điều kiện khác nhau. Chỉ bật `OpenRouter__RequireZeroDataRetention=true` sau một probe synthetic trên đúng model/provider/region và xác nhận request thành công mà không rơi sang route không đạt điều kiện. Nếu không có endpoint phù hợp, hệ thống phải fail closed/escalate, không tự tắt ZDR để tiếp tục. Privacy triage hiện chạy **sau** inference nên không che dữ liệu trước khi gửi AI; muốn pilot cần tiền xử lý/redaction hoặc biện pháp tương đương, threat model và thỏa thuận với provider. Kiến trúc extractor interface hiện cho phép thay adapter OpenRouter bằng local VLM sau khi benchmark lại; không được tuyên bố local model an toàn/chính xác khi chưa test.

Đề xuất **7 ngày cho ảnh gốc sau khi hồ sơ đã kết thúc** chỉ là tham số để thảo luận, không phải cam kết pháp lý hay phép xóa tự động: hồ sơ còn mở, khiếu nại/legal hold, JSON, audit, SQL/file backup và log có thể cần thời hạn khác. Đơn vị kiểm soát dữ liệu phải chốt mục đích, thông báo/đồng ý phù hợp, quyền truy cập, thời hạn từng loại, quy trình xóa có kiểm thử và yêu cầu pháp luật/kế toán áp dụng. Chưa viết job xóa vì xóa ảnh không thể hoàn tác và chưa có chính sách được duyệt.

## GO/NO-GO tiếp theo

1. **GO source hardening offline:** tests/build/EF gate ở trên đều đạt; commit/push được nhưng chưa đại diện cho live.
2. **GO demo synthetic sau publish:** backup DB và `App_Data/receipts`, publish đúng SHA, `/healthz` phải `ok`/migration 0 và `verifyHarnessEnabled=false`, một ảnh synthetic HTTP 202 → terminal, kiểm audit/CSV, kiểm 429 ở môi trường riêng không đốt AI quota.
3. **NO-GO pilot dữ liệu thật:** cần SSO + RBAC server-side, kiểm soát upload/private storage/egress, xác minh Strict ZDR hoặc hợp đồng xử lý dữ liệu phù hợp, retention được duyệt, restore drill, quan sát/alert, kiểm thử tải và security review. 196 unit tests không thay cho các gate này.
4. **NO-GO hàng trăm/ngàn ảnh/mailbox:** connector mail đã bị gỡ; worker/limiter/gate hiện theo process và chưa có capacity benchmark. Thiết kế ingestion idempotent/quarantine, distributed lease/limiter, queue/cost budget và test trên môi trường staging riêng trước khi nối email thật.

Tham chiếu chính thức: [Microsoft OIDC](https://learn.microsoft.com/aspnet/core/security/authentication/configure-oidc-web-authentication), [Google OIDC](https://developers.google.com/identity/openid-connect/openid-connect), [OpenRouter ZDR](https://openrouter.ai/docs/guides/get-started/sovereign-ai), [Luật Bảo vệ dữ liệu cá nhân 91/2025/QH15](https://congbao.chinhphu.vn/van-ban/luat-so-91-2025-qh15-45578.htm), [Nghị định 356/2025/NĐ-CP](https://congbao.chinhphu.vn/van-ban/nghi-dinh-so-356-2025-nd-cp-468371.htm). Đây là gate kỹ thuật cần đối chiếu với bộ phận pháp chế của đơn vị sử dụng, không phải kết luận pháp lý.
