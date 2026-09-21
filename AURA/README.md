# AURA - Automated Underwriting & Reimbursement AI

AURA là sản phẩm Track A - The Escalation Referee cho quy trình hoàn ứng chi phí. Qwen vision qua OpenRouter chỉ đọc ảnh và trả dữ kiện có cấu trúc; `PolicyDecisionEngine` của ASP.NET Core mới là thành phần quyết định tất định.

## Trạng thái Sprint 1

- Build sạch: 0 warning, 0 error.
- 54 kiểm thử tự động: 48 test chính sách/workflow/audit, 3 test hợp đồng OpenRouter/Qwen offline và 3 test toàn vẹn manifest/ảnh Test Kit (gồm gói BGK đúng 15 ca).
- Verify Vision v2: người dùng đã xác nhận local đạt đúng 5/5 ngày 22/09/2026 với 3 `AUTO_APPROVE`, 1 `ESCALATE_FACT`, 1 `ESCALATE_POLICY`. Trước video vẫn cần đúng một lượt smoke trên Live URL để xác nhận cấu hình deploy; không suy rộng 5 fixture tổng hợp thành accuracy thực tế.
- Route upload, Verify, audit, quản lý, CSRF và tra cứu chứng từ đã smoke-test local.
- Giao diện upload ba cột hiển thị trực tiếp facts AI đã đọc. Kết quả cho biết số ca tự động duyệt/chuyển tiếp; ca tự duyệt vào lịch sử, ca chuyển tiếp được giữ trong bảng kết quả ngay cả sau reload.
- Hàng đợi nhân viên, quản lý và audit cập nhật ngay sau thao tác bằng fragment AJAX, đồng thời polling nhẹ mỗi 10 giây để đồng bộ các tab đang mở.
- Live URL: **chưa điền**. Mã nguồn đã có Linux container, health endpoint và cấu hình storage/migration cho cloud; việc tạo tài nguyên bằng tài khoản của nhóm vẫn là blocker cuối trước khi nộp Sprint 1.

## Demo 90 giây

1. Mở trang chủ.
2. Bấm **Chạy Verify Harness (90s)**. Một lần bấm chạy đủ 5 ca và hiển thị expected/actual, PASS/FAIL, câu hỏi, latency và timestamp.
3. Tải một JPG/PNG mới, nhập số tiền đề nghị và bấm **AI tự động kiểm**.
4. Với ca chuyển tiếp, nhân viên bấm **Chuyển tiếp** hoặc **Chuyển tiếp tất cả**; quản lý chọn **Đồng ý duyệt/Từ chối duyệt** theo câu hỏi và hệ thống hiển thị thông báo kết quả.

Dashboard dùng `fetch` cho upload và mọi quyết định nên không tải lại toàn trang. Ảnh được xem trước ngay khi chọn; nội dung AI, bảng nhân viên, hàng đợi quản lý và lịch sử được cập nhật theo từng fragment. Các cửa sổ đang mở đồng bộ lại mỗi 10 giây.

`DecisionPolicy:EscalateDuplicateReceipts` mặc định là `false` cho buổi demo để BGK có thể dùng lại cùng fixture. Hệ thống vẫn phát hiện và ghi `DuplicateDetected` vào audit. Khi vận hành thật, đặt giá trị này thành `true` để ảnh trùng byte trở thành `ESCALATE_FACT`.
5. Mở **Lịch Sử Của Hệ Thống** để xem lần quét, lần chuyển tiếp, câu trả lời, kết quả, thời gian và hoàn tác quyết định quản lý.

## Kiến trúc quyết định

`Upload -> kiểm MIME/magic bytes/size -> lưu chứng từ riêng tư -> Qwen3-VL-8B-Instruct qua OpenRouter + JSON Schema -> hiển thị facts -> C# policy engine -> AUTO_APPROVE hoặc ESCALATE_* -> nhân viên chuyển tiếp -> quản lý Đồng ý/Từ chối -> audit trail`

