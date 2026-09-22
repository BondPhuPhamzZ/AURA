# Deploy AURA và lấy Live URL

Live URL hiện tại: `https://bondphupham-001-site1.ltempurl.com/`.

## Trang index mặc định của SmarterASP.NET

Nếu cùng một URL lúc hiện AURA, lúc hiện trang `Dear customer`, website root vẫn còn `index.html` mặc định hoặc trình duyệt đang giữ bản cache của file đó. File này không thuộc AURA. Cách an toàn là đổi tên thành `index.smarterasp-backup.html`, recycle pool, kiểm tra bằng cửa sổ ẩn danh rồi mới xóa hẳn. Không xóa `web.config`, `AURA.dll`, `wwwroot` hoặc `App_Data`.

Sau khi publish, website root phải chứa trực tiếp `web.config`, `AURA.dll`, `AURA.deps.json`, `AURA.runtimeconfig.json` và `wwwroot/`. Không để các file này nằm thêm một cấp trong thư mục `publish/`.

## Thứ tự smoke test production

1. Mở `/healthz` trong cửa sổ ẩn danh và xác nhận HTTP 200, `status: ok`, `aiConfigured: true` và `policyAvailable: true`.
2. Mở `/`, chuyển qua các tab Nhân viên, Quản lý và Lịch sử; xác nhận không còn trang `index.html` mặc định.
3. Upload đúng một ảnh tổng hợp nhỏ, kiểm preview, facts AI, quyết định và một request tương ứng trong OpenRouter Activity.
4. Với một hồ sơ `ESCALATE_*`, bấm chuyển tiếp, mở tab Quản lý, ra quyết định và xác nhận Audit Log chỉ hiển thị một hồ sơ với timeline nhất quán.
5. Recycle pool, mở lại hồ sơ/ảnh vừa tạo để kiểm tra SQL và thư mục ảnh không mất dữ liệu.
6. Chạy Verify Harness đúng một lượt, ưu tiên thực hiện ngay trong lần quay video; xác nhận đủ 5 dòng, expected/actual, PASS/FAIL, timestamp và tổng thời gian dưới 90 giây.
7. Kiểm tra lại trên điện thoại hoặc mạng 4G, sau đó mới chia sẻ Live URL.

Cập nhật: 22/09/2026. Phương án khuyến nghị cho bản nộp là **SmarterASP.NET 60-day trial**, vì AURA đang dùng ASP.NET Core 8 + SQL Server và cần lưu ảnh hóa đơn bền. Render Free chỉ nên dùng làm preview stateless.

## 1. Chuẩn bị local

1. Bảo đảm nhánh nộp đã push lên GitHub và không có secret trong Git.
2. Chạy:

```powershell
dotnet build --no-restore
dotnet test tests/AURA.Tests/AURA.Tests.csproj --no-restore
```

Kỳ vọng hiện tại: build 0 warning/error và 56/56 test pass. Không cần gọi API AI ở bước này.

3. Trong Visual Studio, mở `AURA.csproj` và chọn **Publish → Folder** hoặc **Publish → Web Deploy**. Target framework là `net8.0`, cấu hình `Release`.

## 2. Tạo trial SmarterASP.NET

1. Đăng ký tại `https://www.smarterasp.net/free_trial` bằng tài khoản của nhóm.
2. Từ màn hình **Các gói hosting**, tại thẻ `bondphupham-001` / plan `W60-US`, bấm nút xanh **Quản lý**. Không cần bấm `+ Đơn hàng mới`, `Kế hoạch nâng cấp`, `Tên miền`, `VPN` hoặc `MCP/API`.
3. Trong Hosting Control Panel vừa mở, vào **Websites**. Chọn website `aura`/temporary site rồi bấm **Manage Website**. Ghi lại Temporary URL và đặt website type/version là ASP.NET Core tương thích .NET 8.
4. Vào **Databases → MSSQL → + Add Database**, chọn SQL Server version còn được gói trial hỗ trợ, đặt tên database và mật khẩu riêng rồi copy connection string. Không chụp/commit mật khẩu.
5. Kiểm tra hosting plan đã bật ASP.NET Core 8 và application pool 64-bit. Nếu runtime không có, mở ticket support trước khi deploy.

## 3. Cấu hình biến môi trường

Trong SmarterASP Control Panel V10, mở **Advanced Tools → Pool Manager → Actions → Environment Variables**. Biến được đặt ở application-pool level; dùng tên duy nhất nếu pool chứa nhiều site.

Thêm:

