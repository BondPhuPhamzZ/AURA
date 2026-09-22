# AURA - Automated Underwriting and Reimbursement AI

AURA là hệ thống hỗ trợ thẩm định hoàn ứng chi phí từ ảnh hóa đơn, được phát triển cho **Track A - The Escalation Referee**.

Qwen Vision qua OpenRouter trích xuất dữ kiện từ ảnh. Các quy tắc C# sau đó quyết định tự động duyệt hoặc chuyển hồ sơ cho con người xử lý.

## Demo

| Hạng mục | Đường dẫn |
|---|---|
| Live Application | [Mở AURA](https://bondphupham-001-site1.ltempurl.com/) |
| Video Demo | [Xem video dưới 3 phút](https://drive.google.com/drive/folders/1_EHs9-KghK2JWpQGRAu_jLWl9WmBkMLc?usp=sharing) |
| Source Code | [GitHub Repository](https://github.com/BondPhuPhamzZ/AURA) |
| Slide thuyết trình | [Tải AURA 5 Slides](https://github.com/BondPhuPhamzZ/AURA/raw/refs/heads/master/AURA/submission/AURA_5_SLIDES.pptx) |
| Build Log | [Tải AURA Build Log](https://github.com/BondPhuPhamzZ/AURA/raw/refs/heads/master/AURA/submission/AURA_BUILD_LOG.docx) |

> Đây là môi trường demo công khai. Không tải lên hóa đơn thật chứa dữ liệu cá nhân.

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
- SmarterASP.NET

## Kiểm thử

- Build: **0 warning, 0 error**
- Automated tests: **54/54 pass**
- Verify Vision v2 local: **5/5 đúng**
- Gói tham chiếu dành cho BGK: **15 test cases**

Kết quả trên fixture tổng hợp không phải tuyên bố độ chính xác trên hóa đơn thực tế độc lập.

## Chạy local

```powershell
dotnet user-secrets set "OpenRouter:ApiKey" "YOUR_OPENROUTER_API_KEY"
dotnet user-secrets set "OpenRouter:Model" "qwen/qwen3-vl-8b-instruct"

dotnet ef database update
dotnet run
```

Không lưu API key trong source code hoặc GitHub.

## Tài liệu

- [Architecture and Integration Report](ARCHITECTURE_AND_INTEGRATION_REPORT.md)
- [Business Rules](BUSINESS_RULES.md)
- [Workflow Specification](submission/AURA_WORKFLOW_SPEC.md)
- [Deployment Guide](docs/DEPLOYMENT.md)
- [Test Cases](docs/TEST_CASES.md)

## Giới hạn Sprint 1

- Chỉ hỗ trợ ảnh JPG/PNG một trang, tối đa 5 MB.
- Chưa hỗ trợ PDF, tra cứu e-invoice và authentication theo vai trò.
- Hệ thống phụ thuộc OpenRouter và provider Qwen.
- Khi AI gặp lỗi hoặc không chắc chắn, hồ sơ được chuyển cho con người thay vì tạo kết quả giả.

## Tác giả

**Phạm Gia Phú**  
Track A - The Escalation Referee