Với e-commerce, schema tách riêng mã đơn hàng, mã vận chuyển, đơn vị vận chuyển, trạng thái đơn, ngày giao dịch và ngày hoàn tất. Mã vận chuyển chỉ là bằng chứng truy vết logistics; nó không thay MST và không tự chứng minh đã thanh toán.

Thứ tự ưu tiên khi có nhiều lỗi: `FACT -> POLICY -> AUTHORITY`. Input nghi vấn không bao giờ được tự động duyệt.

## Chạy local

Yêu cầu: .NET 8 SDK, SQL Server LocalDB/SQL Server, EF CLI.

```powershell
dotnet user-secrets set "OpenRouter:ApiKey" "YOUR_OPENROUTER_KEY"
dotnet user-secrets set "OpenRouter:Model" "qwen/qwen3-vl-8b-instruct"
dotnet ef database update
dotnet run
```

Chạy kiểm thử:

```powershell
dotnet build --no-restore
dotnet test tests/AURA.Tests/AURA.Tests.csproj
```

Không đặt API key trong `appsettings*.json`, Git, ảnh chụp hoặc log. Khi deploy, dùng secret `OpenRouter__ApiKey`, override `ConnectionStrings__DefaultConnection` và làm theo [hướng dẫn deploy](docs/DEPLOYMENT.md).

## Dữ liệu và quyền riêng tư

- Năm ảnh trong `wwwroot/test_data/images` là bộ Verify chạy trực tiếp. `test_kit/judge-manifest.json` khóa đúng 15 ca cho BGK; cả hai được tuyển từ ngân hàng mở rộng 30 ảnh, tạo offline với seed cố định bởi `tools/generate_verify_receipts.py`, không phải hóa đơn cá nhân thật.
- Ảnh người dùng tải lên được lưu ngoài `wwwroot` tại `App_Data/receipts`, với tên ngẫu nhiên, SHA-256 và metadata để tra cứu/audit.
- Ảnh được gửi qua OpenRouter tới provider mà router lựa chọn để Qwen trích xuất. Sản phẩm **không** phải zero-cloud/on-premise.
- Khi deploy dạng container, `ReceiptStorage__Directory` phải trỏ tới persistent volume; nếu không, file có thể mất khi container được tạo lại.

## Giới hạn công bố

- Chỉ nhận một ảnh JPG/PNG tối đa 5 MB; trình duyệt chặn file quá giới hạn trước khi gửi và server vẫn kiểm tra lại MIME, magic bytes và kích thước. Chưa hỗ trợ PDF hoặc hóa đơn nhiều trang.
- Vision không thể xác nhận tính hợp pháp/chính hãng chỉ từ pixel; AURA chỉ ghi nhận identifier và dấu hiệu nhìn thấy.
- Chưa tích hợp tra cứu mã số thuế/e-invoice bên ngoài, tỷ giá ngoại tệ hoặc antivirus.
- Kết quả 5/5 trên fixture tổng hợp không chứng minh độ chính xác trên dữ liệu độc lập.
- OpenRouter/provider có thể trả `429/5xx`; ứng dụng không retry `429`, chỉ retry tối đa một lần với lỗi `5xx`, và luôn chuyển thủ công nếu trích xuất thất bại.
- Theo dõi request, token và chi phí tại **OpenRouter → Activity**. Không chạy Verify lặp lại vì mỗi lượt dùng tối đa năm request trả phí; xem quy trình trong [runbook](docs/RUNBOOK.md).

Xem [workflow đầy đủ](submission/AURA_WORKFLOW_SPEC.md), [checklist nộp](submission/SUBMISSION_CHECKLIST.md), [case study chọn model](docs/MODEL_SELECTION_CASE_STUDY.md), [hướng dẫn deploy](docs/DEPLOYMENT.md), [runbook](docs/RUNBOOK.md), [test matrix](docs/TEST_CASES.md) và [báo cáo kiểm định](PROJECT_AUDIT.md).
