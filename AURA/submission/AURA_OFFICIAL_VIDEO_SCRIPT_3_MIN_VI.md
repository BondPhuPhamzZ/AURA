# Kịch bản video demo AURA dưới 3 phút

**Bản chốt:** 14/10/2026

## Quyết định quay

Nộp một video chỉ demo sản phẩm, thời lượng mục tiêu 2 phút 45 giây đến 2 phút 55 giây. Không trình bày slide trong video. Giữ một bản bitrate thấp trên laptop hoặc USB nhưng chỉ nộp một video chính thức.

Chỉ dùng ca synthetic hoặc receipt đã được phép công bố. Không hiển thị API key, billing provider, hosting portal, connection string, thư mục evidence riêng tư hoặc dữ liệu cá nhân. Quay live URL và không cắt thời gian chờ để làm latency trông nhanh hơn thực tế.

## Timeline và lời thoại

| Thời gian | Hành động trên màn hình | Lời thoại gợi ý |
|---:|---|---|
| 0:00-0:12 | Hiện live URL và hướng dẫn đầu trang | “AURA là Escalation Referee cho hoàn ứng. Vision model đọc facts, policy tất định quyết định route và con người xử lý ngoại lệ.” |
| 0:12-0:20 | Bấm **Chạy Verify Harness** đúng một lần; trang tự cuộn xuống bảng | “Harness chạy năm ca qua pipeline thật: ba ca routine approval và hai ca escalation.” |
| 0:20-1:18 | Giữ progress và timestamp trong lúc Verify chạy | “AURA phân biệt bất định dữ kiện, ngoại lệ policy và giới hạn thẩm quyền. Provider failure không bao giờ được đổi thành business approval.” |
| 1:18-1:32 | Giữ bảng 5/5 và elapsed time | “Candidate đóng băng hoàn tất 5 trên 5 trong khoảng 65 giây, dưới gate 90 giây.” |
| 1:32-1:48 | Mở hồ sơ synthetic `ESCALATE_*`, chỉ facts và reason | “Model chỉ cung cấp facts. Policy C# dừng hồ sơ và đặt câu hỏi cụ thể cho reviewer.” |
| 1:48-2:08 | Bấm **Chuyển tiếp**, sang Quản lý và chọn YES/NO có ý nghĩa | “Nhân viên xác nhận handoff. Quản lý vẫn chịu trách nhiệm cho quyết định ngoại lệ.” |
| 2:08-2:28 | Mở Audit, hiện timeline rồi Undo quyết định gần nhất | “Audit ghi lại điều gì xảy ra, lúc nào và vì sao. Undo thêm event mới, không xóa lịch sử.” |
| 2:28-2:42 | Trở lại hồ sơ, chỉ provider, status, facts và câu hỏi | “SQL giữ background job bền vững qua refresh hoặc restart. Model có thể đổi mà approval policy không đổi.” |
| 2:42-2:54 | Giữ AURA trên màn hình và nêu giới hạn | “Đây là supervised MVP: một JPG/PNG, chưa có production RBAC hoặc multipage PDF, và live fallback vẫn tắt.” |
| 2:54-2:58 | Giữ tiêu đề AURA và live URL | “AURA tự động hóa phần rõ ràng và dừng ở nơi cần quyết định con người.” |

## Checklist trước khi quay

1. Rehearsal toàn bộ chuỗi một lần trên tag đóng băng, không khởi tạo Verify thứ hai.
2. Kiểm `/healthz`: status ok, Qwen3.8 Flash, reasoning none, DB/storage/migration ready, fallback false.
3. Đóng OpenRouter, hosting, terminal, File Explorer chứa receipt thật và mọi notification.
4. Chỉ một người thao tác; không upload receipt khác trong lúc Verify.
5. Quay 1920x1080, zoom 100%, con trỏ lớn và âm thanh rõ.
6. Xác nhận video không quá 180 giây và kết quả thực sự đã terminal.
7. Xem file xuất một lần có âm thanh và một lần tắt tiếng để rà secret/PII.

## Phương án khi lỗi

Nếu live request không bắt đầu sau khoảng 10 giây, dừng take và kiểm tra riêng; không click liên tục. Khi thuyết trình, luôn giữ video này làm đường dự phòng nếu live URL hoặc provider gặp sự cố.
