# Triển khai AURA và lấy Live URL

## 1. Trạng thái hiện tại

Project đã sẵn sàng để publish .NET 8 trên Linux. Dockerfile dùng build nhiều tầng, chạy bằng user không đặc quyền, lắng nghe cổng `8080` và có health endpoint `/healthz`. Database production vẫn phải là SQL Server/Azure SQL; LocalDB trong `appsettings.json` chỉ dùng trên Windows local.

Không có nền tảng nào được xem là đã deploy cho tới khi URL public trả `200`, database hoạt động và ảnh vẫn mở lại sau một lần restart/redeploy.

## 2. Phương án khuyến nghị cho Sprint 1: Azure App Service

Azure phù hợp nhất với stack hiện tại vì hỗ trợ ASP.NET Core, Azure SQL và vùng lưu bền `/home`. Tạo tài nguyên bằng tài khoản của nhóm; không gửi publish profile, API key hay connection string qua chat/Git.

1. Tạo Azure SQL Database và ghi lại connection string có mã hóa.
2. Tạo Web App Linux chạy container .NET 8 hoặc deploy source .NET 8 từ GitHub.
3. Trong **Configuration / Environment variables**, thêm:

```text
ASPNETCORE_ENVIRONMENT=Production
Gemini__ApiKey=<secret>
Gemini__Model=gemini-3.6-flash
ConnectionStrings__DefaultConnection=<Azure SQL connection string>
Database__ApplyMigrationsOnStartup=true
ReceiptStorage__Directory=/home/data/receipts
ReceiptStorage__MaxFileSizeMb=5
WEBSITES_ENABLE_APP_SERVICE_STORAGE=true
```

4. Nếu dùng container, đặt port ứng dụng là `8080`; health check path là `/healthz`.
5. Deploy commit đã chốt từ nhánh `master`. Sau lần khởi động thành công đầu tiên, có thể giữ migration flag ở `true` cho demo một instance hoặc chuyển về `false` và chạy migration như một release step riêng.
6. Mở `https://<app-name>.azurewebsites.net/healthz`; kỳ vọng JSON có `status: ok`.
7. Upload một ảnh test, mở lại chứng từ, restart Web App và xác minh ảnh cùng audit vẫn còn.

## 3. Render Free chỉ là phương án demo tạm

Render có thể build Dockerfile khi đặt **Root Directory** là `AURA`, health path `/healthz` và các biến môi trường tương tự. Tuy nhiên filesystem của Web Service Free là tạm thời và không hỗ trợ persistent disk. Vì vậy không dùng Render Free làm URL nộp cuối nếu yêu cầu lưu chứng từ qua restart/redeploy chưa được giải quyết bằng object storage. Render paid + persistent disk có thể đặt `ReceiptStorage__Directory=/app/storage/receipts`.

## 4. Kiểm chứng không tốn quota Gemini

Thực hiện trước theo thứ tự:

1. `dotnet build --no-restore`.
2. `dotnet test tests/AURA.Tests/AURA.Tests.csproj --no-restore` — hiện có 48 test offline.
3. Gọi `/healthz`, mở ba tab và kiểm tra responsive/audit.
4. Kiểm tra upload file quá 5 MB bị chặn tại client; bước này không gửi Gemini.
5. Chỉ sau khi deploy ổn định mới gọi một ảnh smoke test, rồi đúng một lượt Verify 5 ảnh.

Không chạy 30 ảnh Test Kit qua API trước demo. Giữ ít nhất 10 request dự phòng cho video và BGK.

## 5. Điều kiện hoàn tất deploy

- Live URL HTTPS truy cập được từ cửa sổ ẩn danh.
- `/healthz` trả `200`.
- Database migration hoàn tất và các tab đọc được dữ liệu.
- Ảnh upload và audit còn tồn tại sau restart.
- Không có secret trong log, Git, ảnh hoặc video.
- Một ảnh smoke test thành công; Verify v2 đạt 5/5 dưới 90 giây hoặc ghi đúng lỗi/quota, không tạo PASS giả.
- Live URL được điền đồng nhất vào README, slide, mô tả video và form nộp.
