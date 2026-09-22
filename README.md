# AURA - Automated Underwriting & Reimbursement AI

AURA là hệ thống hỗ trợ thẩm định hoàn ứng chi phí từ ảnh hóa đơn, phát triển cho **Track A – The Escalation Referee**.

Qwen Vision qua OpenRouter chỉ trích xuất dữ kiện có cấu trúc từ ảnh. Quyết định cuối cùng được thực hiện bởi các quy tắc nghiệp vụ tất định trong C#.

## Tài liệu nộp

| Hạng mục | Đường dẫn |
| --- | --- |
| Video Demo | [Xem video dưới 3 phút](https://drive.google.com/drive/folders/1_EHs9-KghK2JWpQGRAu_jLWl9WmBkMLc?usp=sharing) |
| Slide thuyết trình | [Tải AURA 5 Slides](https://raw.githubusercontent.com/BondPhuPhamzZ/AURA/master/AURA/submission/AURA_5_SLIDES.pptx) |
| Build Log | [Tải AURA Build Log](https://raw.githubusercontent.com/BondPhuPhamzZ/AURA/master/AURA/submission/AURA_BUILD_LOG.docx) |

## Chức năng chính

- Đọc hóa đơn JPG/PNG bằng Qwen Vision.
- Trích xuất dữ kiện theo JSON Schema.
- Đối chiếu chính sách hoàn ứng bằng C#.
- Tự động duyệt ca rõ ràng; chuyển ngoại lệ cho con người.
- Nhân viên chuyển tiếp, quản lý duyệt/từ chối và có thể hoàn tác.
- Ghi Audit Log cho toàn bộ vòng đời hồ sơ.
- Verify Harness chạy 5 ca qua pipeline thực tế.
- Chặn thao tác đồng thời để tránh ghi trùng hoặc xung đột dữ liệu.

## Luồng xử lý

```text
Upload hóa đơn
→ Qwen trích xuất dữ kiện
→ Backend kiểm tra dữ liệu
→ Policy C# ra quyết định
→ AUTO_APPROVE hoặc ESCALATE_*
→ Nhân viên chuyển tiếp
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
- Automated tests: **56/56 pass**
- Verify Harness: **5 test cases**
- Gói tham chiếu dành cho BGK: **15 test cases**

Kết quả trên fixture tổng hợp không phải tuyên bố độ chính xác trên hóa đơn thực tế độc lập.

## API key dành cho BGK

Nhóm cung cấp API key đánh giá riêng qua kênh liên hệ riêng tư.

API key không được lưu trong GitHub, README, source code, ảnh chụp hoặc video. BTC thay placeholder dưới đây bằng key đánh giá được cung cấp:

```powershell
dotnet user-secrets set "OpenRouter:ApiKey" "OPENROUTER_KEY_DUOC_CUNG_CAP_RIENG"
dotnet user-secrets set "OpenRouter:Model" "qwen/qwen3-vl-8b-instruct"
```

## Chạy local

Yêu cầu: Windows, .NET 8 SDK và SQL Server LocalDB hoặc SQL Server.

```powershell
git clone https://github.com/BondPhuPhamzZ/AURA.git
cd AURA\AURA

dotnet tool restore
dotnet restore

dotnet user-secrets set "OpenRouter:ApiKey" "OPENROUTER_KEY_DUOC_CUNG_CAP_RIENG"
dotnet user-secrets set "OpenRouter:Model" "qwen/qwen3-vl-8b-instruct"

dotnet ef database update
dotnet run
```

Mở địa chỉ localhost xuất hiện trong terminal. Sau đó:

1. Bấm **Chạy Verify Harness** để smoke-test 5 ca.
2. Hoặc tải ảnh JPG/PNG, nhập số tiền và bấm **AI tự động kiểm**.
3. Với hồ sơ `ESCALATE_*`, bấm **Chuyển tiếp**.
4. Tại tab quản lý, chọn **Đồng ý duyệt** hoặc **Từ chối duyệt**.
5. Mở **Lịch Sử Của Hệ Thống** để xem Audit Log.

### Chạy automated tests

Các test này không gọi API trả phí:

```powershell
dotnet test tests\AURA.Tests\AURA.Tests.csproj
```

## Tài liệu

- [Architecture and Integration Report](ARCHITECTURE_AND_INTEGRATION_REPORT.md)
- [Business Rules](BUSINESS_RULES.md)
- [Workflow Specification](submission/AURA_WORKFLOW_SPEC.md)
- [Test Cases](docs/TEST_CASES.md)

## Giới hạn Sprint 1

- Chỉ hỗ trợ ảnh JPG/PNG một trang, tối đa 5 MB.
- Chưa hỗ trợ PDF, tra cứu e-invoice và authentication theo vai trò.
- Hệ thống phụ thuộc OpenRouter và provider Qwen.
- Khi AI lỗi hoặc không chắc chắn, hồ sơ được chuyển cho con người thay vì tạo kết quả giả.

## Tác giả

**Phạm Gia Phú**  
Track A – The Escalation Referee
