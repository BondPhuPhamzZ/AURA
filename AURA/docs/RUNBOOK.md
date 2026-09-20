# AURA Runbook

## 1. Phụ thuộc

- .NET 8 SDK
- SQL Server LocalDB trên Windows hoặc SQL Server/Azure SQL có thể truy cập
- `dotnet-ef` tương thích EF Core 8 (khuyến nghị)
- Gemini API key có quyền gọi `gemini-3.6-flash`

## 2. Clone và cấu hình local

```powershell
git clone https://github.com/BondPhuPhamzZ/AURA.git
cd AURA/AURA
dotnet restore
dotnet user-secrets set "Gemini:ApiKey" "YOUR_KEY"
dotnet ef database update
```

Không dùng `dotnet user-secrets list` khi quay video/chia sẻ màn hình vì lệnh có thể hiển thị secret.

## 3. Build, test và chạy

```powershell
dotnet build --no-restore
dotnet test tests/AURA.Tests/AURA.Tests.csproj --no-restore
dotnet run
```

Mở URL được in trong terminal. Không truy cập `/Verify` để tìm trang riêng; nút Verify nằm ngay trên trang chủ. `/Applicant` và `/Verify` chủ động chuyển về `/`.

## 4. Tái lập Verify

1. Bấm **Chạy Verify Harness (90s)** một lần.
2. Chờ năm request Gemini chạy tuần tự.
3. Xác nhận đúng 5 dòng, có timestamp và latency.
4. Kỳ vọng: TC-01..03 `AUTO_APPROVE`, TC-04 `ESCALATE_FACT`, TC-05 `ESCALATE_POLICY`.
5. Nếu `429/5xx`, chờ retry. Nếu vẫn lỗi, kết quả phải là `ESCALATE_SYSTEM_ERROR`, tuyệt đối không PASS giả.

Fixture có thể tái tạo bằng Python/Pillow qua `tools/generate_verify_receipts.py`. Không thay fixture sau khi chốt bài mà không cập nhật manifest và commit.

## 5. Cấu hình deploy

Các biến môi trường bắt buộc:

```text
Gemini__ApiKey=<secret>
Gemini__Model=gemini-3.6-flash
ConnectionStrings__DefaultConnection=<SQL Server connection string>
ReceiptStorage__Directory=<persistent volume path>
```

Quy trình release:

```powershell
dotnet test tests/AURA.Tests/AURA.Tests.csproj -c Release
dotnet publish AURA.csproj -c Release -o publish
dotnet ef database update --connection "<DEPLOYMENT_CONNECTION>"
```

Ứng dụng phải chạy sau HTTPS/reverse proxy. Persistent volume phải tồn tại qua lần restart/redeploy; kiểm tra bằng cách upload một ảnh, restart instance, rồi mở lại link **Xem hóa đơn** trong Audit.

## 6. Checklist trước nộp

- Trang chủ public trả `200`, không yêu cầu tài khoản.
- POST Verify thiếu CSRF trả `400`; nút UI có token và chạy được.
- `/BUSINESS_RULES.md` trả `404` vì policy không được public từ static root.
- Upload giả MIME bị từ chối; JPG/PNG hợp lệ được lưu và mở lại qua route chứng từ.
- Verify đạt 5/5 trong dưới 90 giây.
- Audit hiển thị input, action, timestamp, reason; approve/reject/undo hoạt động.
- Live URL đã điền trong README, slide và form nộp.
- API key/connection string không xuất hiện trong Git hoặc video.
