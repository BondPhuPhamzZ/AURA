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
4. Kỳ vọng fixture v2: TC-01..03 `AUTO_APPROVE`, TC-04 `ESCALATE_FACT`, TC-05 `ESCALATE_POLICY`.
5. Nếu `429/5xx`, chờ retry. Nếu vẫn lỗi, kết quả phải là `ESCALATE_SYSTEM_ERROR`, tuyệt đối không PASS giả.

Fixture có thể tái tạo bằng Python/Pillow qua `tools/generate_verify_receipts.py --as-of-date 2026-09-21`. Script đồng thời sinh Test Kit v2 gồm 30 ca nhưng chỉ 5 ca đại diện được Verify gọi. Không đổi `as-of-date`, fixture hoặc expected sau khi chốt mà không cập nhật manifest, tài liệu và commit.

Để bảo toàn quota: build + 46 automated test trước, deploy, chạy đúng một ảnh smoke test, sau đó chỉ chạy **một lượt** Verify 5 ảnh trước khi quay video. Không chạy tự động 30 ảnh trên free tier.

### Khi Gemini trả HTTP 429

1. Mở **Google AI Studio → Dashboard → Usage & Billing** và xem chính xác giới hạn nào đã chạm: RPM (request/phút), TPM (token/phút) hay RPD (request/ngày). Quota được tính theo **project**, không theo từng API key.
2. Nếu là RPM/TPM, dừng gọi API vài phút rồi thử lại đúng **một ảnh**. Không bấm Verify liên tục vì mỗi lần chạy tiêu thụ năm request tuần tự.
3. Nếu là RPD, chờ quota ngày reset lúc nửa đêm theo múi giờ Pacific. Trong tháng 9, thời điểm này thường tương ứng khoảng 14:00 tại Việt Nam; đồng hồ/quota trong AI Studio là nguồn xác nhận cuối cùng.
4. Không đổi model ngay trong lúc demo. Chỉ cấu hình model dự phòng sau khi chạy lại đủ ma trận 5 ca và kiểm tra JSON Schema, latency, câu hỏi chuyển tiếp.
5. Nếu cần live demo ổn định hơn free tier, nâng project chính thức lên paid tier với ngân sách/cảnh báo chi tiêu nhỏ. Không tạo nhiều project chỉ để né rate limit.

Phương án dự phòng miễn phí ưu tiên để benchmark là `gemini-3.5-flash-lite`; không tự động fallback sang model này trong production cho tới khi đạt lại 5/5 test và xác nhận cấu hình `thinkingConfig` tương thích. Khi API vẫn không khả dụng, AURA phải giữ `ESCALATE_SYSTEM_ERROR` và chuyển hồ sơ sang người quản lý, không dùng kết quả giả hoặc cache cũ như một lần gọi AI mới.

## 5. Cấu hình deploy

Các biến môi trường bắt buộc:

```text
Gemini__ApiKey=<secret>
Gemini__Model=gemini-3.6-flash
ConnectionStrings__DefaultConnection=<SQL Server connection string>
ReceiptStorage__Directory=<persistent volume path>
Database__ApplyMigrationsOnStartup=true
```

Quy trình release:

```powershell
dotnet test tests/AURA.Tests/AURA.Tests.csproj -c Release
dotnet publish AURA.csproj -c Release -o publish
dotnet ef database update --connection "<DEPLOYMENT_CONNECTION>"
```

Ứng dụng phải chạy sau HTTPS/reverse proxy. Container Linux lắng nghe cổng `8080`; health endpoint là `/healthz`. Persistent volume phải tồn tại qua lần restart/redeploy; kiểm tra bằng cách upload một ảnh, restart instance, rồi mở lại link **Xem hóa đơn** trong Audit. Hướng dẫn Azure/Render cụ thể nằm tại `docs/DEPLOYMENT.md`.

## 6. Checklist trước nộp

- Trang chủ public trả `200`, không yêu cầu tài khoản.
- POST Verify thiếu CSRF trả `400`; nút UI có token và chạy được.
- `/BUSINESS_RULES.md` trả `404` vì policy không được public từ static root.
- Upload giả MIME bị từ chối; JPG/PNG hợp lệ được lưu và mở lại qua route chứng từ.
- Ảnh lớn hơn 5 MB bị chặn với thông báo dễ hiểu, không xuất hiện lỗi `Unexpected end of JSON input`. Giới hạn multipart có phần đệm cho antiforgery/boundary nhưng controller vẫn khóa riêng file ở 5 MB.
- Verify đạt 5/5 trong dưới 90 giây.
- Audit hiển thị input, action, timestamp, reason; chuyển tiếp/Đồng ý/Từ chối/undo hoạt động.
- Ca `ESCALATE_*` xuất hiện ở cửa sổ nhân viên trước; bấm **Chuyển tiếp** rồi mới xuất hiện ở cửa sổ quản lý.
- Nhân viên có thể chuyển từng hồ sơ hoặc **Chuyển tiếp tất cả**; mỗi hồ sơ phải có audit `EMPLOYEE_FORWARDED_TO_MANAGER`.
- Quản lý bấm **Đồng ý duyệt** hoặc **Từ chối duyệt**; kiểm tra toast và audit `MANAGER_YES`/`MANAGER_NO` chứa câu hỏi, câu trả lời và outcome.
- Trạng thái quyết định và audit được lưu cùng một lần EF Core `SaveChanges`, tránh trạng thái đổi nhưng thiếu nhật ký.
- Ảnh POS có MID/TID không được dùng thay seller tax ID; ảnh hóa đơn nháp/chưa phát hành phải chuyển FACT.
- Ảnh e-commerce được phép thiếu MST/invoice number nếu có order/booking/tracking/receipt ID cùng trạng thái hoàn tất, ngày giao dịch/thanh toán, merchant, total, currency và line items đáng tin cậy. Ngày giao hàng không tự thay ngày giao dịch; mã vận chuyển không được gọi là MST hay hóa đơn thuế.
- Live URL đã điền trong README, slide và form nộp.
- API key/connection string không xuất hiện trong Git hoặc video.
