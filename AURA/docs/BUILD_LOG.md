# Build Log - Sprint 1 và hardening Sprint 2

## Công cụ AI và cách sử dụng

- **Qwen3-VL-8B-Instruct qua OpenRouter:** đọc một ảnh hóa đơn và trả dữ kiện theo JSON Schema chặt. Qwen không được quyền tự quyết định duyệt/chuyển tiếp.
- **Codex:** đọc code/tài liệu, truy nguyên route và JavaScript, hỗ trợ refactor, viết test, build và smoke-test. Mọi thay đổi được chia thành commit nhỏ để giữ lịch sử phát triển.

## Điều mang lại hiệu quả

- Tách extraction khỏi deterministic policy giúp kết quả giải thích và unit-test được.
- Judge set mở rộng ngày 29/09 chạy đúng upload endpoint với fallback tắt. OpenRouter/Qwen3-VL-8B đạt 15/15 quyết định, 70/75 field và P50/P95 3,614/16,459 giây. Ollama/Qwen3-VL-4B đạt 14/15 quyết định, 73/75 field và 52,238/58,318 giây; model bỏ sót escalation TK-12. Baseline lặp trên 5 fixture ngày 27/09 được giữ như bằng chứng lịch sử, không dùng thay accuracy thực tế.
- Concurrent smoke OpenRouter tiếp nhận và hoàn tất 5/5 request, HTTP accepted P95 93 ms và end-to-end P95 17,072 giây.
- 100 automated tests bao phủ policy/workflow/audit, hợp đồng OpenRouter/Ollama, semantic validation/repair, định danh chứng từ giấy, đối chiếu giảm giá có cấu trúc, fallback/circuit breaker, backoff worker và tính toàn vẹn Test Kit/manifest.
- EF Core SQL Server/Tools và local `dotnet-ef` đã vá lên 8.0.31 trong cùng major. Checkpoint hardening 30/09 đạt 93/93 test; baseline sau postfix live hardening hiện đạt 102/102, NuGet không báo package vulnerable và EF không có pending model change.
- Structured Output giảm parsing lỗi so với JSON tự do.
- Backend phát hiện JSON đúng schema nhưng sai nghĩa như `295.199 đ -> 295.199`, `ECOMMERCE -> RIDE_HAILING`, hoặc số biên nhận bị gán vào `orderId`; model được đọc lại đúng một lần. Repair không nhận claimed amount và kết quả còn mâu thuẫn luôn đi `ESCALATE_FACT`.
- Contract tách `invoiceNumber`, `receiptNumber` và `transactionReference`. Nhãn như `Check`, `Trace`, `RRN` có thể làm mã truy vết cho đúng giao dịch; ShopID, POS/register, MID/TID và pager/table không bao giờ thay thế được mã giao dịch. Khi FACT và POLICY cùng tồn tại, FACT vẫn là status chính nhưng reason hiển thị hạng mục policy thứ cấp.
- Hardening ngày 01/10 ban đầu dùng one-shot repair cho ngày giấy ở `transactionDate`. Postfix live evidence chứng minh quy tắc đó quá chặt, nên ngày mua/giao dịch giấy duy nhất hiện được chuẩn hóa lossless thành ngày chứng từ canonical; `completionDate` vẫn bị cấm thay thế. Distinct labelled dates có thể cùng tồn tại và policy giấy dùng `invoiceDate`.
- Hardening giảm giá ngày 01/10 bổ sung `discountAmount`, khóa Vinamilk `183.114 - 2.828 = 180.286` và vẫn FACT khi khai số trước giảm. Postfix thêm negative examples cho điểm/tích lũy và deterministic normalization chỉ khi `lineItems=subtotal=totalAmount`, tax=0. Suite hiện đạt 102/102; live gate mới nằm trong `LIVE_POSTFIX_INCIDENT_2026-10-01.md`.
- Guard chứng từ giấy ngày 02/10 được kích hoạt từ ca Phê La có vùng món hàng bị che: `lineItems=[]` không còn được phép suy ra không có hạng mục cấm. Backend repair một lần rồi fail-safe FACT, đồng thời ghi missing field/warning và hạ confidence; 102/102 test vẫn pass. Preflight thêm `-OutputPath`, registry inspect trở thành warning khi health database là bằng chứng vận hành, và các runner không còn dùng `$PSScriptRoot` trong default parameter binding của Windows PowerShell 5.1.
- UI bảng nhân viên hợp nhất kết quả lượt hiện tại với escalation lưu DB, tự refresh 5 giây/on-visible và có ARIA live progress notice. Escalation hoàn tất sau restart không còn phụ thuộc sessionStorage hoặc một lượt test mới để xuất hiện.
- Bản production trên SmarterASP.NET đã smoke thành công luồng upload, AI extraction và audit sau khi API key được cập nhật trong Pool Manager.
- UI hiển thị facts AI theo ba cột; bảng chuyển tiếp riêng được hợp nhất vào bảng kết quả để tránh trùng nhưng vẫn phục hồi escalation sau reload.
- Hồ sơ và audit AI được ghi trong cùng một `SaveChanges`; sau thao tác chuyển tiếp/quản lý, giao diện điều hướng toàn trang để dựng lại các bảng từ trạng thái database đã commit, tránh dữ liệu fragment cũ ghi đè nhau.
- Upload dùng hàng đợi DB-backed, trả `202 Accepted`, worker claim bằng lease và UI poll trạng thái. `WorkflowOperationGate` chỉ chống hai batch Verify chạy chồng; `RowVersion` chặn ghi đè cùng hồ sơ.
- Worker dùng exponential backoff có trần khi SQL tạm mất kết nối; log lặp được giảm nhưng lần đầu và mỗi mốc thứ năm vẫn giữ đủ exception để chẩn đoán.
- Fallback OpenRouter sang Ollama mặc định tắt. Khi bật, allowlist lỗi hạ tầng và circuit breaker giới hạn việc chuyển provider; audit ghi provider chính và provider phục vụ.

