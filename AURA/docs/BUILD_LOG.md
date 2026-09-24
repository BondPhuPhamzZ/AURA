# Build Log - Sprint 1

## Công cụ AI và cách sử dụng

- **Qwen3-VL-8B-Instruct qua OpenRouter:** đọc một ảnh hóa đơn và trả dữ kiện theo JSON Schema chặt. Qwen không được quyền tự quyết định duyệt/chuyển tiếp.
- **Codex:** đọc code/tài liệu, truy nguyên route và JavaScript, hỗ trợ refactor, viết test, build và smoke-test. Mọi thay đổi được chia thành commit nhỏ để giữ lịch sử phát triển.

## Điều mang lại hiệu quả

- Tách extraction khỏi deterministic policy giúp kết quả giải thích và unit-test được.
- Người dùng xác nhận Verify fixture v2 đạt đúng 5/5 local ngày 22/09/2026. Test Kit v2 gồm 30 ảnh đa layout được sinh offline; kết quả này là fixture demo, không phải accuracy trên dữ liệu độc lập.
- 56 automated tests gồm 49 policy/workflow/audit và chống thao tác chồng, khóa precedence, e-commerce/mã vận chuyển, phạm vi MST, các nhánh quyết định và phép chiếu timeline; 4 test hợp đồng OpenRouter/Qwen offline; 3 test toàn vẹn Test Kit/manifest, trong đó khóa đúng gói BGK 15 ca.
- Structured Output giảm parsing lỗi so với JSON tự do.
- Bản production trên SmarterASP.NET đã smoke thành công luồng upload, AI extraction và audit sau khi API key được cập nhật trong Pool Manager.
- UI hiển thị facts AI theo ba cột; bảng chuyển tiếp riêng được hợp nhất vào bảng kết quả để tránh trùng nhưng vẫn phục hồi escalation sau reload.
- Hồ sơ và audit AI được ghi trong cùng một `SaveChanges`; sau thao tác chuyển tiếp/quản lý, giao diện điều hướng toàn trang để dựng lại các bảng từ trạng thái database đã commit, tránh dữ liệu fragment cũ ghi đè nhau.
- `WorkflowOperationGate` chặn thao tác ghi chồng trong một instance và SQL Server `RowVersion` phát hiện cập nhật đồng thời giữa nhiều instance.

## Chi phí/thời gian và sự cố thực tế

- Prototype ban đầu dùng Gemini Free Tier; bản hiện tại dùng Qwen3-VL-8B-Instruct trả phí qua OpenRouter để tránh phụ thuộc quota miễn phí. Qwen3-VL-4B-Instruct được giữ làm hướng self-host sau khi benchmark tài nguyên.
- Hệ thống không retry HTTP 429, chỉ retry tối đa một lần với lỗi 5xx tạm thời, trả mã lỗi an toàn (`AI_RATE_LIMIT`/`AI_TEMPORARILY_UNAVAILABLE`) và chuyển `ESCALATE_SYSTEM_ERROR`; không giả quyết định nghiệp vụ.
- LocalDB chỉ dùng khi phát triển Windows. Bản production hiện dùng SQL Server và folder ảnh riêng tư được cấu hình bằng biến môi trường trên SmarterASP.NET; mã nguồn vẫn giữ Linux container, `/healthz` và migration opt-in để có thể chuyển hạ tầng sau này.
- Test Kit v2 đã bổ sung mobile e-commerce, giấy in nhiệt, góc xoay, blur/crop và tiếng Việt có dấu; vẫn là dữ liệu tổng hợp nên không được xem là accuracy tổng quát.

## Tính năng lớn nhất cắt giảm

- PDF/hóa đơn nhiều trang, antivirus, tax/e-invoice lookup, ngoại tệ và xác thực người dùng theo role chưa triển khai trong Sprint 1.
- Không fine-tune hoặc setup model local để tránh rủi ro trễ deadline; dùng Qwen qua OpenRouter + policy C# để có sản phẩm deploy được.
- Ưu tiên một vertical slice chạy thật: upload -> extract -> decide -> audit -> human override/undo -> tra cứu chứng từ.

## Minh bạch dữ liệu

Ba mươi fixture Test Kit v2 là dữ liệu tổng hợp sinh offline bằng script; không có hóa đơn cá nhân thật. Năm ảnh đại diện được dùng cho Verify. Ảnh upload được gửi qua OpenRouter tới provider Qwen được router lựa chọn, vì vậy AURA không tuyên bố on-premise hoặc zero-cloud.
