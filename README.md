# AURA - Automated Underwriting and Reimbursement AI

AURA là hệ thống hỗ trợ thẩm định hoàn ứng chi phí từ ảnh hóa đơn, được phát triển cho **Track A - The Escalation Referee**.

Qwen Vision qua OpenRouter trích xuất dữ kiện từ ảnh. Các quy tắc C# sau đó quyết định tự động duyệt hoặc chuyển hồ sơ cho con người xử lý.

## Tài liệu nộp

| Hạng mục | Đường dẫn |
|---|---|
| Video Demo | [Xem video dưới 3 phút](https://drive.google.com/drive/folders/1_EHs9-KghK2JWpQGRAu_jLWl9WmBkMLc?usp=sharing) |
| Slide thuyết trình | [Tải AURA 5 Slides](https://github.com/BondPhuPhamzZ/AURA/raw/refs/heads/master/AURA/submission/AURA_5_SLIDES.pptx) |
| Build Log | [Tải AURA Build Log](https://github.com/BondPhuPhamzZ/AURA/raw/refs/heads/master/AURA/submission/AURA_BUILD_LOG.docx) |

## Chức năng chính

- Đọc ảnh hóa đơn JPG/PNG bằng Qwen Vision.
- Trả dữ kiện có cấu trúc theo JSON Schema.
- Tự động đối chiếu chính sách hoàn ứng.
- Tự duyệt trường hợp rõ ràng.
- Chuyển ngoại lệ cho nhân viên và quản lý.
- Ghi Audit Log và hỗ trợ hoàn tác quyết định.
- Verify Harness chạy năm test case qua pipeline thực tế.

## Luồng xử lý

```text
Upload hóa đơn
→ Qwen trích xuất dữ kiện
→ Backend kiểm tra dữ liệu
→ Policy C# ra quyết định
→ AUTO_APPROVE hoặc ESCALATE
→ Quản lý xử lý ngoại lệ
→ Audit Log
```

AURA không fine-tune Qwen và không giao toàn bộ quyền quyết định cho AI. Model chỉ đọc dữ kiện; `PolicyDecisionEngine` áp dụng quy tắc nghiệp vụ tất định.

## Công nghệ

- ASP.NET Core 8 MVC
- Entity Framework Core và SQL Server
- Qwen3-VL-8B-Instruct qua OpenRouter
- JSON Schema Structured Output
- Razor Views và JavaScript Fetch API

## Kiểm thử

- Build: **0 warning, 0 error**
- Automated tests: **55/55 pass**
- Verify Vision v2 local: **5/5 đúng**
- Gói tham chiếu dành cho BGK: **15 test cases**

Kết quả trên fixture tổng hợp không phải tuyên bố độ chính xác trên hóa đơn thực tế độc lập.

## API key dành cho BGK

Nhóm cung cấp **API key để test**: 
"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"
Sau khi nhận key, BGK thay `OPENROUTER_KEY_DUOC_CUNG_CAP_RIENG` trong lệnh bên dưới. `dotnet user-secrets` lưu key bên ngoài repository nên không làm thay đổi source code.

## Chạy local

Yêu cầu: Windows, .NET 8 SDK và SQL Server LocalDB/SQL Server.

```powershell
git clone https://github.com/BondPhuPhamzZ/AURA.git
cd AURA\AURA

dotnet tool restore
dotnet restore
dotnet user-secrets set "OpenRouter:ApiKey" "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"
dotnet user-secrets set "OpenRouter:Model" "qwen/qwen3-vl-8b-instruct"

dotnet ef database update
dotnet run
```

Mở địa chỉ localhost được in trong terminal. Sau đó:

1. Bấm **Chạy Verify Harness** để smoke-test 5 ca qua API thật.
2. Hoặc tải một ảnh JPG/PNG, nhập số tiền đề nghị và bấm **AI tự động kiểm**.
3. Với hồ sơ `ESCALATE_*`, bấm **Chuyển tiếp**.
4. Mở tab quản lý để **Đồng ý duyệt** hoặc **Từ chối duyệt**.
5. Mở **Lịch Sử Của Hệ Thống** để kiểm tra Audit Log.

### Chạy automated tests

Các test này không gọi API trả phí:

```powershell
dotnet test tests\AURA.Tests\AURA.Tests.csproj
```

## Tài liệu

- [Architecture and Integration Report](AURA/ARCHITECTURE_AND_INTEGRATION_REPORT.md)
- [Business Rules](AURA/BUSINESS_RULES.md)
- [Workflow Specification](AURA/submission/AURA_WORKFLOW_SPEC.md)
- [Test Cases](AURA/docs/TEST_CASES.md)

## Giới hạn Sprint 1

- Chỉ hỗ trợ ảnh JPG/PNG một trang, tối đa 5 MB.
- Chưa hỗ trợ PDF, tra cứu e-invoice và authentication theo vai trò.
- Hệ thống phụ thuộc OpenRouter và provider Qwen.
- Khi AI gặp lỗi hoặc không chắc chắn, hồ sơ được chuyển cho con người thay vì tạo kết quả giả.

## Tác giả

**Phạm Gia Phú**  
Track A - The Escalation Referee
