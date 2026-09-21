# Build Log - Sprint 1

## Công cụ AI và cách sử dụng

- **Gemini 3.6 Flash qua Google AI Studio/API:** đọc một ảnh hóa đơn và trả dữ kiện theo Structured Output. Gemini không được quyền tự quyết định duyệt/chuyển tiếp.
- **Codex:** đọc code/tài liệu, truy nguyên route và JavaScript, hỗ trợ refactor, viết test, build và smoke-test. Mọi thay đổi được chia thành commit nhỏ để giữ lịch sử phát triển.

## Điều mang lại hiệu quả

- Tách extraction khỏi deterministic policy giúp kết quả giải thích và unit-test được.
- Benchmark fixture v1 đạt 5/5 trong khoảng 32 giây tổng vào 20/09/2026. Test Kit v2 gồm 30 ảnh đa layout được sinh offline; 5 ảnh đại diện chưa gọi API để giữ quota cho live benchmark/video.
- 46 automated tests gồm 44 policy/workflow test khóa precedence, e-commerce/mã vận chuyển, tám nhánh quyết định và 2 test toàn vẹn Test Kit/manifest.
- Structured Output giảm parsing lỗi so với JSON tự do.

## Chi phí/thời gian và sự cố thực tế

- Dùng Gemini Free Tier cho prototype; quota/rate limit không được bảo đảm.
- Đã quan sát cả HTTP 503 và HTTP 429 khi gọi Gemini. Hệ thống retry backoff ba lần, trả mã lỗi an toàn (`AI_RATE_LIMIT`/`AI_TEMPORARILY_UNAVAILABLE`) và chuyển `ESCALATE_SYSTEM_ERROR`; không giả quyết định nghiệp vụ.
- LocalDB chỉ phù hợp phát triển Windows. Deploy cần SQL Server/Azure SQL và persistent volume cho ảnh.
- Test Kit v2 đã bổ sung mobile e-commerce, giấy in nhiệt, góc xoay, blur/crop và tiếng Việt có dấu; vẫn là dữ liệu tổng hợp nên không được xem là accuracy tổng quát.

## Tính năng lớn nhất cắt giảm

- PDF/hóa đơn nhiều trang, antivirus, tax/e-invoice lookup, ngoại tệ và xác thực người dùng theo role chưa triển khai trong Sprint 1.
- Không fine-tune hoặc setup model local để tránh rủi ro trễ deadline; dùng Gemini API + policy C# để có sản phẩm deploy được.
- Ưu tiên một vertical slice chạy thật: upload -> extract -> decide -> audit -> human override/undo -> tra cứu chứng từ.

## Minh bạch dữ liệu

Ba mươi fixture Test Kit v2 là dữ liệu tổng hợp sinh offline bằng script; không có hóa đơn cá nhân thật. Năm ảnh đại diện được dùng cho Verify. Ảnh upload được gửi tới Gemini API, vì vậy AURA không tuyên bố on-premise hoặc zero-cloud.
