# AURA - Automated Underwriting & Reimbursement AI

AURA là sản phẩm Track A - The Escalation Referee cho quy trình hoàn ứng chi phí. Qwen vision qua OpenRouter chỉ đọc ảnh và trả dữ kiện có cấu trúc; `PolicyDecisionEngine` của ASP.NET Core mới là thành phần quyết định tất định.

## Trạng thái Sprint 1

- Build sạch: 0 warning, 0 error.
- 55 kiểm thử tự động: chính sách/workflow/audit, hợp đồng OpenRouter/Qwen offline, retry structured output và tính toàn vẹn manifest/ảnh Test Kit (gồm gói BGK đúng 15 ca).
- Verify Vision v2: local đạt đúng 5/5 ngày 22/09/2026 với 3 `AUTO_APPROVE`, 1 `ESCALATE_FACT`, 1 `ESCALATE_POLICY`; không suy rộng 5 fixture tổng hợp thành accuracy thực tế.
- Route upload, Verify, audit, quản lý, CSRF và tra cứu chứng từ đã smoke-test local.
- Giao diện upload ba cột hiển thị trực tiếp facts AI đã đọc. Kết quả cho biết số ca tự động duyệt/chuyển tiếp; ca tự duyệt vào lịch sử, ca chuyển tiếp được giữ trong bảng kết quả ngay cả sau reload.
- Hàng đợi nhân viên, quản lý và audit cập nhật ngay sau thao tác bằng fragment AJAX, đồng thời polling nhẹ mỗi 10 giây để đồng bộ các tab đang mở.

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

Nhóm cung cấp API key đánh giá tạm thời cho BGK qua kênh riêng. Key thật không được lưu trong repository. Thay placeholder dưới đây bằng key được cung cấp:

```powershell
dotnet user-secrets set "OpenRouter:ApiKey" "OPENROUTER_KEY_DUOC_CUNG_CAP_RIENG"
dotnet user-secrets set "OpenRouter:Model" "qwen/qwen3-vl-8b-instruct"
dotnet ef database update
dotnet run
```

Chạy kiểm thử:

```powershell
dotnet build --no-restore
dotnet test tests/AURA.Tests/AURA.Tests.csproj
```

Không đặt API key trong `appsettings*.json`, Git, ảnh chụp hoặc log.

## Dữ liệu và quyền riêng tư

- Năm ảnh trong `wwwroot/test_data/images` là bộ Verify chạy trực tiếp. `test_kit/judge-manifest.json` khóa đúng 15 ca cho BGK; cả hai được tuyển từ ngân hàng mở rộng 30 ảnh, tạo offline với seed cố định bởi `tools/generate_verify_receipts.py`, không phải hóa đơn cá nhân thật.
- Ảnh người dùng tải lên được lưu ngoài `wwwroot` tại `App_Data/receipts`, với tên ngẫu nhiên, SHA-256 và metadata để tra cứu/audit.
- Ảnh được gửi qua OpenRouter tới provider mà router lựa chọn để Qwen trích xuất. Sản phẩm **không** phải zero-cloud/on-premise.

## Giới hạn công bố

- Chỉ nhận một ảnh JPG/PNG tối đa 5 MB; trình duyệt chặn file quá giới hạn trước khi gửi và server vẫn kiểm tra lại MIME, magic bytes và kích thước. Chưa hỗ trợ PDF hoặc hóa đơn nhiều trang.
- Vision không thể xác nhận tính hợp pháp/chính hãng chỉ từ pixel; AURA chỉ ghi nhận identifier và dấu hiệu nhìn thấy.
- Chưa tích hợp tra cứu mã số thuế/e-invoice bên ngoài, tỷ giá ngoại tệ hoặc antivirus.
- Kết quả 5/5 trên fixture tổng hợp không chứng minh độ chính xác trên dữ liệu độc lập.
- OpenRouter/provider có thể trả `429/5xx` hoặc JSON hỏng; ứng dụng bật response healing, retry giới hạn với `5xx`/structured output hỏng và luôn chuyển thủ công nếu trích xuất vẫn thất bại.
- Theo dõi request, token và chi phí tại **OpenRouter → Activity**. Không chạy Verify lặp lại vì mỗi lượt dùng tối đa năm request trả phí; xem quy trình trong [runbook](docs/RUNBOOK.md).

Xem [báo cáo kiến trúc và tích hợp](ARCHITECTURE_AND_INTEGRATION_REPORT.md), [workflow đầy đủ](submission/AURA_WORKFLOW_SPEC.md), [checklist nộp](docs/SUBMISSION_CHECKLIST.md), [Build Log nguồn](docs/BUILD_LOG.md), [kịch bản video](docs/VIDEO_DEMO_SCRIPT.md), [case study chọn model](docs/MODEL_SELECTION_CASE_STUDY.md), [runbook](docs/RUNBOOK.md) và [test matrix](docs/TEST_CASES.md).
