# Runbook

## 1. Môi trường yêu cầu
- .NET 8 SDK
- SQL Server (LocalDB hoặc Azure SQL)

## 2. Cấu hình
\\\ash
git clone <repo_url>
cd AURA/AURA
dotnet user-secrets set "Gemini:ApiKey" "YOUR_API_KEY"
\\\

## 3. Database Migration
\\\ash
dotnet ef database update
\\\

## 4. Chạy ứng dụng
\\\ash
dotnet run
\\\

## 5. Tái lập kết quả Verify
- Truy cập \http://localhost:5xxx/Verify\
- Bấm nút **Run 5 Canonical Cases**
- Đợi ~10-15s để hệ thống gọi AI và trả kết quả.