## Chi phí/thời gian và sự cố thực tế

- Prototype ban đầu dùng Gemini Free Tier; bản hiện tại dùng Qwen3-VL-8B-Instruct trả phí qua OpenRouter để tránh phụ thuộc quota miễn phí. Qwen3-VL-4B-Instruct được giữ làm hướng self-host sau khi benchmark tài nguyên.
- Hệ thống không retry HTTP 429, chỉ retry tối đa một lần với lỗi 5xx tạm thời, trả mã lỗi an toàn (`AI_RATE_LIMIT`/`AI_TEMPORARILY_UNAVAILABLE`) và chuyển `ESCALATE_SYSTEM_ERROR`; không giả quyết định nghiệp vụ.
- LocalDB chỉ dùng khi phát triển Windows. Bản production hiện dùng SQL Server và folder ảnh riêng tư được cấu hình bằng biến môi trường trên SmarterASP.NET; mã nguồn vẫn giữ Linux container, `/healthz` và migration opt-in để có thể chuyển hạ tầng sau này.
- Test Kit v2 đã bổ sung mobile e-commerce, giấy in nhiệt, góc xoay, blur/crop và tiếng Việt có dấu; vẫn là dữ liệu tổng hợp nên không được xem là accuracy tổng quát.
- Runner PowerShell chạy gói 15/30 ca qua đúng upload endpoint, không thêm nút UI, và lưu metadata/CSV/JSON để phép đo có thể audit. Gói 15 ca đã chạy thật cho cả hai provider; gói 30 ca chưa cần chạy trong phần trình bày.
- Readiness `/healthz` kiểm tra policy, cấu hình AI, kết nối database, pending migration và thư mục receipt. Endpoint không gọi provider, nên không tiêu quota và vẫn cần một ảnh smoke test riêng.
- `tools/Test-DemoReadiness.ps1` kiểm đúng database `AuraDb`, LocalDB, policy, storage, migration, provider và fallback trước demo mà không in secret. Chế độ `-DiagnoseLocalDb` ghi user, boot time khi có quyền, versions/instances/details và không sửa registry/MDF; wrapper native-command tránh PowerShell 5.1 dừng script vì stderr.

## Tính năng lớn nhất cắt giảm

- PDF/hóa đơn nhiều trang, antivirus, tax/e-invoice lookup, ngoại tệ và xác thực người dùng theo role chưa triển khai trong Sprint 1.
- Không fine-tune hoặc đổi provider mặc định trong lúc chấm. OpenRouter 8B giữ vai trò primary vì judge set không bỏ sót escalation và đáp ứng thời gian demo. Ollama 4B chỉ dùng offline/manual cho tới khi xử lý TK-12 và kiểm lại tập độc lập.
- Ưu tiên một vertical slice chạy thật: upload -> extract -> decide -> audit -> human override/undo -> tra cứu chứng từ.

## Minh bạch dữ liệu

Ba mươi fixture Test Kit v2 là dữ liệu tổng hợp sinh offline bằng script; không có hóa đơn cá nhân thật. Năm ảnh đại diện được dùng cho Verify. Baseline gửi ảnh qua OpenRouter tới provider Qwen, vì vậy AURA không tuyên bố on-premise hoặc zero-cloud. Phép so sánh cùng build đã có trên judge set 15 ca, nhưng chưa có kết quả chất lượng trên tập hóa đơn thực tế độc lập đã đồng thuận và ẩn danh.
