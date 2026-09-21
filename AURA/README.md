# AURA - Automated Underwriting & Reimbursement AI

AURA là sản phẩm Track A - The Escalation Referee cho quy trình hoàn ứng chi phí. Gemini 3.6 Flash chỉ đọc ảnh và trả dữ kiện theo JSON Schema; `PolicyDecisionEngine` của ASP.NET Core mới là thành phần quyết định tất định.

## Trạng thái Sprint 1

- Build sạch: 0 warning, 0 error.
- 39 kiểm thử chính sách và workflow tự động: đạt 39/39.
- Verify Vision: 5 ảnh tổng hợp, đạt 5/5 với Gemini 3.6 Flash ngày 20/09/2026; gồm 3 `AUTO_APPROVE`, 1 `ESCALATE_FACT`, 1 `ESCALATE_POLICY`.
- Route upload, Verify, audit, quản lý, CSRF và tra cứu chứng từ đã smoke-test local.
- Live URL: **chưa điền**. Đây là blocker cuối trước khi nộp Sprint 1.

## Demo 90 giây

1. Mở trang chủ.
2. Bấm **Chạy Verify Harness (90s)**. Một lần bấm chạy đủ 5 ca và hiển thị expected/actual, PASS/FAIL, câu hỏi, latency và timestamp.
3. Tải một JPG/PNG mới, nhập số tiền đề nghị và bấm **AI tự động kiểm**.
4. Với ca chuyển tiếp, nhân viên bấm **Chuyển tiếp** hoặc **Chuyển tiếp tất cả**; quản lý chọn **Đồng ý duyệt/Từ chối duyệt** theo câu hỏi và hệ thống hiển thị thông báo kết quả.
5. Mở **Lịch Sử Của Hệ Thống** để xem lần quét, lần chuyển tiếp, câu trả lời, kết quả, thời gian và hoàn tác quyết định quản lý.

## Kiến trúc quyết định

`Upload -> kiểm MIME/magic bytes/size -> lưu chứng từ riêng tư -> Gemini Structured Output -> C# policy engine -> AUTO_APPROVE hoặc ESCALATE_* -> nhân viên chuyển tiếp -> quản lý Đồng ý/Từ chối -> audit trail`

Thứ tự ưu tiên khi có nhiều lỗi: `FACT -> POLICY -> AUTHORITY`. Input nghi vấn không bao giờ được tự động duyệt.

## Chạy local

Yêu cầu: .NET 8 SDK, SQL Server LocalDB/SQL Server, EF CLI.

```powershell
dotnet user-secrets set "Gemini:ApiKey" "YOUR_KEY"
dotnet ef database update
dotnet run
```

Chạy kiểm thử:

```powershell
dotnet build --no-restore
dotnet test tests/AURA.Tests/AURA.Tests.csproj
```

Không đặt API key trong `appsettings*.json`, Git, ảnh chụp hoặc log. Khi deploy, dùng secret `Gemini__ApiKey` và override `ConnectionStrings__DefaultConnection`.

## Dữ liệu và quyền riêng tư

- Năm ảnh trong `wwwroot/test_data/images` là dữ liệu tổng hợp, được tạo bởi `tools/generate_verify_receipts.py`; không phải hóa đơn của cá nhân thật.
- Ảnh người dùng tải lên được lưu ngoài `wwwroot` tại `App_Data/receipts`, với tên ngẫu nhiên, SHA-256 và metadata để tra cứu/audit.
- Ảnh được gửi đến Gemini API để trích xuất. Sản phẩm **không** phải zero-cloud/on-premise.
- Khi deploy dạng container, `ReceiptStorage__Directory` phải trỏ tới persistent volume; nếu không, file có thể mất khi container được tạo lại.

## Giới hạn công bố

- Chỉ nhận một ảnh JPG/PNG tối đa 5 MB; chưa hỗ trợ PDF hoặc hóa đơn nhiều trang.
- Vision không thể xác nhận tính hợp pháp/chính hãng chỉ từ pixel; AURA chỉ ghi nhận identifier và dấu hiệu nhìn thấy.
- Chưa tích hợp tra cứu mã số thuế/e-invoice bên ngoài, tỷ giá ngoại tệ hoặc antivirus.
- Kết quả 5/5 trên fixture tổng hợp không chứng minh độ chính xác trên dữ liệu độc lập.
- Free tier Gemini có rate limit và có thể trả `429/5xx`; ứng dụng retry lỗi tạm thời ba lần nhưng vẫn chuyển thủ công nếu thất bại.
- Khi gặp `429`, xem **AI Studio → Dashboard → Usage & Billing** để phân biệt RPM/TPM với RPD. RPM/TPM thường chỉ cần tạm dừng vài phút; RPD reset lúc nửa đêm Pacific. Không chạy Verify lặp lại vì mỗi lượt dùng năm request; xem quy trình và phương án model dự phòng trong [runbook](docs/RUNBOOK.md).

Xem [runbook](docs/RUNBOOK.md), [test matrix](docs/TEST_CASES.md), [trạng thái Sprint 1](docs/SPRINT1_SUBMISSION.md) và [báo cáo kiểm định](PROJECT_AUDIT.md).
