# Build Log - Sprint 1

## Công cụ AI và cách sử dụng

- **Qwen3-VL-8B-Instruct qua OpenRouter:** đọc một ảnh hóa đơn và trả dữ kiện theo JSON Schema chặt. Qwen không được quyền tự quyết định duyệt/chuyển tiếp.
- **Codex:** đọc code/tài liệu, truy nguyên route và JavaScript, hỗ trợ refactor, viết test, build và smoke-test. Mọi thay đổi được chia thành commit nhỏ để giữ lịch sử phát triển.

## Điều mang lại hiệu quả

- Tách extraction khỏi deterministic policy giúp kết quả giải thích và unit-test được.
- Benchmark fixture v1 đạt 5/5 trong khoảng 32 giây tổng vào 20/09/2026. Test Kit v2 gồm 30 ảnh đa layout được sinh offline; 5 ảnh đại diện chưa gọi API để giữ quota cho live benchmark/video.
- 52 automated tests gồm 47 policy/workflow test khóa precedence, e-commerce/mã vận chuyển, phạm vi MST theo loại chứng từ và các nhánh quyết định; 2 test hợp đồng OpenRouter/Qwen offline; 3 test toàn vẹn Test Kit/manifest, trong đó khóa đúng gói BGK 15 ca.
- Structured Output giảm parsing lỗi so với JSON tự do.
- UI hiển thị facts AI theo ba cột; bảng chuyển tiếp riêng được hợp nhất vào bảng kết quả để tránh trùng nhưng vẫn phục hồi escalation sau reload.
- Hồ sơ và audit AI được ghi trong cùng một `SaveChanges`; các fragment kết quả/quản lý/lịch sử cập nhật sau thao tác mà không reload toàn trang.

## Chi phí/thời gian và sự cố thực tế

- Prototype ban đầu dùng Gemini Free Tier; bản hiện tại dùng Qwen3-VL-8B-Instruct trả phí qua OpenRouter để tránh phụ thuộc quota miễn phí. Qwen3-VL-4B-Instruct được giữ làm hướng self-host sau khi benchmark tài nguyên.
- Hệ thống không retry HTTP 429, chỉ retry tối đa một lần với lỗi 5xx tạm thời, trả mã lỗi an toàn (`AI_RATE_LIMIT`/`AI_TEMPORARILY_UNAVAILABLE`) và chuyển `ESCALATE_SYSTEM_ERROR`; không giả quyết định nghiệp vụ.
- LocalDB chỉ phù hợp phát triển Windows. Mã nguồn đã có Linux container, `/healthz`, migration opt-in và hỗ trợ absolute persistent-volume path; deploy thật vẫn cần SQL Server/Azure SQL cùng tài nguyên cloud do nhóm sở hữu.
- Test Kit v2 đã bổ sung mobile e-commerce, giấy in nhiệt, góc xoay, blur/crop và tiếng Việt có dấu; vẫn là dữ liệu tổng hợp nên không được xem là accuracy tổng quát.

## Tính năng lớn nhất cắt giảm

- PDF/hóa đơn nhiều trang, antivirus, tax/e-invoice lookup, ngoại tệ và xác thực người dùng theo role chưa triển khai trong Sprint 1.
- Không fine-tune hoặc setup model local để tránh rủi ro trễ deadline; dùng Qwen qua OpenRouter + policy C# để có sản phẩm deploy được.
- Ưu tiên một vertical slice chạy thật: upload -> extract -> decide -> audit -> human override/undo -> tra cứu chứng từ.

## Minh bạch dữ liệu

Ba mươi fixture Test Kit v2 là dữ liệu tổng hợp sinh offline bằng script; không có hóa đơn cá nhân thật. Năm ảnh đại diện được dùng cho Verify. Ảnh upload được gửi qua OpenRouter tới provider Qwen được router lựa chọn, vì vậy AURA không tuyên bố on-premise hoặc zero-cloud.
