# AURA Runbook

## 1. Phụ thuộc

- .NET 8 SDK
- SQL Server LocalDB trên Windows hoặc SQL Server/Azure SQL có thể truy cập
- `dotnet-ef` tương thích EF Core 8 (khuyến nghị)
- OpenRouter API key có quyền gọi model cấu hình trong `OpenRouter:Model`

## 2. Clone và cấu hình local

```powershell
git clone https://github.com/BondPhuPhamzZ/AURA.git
cd AURA/AURA
dotnet restore
dotnet user-secrets set "OpenRouter:ApiKey" "YOUR_OPENROUTER_KEY"
dotnet user-secrets set "OpenRouter:Model" "qwen/qwen3-vl-8b-instruct"
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
2. Chờ tối đa năm request Qwen chạy tuần tự qua OpenRouter.
3. Xác nhận đúng 5 dòng, có timestamp và latency.
4. Kỳ vọng fixture v2: TC-01..03 `AUTO_APPROVE`, TC-04 `ESCALATE_FACT`, TC-05 `ESCALATE_POLICY`.
5. Nếu `429/5xx`, chờ retry. Nếu vẫn lỗi, kết quả phải là `ESCALATE_SYSTEM_ERROR`, tuyệt đối không PASS giả.

Fixture có thể tái tạo bằng Python/Pillow qua `tools/generate_verify_receipts.py --as-of-date 2026-09-21`. Script đồng thời sinh Test Kit v2 gồm 30 ca nhưng chỉ 5 ca đại diện được Verify gọi. Không đổi `as-of-date`, fixture hoặc expected sau khi chốt mà không cập nhật manifest, tài liệu và commit.

Để bảo toàn credit: build + 53 automated test offline trước, deploy, chạy đúng một ảnh smoke test, sau đó chỉ chạy **một lượt** Verify 5 ảnh trước khi quay video. Không chạy tự động 15/30 ảnh tham chiếu qua API.

Trong demo, `DecisionPolicy:EscalateDuplicateReceipts=false` cho phép chạy lại cùng ảnh nhưng vẫn ghi nhận trùng trong audit. Trước production, đổi thành `true`. Thay đổi cấu hình này không cần sửa code.

Nếu một ca dừng gần đúng thời gian `OpenRouter:TimeoutSeconds`, đó là `AI_TIMEOUT`, không phải model “học kém đi”. Harness dừng gọi AI cho các ca còn lại sau timeout, rate limit, lỗi xác thực/credit/model hoặc lỗi contract JSON mang tính hệ thống để bảo vệ chi phí. HTTP 429 không được tự động retry.

### Model OpenRouter cho demo

- Mặc định demo: `qwen/qwen3-vl-8b-instruct`, model vision-language 8B tập trung OCR/document và hỗ trợ structured output bằng JSON Schema.
- Qwen3-VL-4B-Instruct là ứng viên self-host tiếp theo, nhưng không được khai báo như route OpenRouter khi catalog chưa cung cấp endpoint đó.
- HTTP 404 / `AI_MODEL_UNAVAILABLE`: slug model sai, đã bị gỡ hoặc hiện không có endpoint; đây không phải quota.
- HTTP 401/403 / `AI_AUTH_ERROR`: key sai hoặc thiếu quyền.
- HTTP 402 / `AI_CREDITS_REQUIRED`: tài khoản không đủ credit hoặc key không được phép dùng model.
- HTTP 429 / `AI_RATE_LIMIT`: rate limit của OpenRouter/provider; ứng dụng không tự retry để tránh phát sinh thêm chi phí.
- Đổi model bằng `OpenRouter:Model`; luôn xác nhận model nhận input ảnh trên catalog trước khi đổi.

### Khi OpenRouter/Qwen trả lỗi

1. Mở **OpenRouter → Activity/Logs** và đối chiếu HTTP status, model, provider, token và chi phí.
2. Với `401/403`, xác nhận secret thuộc đúng tài khoản đã nạp credit và key chưa bị thu hồi/giới hạn bởi guardrail.
3. Với `402`, kiểm tra số dư và giới hạn chi tiêu riêng của API key. `Key limit` là trần chi tiêu, không phải số dư.
4. Với `429`, dừng vài phút rồi thử đúng **một ảnh**; không bấm Verify liên tục.
5. Với `5xx/timeout`, kiểm tra trang trạng thái OpenRouter/provider. Hệ thống phải giữ `ESCALATE_SYSTEM_ERROR`, không dùng kết quả giả hoặc cache cũ như một lần gọi AI mới.
6. Chỉ đổi model sau khi chạy lại ma trận 5 ca và xác nhận input ảnh, JSON Schema, latency và câu hỏi chuyển tiếp.

## 5. Cấu hình deploy

Các biến môi trường bắt buộc:

```text
OpenRouter__ApiKey=<secret>
OpenRouter__Model=qwen/qwen3-vl-8b-instruct
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
- Verify đạt 5/5 trong thời gian demo cho phép; từng request có timeout 90 giây.
- Audit hiển thị input, action, timestamp, reason; chuyển tiếp/Đồng ý/Từ chối/undo hoạt động.
- Ca `ESCALATE_*` xuất hiện ở cửa sổ nhân viên trước; bấm **Chuyển tiếp** rồi mới xuất hiện ở cửa sổ quản lý.
- Nhân viên có thể chuyển từng hồ sơ hoặc **Chuyển tiếp tất cả**; mỗi hồ sơ phải có audit `EMPLOYEE_FORWARDED_TO_MANAGER`.
- Quản lý bấm **Đồng ý duyệt** hoặc **Từ chối duyệt**; kiểm tra toast và audit `MANAGER_YES`/`MANAGER_NO` chứa câu hỏi, câu trả lời và outcome.
- Trạng thái quyết định và audit được lưu cùng một lần EF Core `SaveChanges`, tránh trạng thái đổi nhưng thiếu nhật ký.
- Ảnh POS có MID/TID không được dùng thay số hóa đơn/biên nhận. Chỉ `VAT_INVOICE` bắt buộc seller tax ID; retail/restaurant/POS receipt có số biên nhận hợp lệ có thể không có MST. Ảnh hóa đơn nháp/chưa phát hành phải chuyển FACT.
- Ảnh e-commerce được phép thiếu MST/invoice number nếu có order/booking/tracking/receipt ID cùng trạng thái hoàn tất, ngày giao dịch/thanh toán, merchant, total, currency và line items đáng tin cậy. Ngày giao hàng không tự thay ngày giao dịch; mã vận chuyển không được gọi là MST hay hóa đơn thuế.
- Live URL đã điền trong README, slide và form nộp.
- API key/connection string không xuất hiện trong Git hoặc video.
