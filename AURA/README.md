# AURA - Automated Underwriting & Reimbursement AI

AURA là sản phẩm Track A - The Escalation Referee cho quy trình hoàn ứng chi phí. Gemini 3.6 Flash chỉ đọc ảnh và trả dữ kiện theo JSON Schema; `PolicyDecisionEngine` của ASP.NET Core mới là thành phần quyết định tất định.

## Trạng thái Sprint 1

- Build sạch: 0 warning, 0 error.
- 48 kiểm thử tự động: 46 test chính sách/workflow (gồm e-commerce/mã vận chuyển và phạm vi MST theo loại chứng từ) cùng 2 test toàn vẹn manifest/ảnh Test Kit.
- Verify Vision v2: 5 ảnh tổng hợp đa layout gồm 3 `AUTO_APPROVE`, 1 `ESCALATE_FACT`, 1 `ESCALATE_POLICY`; đang chờ đúng một lượt benchmark sau deploy để bảo toàn quota. Kết quả 5/5 ngày 20/09/2026 thuộc fixture v1 và chỉ là lịch sử.
- Route upload, Verify, audit, quản lý, CSRF và tra cứu chứng từ đã smoke-test local.
- Giao diện upload ba cột hiển thị trực tiếp facts AI đã đọc. Kết quả cho biết số ca tự động duyệt/chuyển tiếp; ca tự duyệt vào lịch sử, ca chuyển tiếp được giữ trong bảng kết quả ngay cả sau reload.
- Hàng đợi nhân viên, quản lý và audit cập nhật ngay sau thao tác bằng fragment AJAX, đồng thời polling nhẹ mỗi 15 giây để đồng bộ các tab đang mở.
- Live URL: **chưa điền**. Mã nguồn đã có Linux container, health endpoint và cấu hình storage/migration cho cloud; việc tạo tài nguyên bằng tài khoản của nhóm vẫn là blocker cuối trước khi nộp Sprint 1.

## Demo 90 giây

1. Mở trang chủ.
2. Bấm **Chạy Verify Harness (90s)**. Một lần bấm chạy đủ 5 ca và hiển thị expected/actual, PASS/FAIL, câu hỏi, latency và timestamp.
3. Tải một JPG/PNG mới, nhập số tiền đề nghị và bấm **AI tự động kiểm**.
4. Với ca chuyển tiếp, nhân viên bấm **Chuyển tiếp** hoặc **Chuyển tiếp tất cả**; quản lý chọn **Đồng ý duyệt/Từ chối duyệt** theo câu hỏi và hệ thống hiển thị thông báo kết quả.
5. Mở **Lịch Sử Của Hệ Thống** để xem lần quét, lần chuyển tiếp, câu trả lời, kết quả, thời gian và hoàn tác quyết định quản lý.

## Kiến trúc quyết định

`Upload -> kiểm MIME/magic bytes/size -> lưu chứng từ riêng tư -> Gemini Structured Output -> hiển thị facts -> C# policy engine -> AUTO_APPROVE hoặc ESCALATE_* -> nhân viên chuyển tiếp -> quản lý Đồng ý/Từ chối -> audit trail`

Với e-commerce, schema tách riêng mã đơn hàng, mã vận chuyển, đơn vị vận chuyển, trạng thái đơn, ngày giao dịch và ngày hoàn tất. Mã vận chuyển chỉ là bằng chứng truy vết logistics; nó không thay MST và không tự chứng minh đã thanh toán.

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

Không đặt API key trong `appsettings*.json`, Git, ảnh chụp hoặc log. Khi deploy, dùng secret `Gemini__ApiKey`, override `ConnectionStrings__DefaultConnection` và làm theo [hướng dẫn deploy](docs/DEPLOYMENT.md).

## Dữ liệu và quyền riêng tư

- Năm ảnh trong `wwwroot/test_data/images` là tập đại diện lấy từ Test Kit v2 gồm 30 ảnh tại `test_kit`; được tạo offline với seed cố định bởi `tools/generate_verify_receipts.py`, không phải hóa đơn cá nhân thật.
- Ảnh người dùng tải lên được lưu ngoài `wwwroot` tại `App_Data/receipts`, với tên ngẫu nhiên, SHA-256 và metadata để tra cứu/audit.
- Ảnh được gửi đến Gemini API để trích xuất. Sản phẩm **không** phải zero-cloud/on-premise.
- Khi deploy dạng container, `ReceiptStorage__Directory` phải trỏ tới persistent volume; nếu không, file có thể mất khi container được tạo lại.

## Giới hạn công bố

- Chỉ nhận một ảnh JPG/PNG tối đa 5 MB; trình duyệt chặn file quá giới hạn trước khi gửi và server vẫn kiểm tra lại MIME, magic bytes và kích thước. Chưa hỗ trợ PDF hoặc hóa đơn nhiều trang.
- Vision không thể xác nhận tính hợp pháp/chính hãng chỉ từ pixel; AURA chỉ ghi nhận identifier và dấu hiệu nhìn thấy.
- Chưa tích hợp tra cứu mã số thuế/e-invoice bên ngoài, tỷ giá ngoại tệ hoặc antivirus.
- Kết quả 5/5 trên fixture tổng hợp không chứng minh độ chính xác trên dữ liệu độc lập.
- Free tier Gemini có rate limit và có thể trả `429/5xx`; ứng dụng retry lỗi tạm thời ba lần nhưng vẫn chuyển thủ công nếu thất bại.
- Khi gặp `429`, xem **AI Studio → Dashboard → Usage & Billing** để phân biệt RPM/TPM với RPD. RPM/TPM thường chỉ cần tạm dừng vài phút; RPD reset lúc nửa đêm Pacific. Không chạy Verify lặp lại vì mỗi lượt dùng năm request; xem quy trình và phương án model dự phòng trong [runbook](docs/RUNBOOK.md).

Xem [hướng dẫn deploy](docs/DEPLOYMENT.md), [nội dung 5 slide và kịch bản video](docs/SPRINT1_SLIDES_AND_DEMO.md), [runbook](docs/RUNBOOK.md), [test matrix](docs/TEST_CASES.md), [trạng thái Sprint 1](docs/SPRINT1_SUBMISSION.md) và [báo cáo kiểm định](PROJECT_AUDIT.md).