```text
ASPNETCORE_ENVIRONMENT=Production
OpenRouter__ApiKey=<OPENROUTER_KEY_MỚI>
OpenRouter__Model=qwen/qwen3-vl-8b-instruct
ConnectionStrings__DefaultConnection=<SQL_SERVER_CONNECTION_STRING>
Database__ApplyMigrationsOnStartup=true
ReceiptStorage__Directory=App_Data/receipts
ReceiptStorage__MaxFileSizeMb=5
```

Sau migration đầu tiên chạy thành công, nên đổi `Database__ApplyMigrationsOnStartup=false` và recycle application pool. Không đặt key vào `appsettings.json`, publish profile hoặc ảnh/video.

## 4. Publish bằng Web Deploy

1. Trong SmarterASP control panel, mở thông tin **Web Deploy** và tải/copy profile.
2. Visual Studio → **Publish → Import Profile**, chọn profile vừa tải.
3. Chọn **Settings**:
   - Configuration: `Release`;
   - Target Framework: `net8.0`;
   - Deployment mode: Framework-dependent;
   - không xóa file trong `App_Data/receipts` khi publish lại.
4. Bấm **Validate Connection**, sau đó **Publish**.
5. Nếu dùng Folder publish thay vì Web Deploy, upload toàn bộ nội dung thư mục publish bằng File Manager/FTP vào web root, không upload source tree.

## 5. Xác minh ngay sau deploy

Thực hiện theo thứ tự để không tốn quota vô ích:

1. Mở `https://<live-url>/healthz`, kỳ vọng HTTP 200 và JSON `status: ok`.
2. Mở trang chủ ở cửa sổ ẩn danh, kiểm CSS/JS/ba tab.
3. Mở tab quản lý và lịch sử để xác nhận SQL Server/migration hoạt động.
4. Upload một ảnh smoke tổng hợp; xác nhận preview, facts và bảng kết quả cập nhật không reload trang.
5. Mở lại ảnh từ lịch sử, recycle app pool rồi mở lại lần nữa để xác minh file tồn tại.
6. Chỉ khi 5 bước trên đạt, chạy đúng một lượt Verify Harness và ghi thời điểm, 5 expected/actual, tổng latency và OpenRouter cost.
7. Đổi migration flag về `false`, recycle pool và kiểm `/healthz` lần cuối.

## 6. Xử lý lỗi thường gặp

| Hiện tượng | Kiểm tra |
|---|---|
| 500.30/502.5 | Runtime .NET 8, startup log tạm thời, connection string và environment variables |
| Database login failed | server/database/user/password, firewall/provider connection string |
| `AI_NOT_CONFIGURED`/401 | `OpenRouter__ApiKey` ở đúng app pool, recycle sau khi sửa |
| `POLICY_NOT_FOUND` hoặc `policyAvailable: false` | Publish lại bản mới có `BUSINESS_RULES.md` cạnh `AURA.dll`; không chỉ upload riêng DLL |
| 404 ảnh sau recycle | quyền ghi `App_Data/receipts` và publish không xóa folder |
| 429/hết credit | dừng Verify, kiểm OpenRouter Activity/Credits; không tạo key mới để né limit |
| App chạy nhưng redirect HTTPS lỗi | giữ URL HTTPS do host cấp và kiểm proxy/header; không hard-code domain |

Chỉ bật stdout startup log khi cần chẩn đoán, tải log về rồi tắt lại để tránh đầy storage hoặc lộ cấu hình.

## 7. Vì sao không chọn Render Free cho bản nộp

Render chạy được .NET qua Dockerfile và cấp URL `onrender.com`, nhưng Free Web Service:

- spin down sau 15 phút không hoạt động và có thể mất khoảng một phút để wake;
- dùng filesystem tạm, mất ảnh upload khi restart/redeploy/spin-down;
- không gắn persistent disk ở gói Free;
- Render Postgres không dùng trực tiếp với provider EF SQL Server hiện tại và free database hết hạn sau 30 ngày.

Muốn dùng Render ổn định phải trả phí Web Service + persistent disk và dùng SQL Server bên ngoài, hoặc đổi code sang PostgreSQL/object storage. Đây là thay đổi kiến trúc không nên làm sát deadline. Tài liệu chính thức: `https://render.com/docs/free`, `https://render.com/docs/docker`, `https://render.com/docs/disks`.

## 8. Điều kiện coi deploy hoàn tất

- Live URL HTTPS mở được ở cửa sổ ẩn danh, không cần login.
- `/healthz` trả 200.
- SQL migration hoàn tất; ba tab đọc được dữ liệu.
- Ảnh vẫn mở sau recycle/redeploy kiểm soát.
- Không có secret trong Git, log, slide hoặc video.
- Một ảnh smoke và đúng một lượt Verify hoạt động; lỗi provider phải fail-safe chứ không giả PASS.
- URL được điền đồng nhất vào README, slide, video description và form nộp.
