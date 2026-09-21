# AURA Test Matrix

Mốc cập nhật: 22/09/2026. Ký hiệu: `A` đã tự động hóa bằng xUnit; `V` nằm trong Verify Vision; `P` backlog mở rộng, chưa tuyên bố đã đạt.

## Verify một nút

| ID | Fixture tổng hợp | Claimed | Kỳ vọng | Phủ |
|---|---|---:|---|---|
| TC-01 | Mobile e-commerce hoàn tất, có mã SPX và ngày thanh toán | 295.199 | AUTO_APPROVE | V2, local PASS 22/09/2026 |
| TC-02 | Phiếu in nhiệt tiếng Việt có dấu, đủ MST/biên nhận | 88.000 | AUTO_APPROVE | V2, local PASS 22/09/2026 |
| TC-03 | Hóa đơn VAT văn phòng phẩm, layout ngang | 420.000 | AUTO_APPROVE | V2, local PASS 22/09/2026 |
| TC-04 | Phiếu in nhiệt để trống MST thật sự | 210.000 | ESCALATE_FACT | V2, local PASS 22/09/2026 |
| TC-05 | Nhà hàng có Bia Tiger x 6 | 780.000 | ESCALATE_POLICY | V2, local PASS 22/09/2026 |

Ngày 20/09/2026, provider Gemini cũ đạt 5/5 trên fixture v1. Fixture v2 đa layout được sinh offline ngày 21/09/2026; người dùng xác nhận đạt đúng 5/5 local bằng Qwen3-VL-8B-Instruct/OpenRouter ngày 22/09/2026. Phải chạy đúng một lượt smoke sau deploy trước khi quay video; 5/5 trên fixture tổng hợp không phải accuracy tổng quát.

Quan sát UI bắt buộc: sau một lượt phân tích phải có tổng số `AUTO_APPROVE`/`ESCALATE_*`; facts trích xuất hiển thị cạnh upload; auto approve xuất hiện ngay trong Audit; escalation còn trong bảng kết quả cho tới khi chuyển quản lý; các thao tác không yêu cầu reload toàn trang.

## Ma trận policy mở rộng

| ID | Tình huống | Kỳ vọng | Phủ |
|---|---|---|---|
| P01 | Receipt hợp lệ dưới 1 triệu | AUTO_APPROVE | A |
| P02 | Extraction null | ESCALATE_FACT | A |
| P03 | Confidence dưới 0,70 | ESCALATE_FACT | A |
| P04 | Không đọc được total | ESCALATE_FACT | A |
| P05 | Claimed amount lệch total | ESCALATE_FACT | A |
| P06 | Thiếu merchant | ESCALATE_FACT | A |
| P07 | Thiếu invoice number | ESCALATE_FACT | A |
| P08 | Hóa đơn GTGT thiếu seller tax ID | ESCALATE_FACT | A |
| P09 | Ngoại tệ chưa có tỷ giá | ESCALATE_FACT | A |
| P10 | Ngày không đúng ISO/không chắc chắn | ESCALATE_FACT | A |
| P11 | Ngày tương lai | ESCALATE_FACT | A |
| P12 | Quá thời hạn 90 ngày | ESCALATE_FACT | A |
| P13 | Cuối tuần | ESCALATE_FACT | A |
| P14 | Ngoài 06:00-22:00 | ESCALATE_FACT | A |
| P15 | Warning blur/crop critical | ESCALATE_FACT | A |
| P16 | SHA-256 trùng hồ sơ trước | ESCALATE_FACT | A |
| P16b | Vision báo `impossible arithmetic` do số lượng × đơn giá sai | ESCALATE_FACT | A |
| P17 | Beer/alcohol | ESCALATE_POLICY | A |
| P18 | Tobacco | ESCALATE_POLICY | A |
| P19 | Karaoke/entertainment | ESCALATE_POLICY | A |
| P20 | Personal item | ESCALATE_POLICY | A |
| P21 | Tổng 1.000.001 VND | ESCALATE_AUTHORITY | A |
| P22 | Đồng thời mờ + bia + quá hạn mức | ESCALATE_FACT theo precedence | A |
| P23 | JPG extension nhưng magic bytes sai | HTTP 400, không gọi AI | smoke |
| P24 | POST Verify không CSRF | HTTP 400 | smoke |
| P25 | BUSINESS_RULES qua static URL | HTTP 404 | smoke |
| P26 | OpenRouter/Qwen 429/503 | không retry 429; retry tối đa một lần với 5xx rồi SYSTEM_ERROR | code |
| P27 | Hai hóa đơn trong một ảnh | FACT hoặc tách document theo policy tương lai | P |
| P28 | PDF/nhiều trang | Từ chối có giải thích ở Sprint 1 | P |
| P29 | Prompt injection in trên hóa đơn | Bỏ qua chỉ dẫn, ghi suspicious signal, FACT | P |
| P30 | Tổng dòng hàng không khớp grand total | FACT | P |
| P31 | Sửa amount bằng font/overlay bất thường | FACT, không cáo buộc gian lận | P |
| P32 | Ride-hailing thiếu Trip/Booking ID và receipt number | ESCALATE_FACT; MST không thay định danh chuyến/biên nhận | A |
| P33 | Receipt không có giờ | Được phép nếu các dữ kiện khác đủ; giờ chỉ kiểm khi nhìn thấy | policy |
| P34 | Mốc 06:00 và 22:00 | Inclusive; AUTO nếu điều kiện khác đạt | P |
| P35 | Tổng đúng 1.000.000 VND | AUTO nếu điều kiện khác đạt | P |
| P36 | Claim bằng 0/âm | HTTP 400, không gọi AI | P |
| P37 | Ảnh màn hình hóa đơn còn “chưa cấp số/lưu và phát hành” | ESCALATE_FACT vì DRAFT | A |
| P38 | Chứng từ CANCELLED/đã hủy | ESCALATE_FACT | A |
| P39 | POS/retail receipt có số biên nhận và MID/TID nhưng không có seller tax ID | MID/TID được trích xuất riêng; không thay số biên nhận; không bắt buộc MST nếu không phải VAT_INVOICE | policy/live |
| P40 | Qwen trả `finish_reason=length` hoặc JSON lỗi | SYSTEM_ERROR, không dùng dữ kiện bị cắt | code |
| P41 | Ảnh vượt 5 MB hoặc server trả body rỗng/non-JSON | Chặn trước upload hoặc hiển thị lỗi HTTP dễ hiểu; không ném lỗi parse JSON | client/server |
| P42 | E-commerce đủ dữ kiện, có order ID nhưng không có MST/invoice number | AUTO_APPROVE nếu hoàn tất, ngày giao dịch/tổng tiền/item hợp lệ | A |
| P43 | E-commerce đủ dữ kiện, chỉ có shipping tracking code làm định danh truy vết | AUTO_APPROVE; tracking không được diễn giải là MST/hóa đơn thuế | A |
| P44 | E-commerce không có order/booking/tracking/receipt ID | ESCALATE_FACT | A |
| P45 | Chỉ có ngày giao hàng nhưng thiếu ngày giao dịch/thanh toán | ESCALATE_FACT | A |
| P46 | Đơn đã hoàn thành nhưng đồng thời REFUNDED/RETURNED | ESCALATE_FACT bất kể có tracking code | A |

## Ma trận human-in-the-loop

- Nhân viên chỉ chuyển tiếp được hồ sơ đang ở `ESCALATE_*`; thao tác tạo audit `EMPLOYEE_FORWARDED_TO_MANAGER`.
- Cửa sổ quản lý chỉ thấy hồ sơ đã được nhân viên chuyển tiếp.
- Nhân viên không trả lời câu hỏi quyết định; họ chỉ **Chuyển tiếp** hoặc **Chuyển tiếp tất cả**.
- `Đồng ý duyệt/Từ chối duyệt` của quản lý được ánh xạ theo loại câu hỏi: FACT/SYSTEM_ERROR tiếp nhận thủ công hoặc trả lại; POLICY duyệt/từ chối ngoại lệ; AUTHORITY chuyển cấp hoặc trả lại.
- Mỗi quyết định quản lý ghi câu hỏi, câu trả lời và outcome; hoàn tác đưa hồ sơ về đúng trạng thái escalation trước đó.
- Tám nhánh quyết định của bốn loại escalation và nhãn phân quyền đã được khóa bằng unit test.

## Tiêu chí comparator

- PASS chỉ khi expected và actual trùng; `ESCALATE` tổng quát chỉ khớp một status bắt đầu bằng `ESCALATE_`.
- `ESCALATE_SYSTEM_ERROR` không bao giờ được coi là PASS cho FACT/POLICY/AUTHORITY.
- Ca chuyển tiếp phải có câu hỏi tiếng Việt, nêu dữ kiện cụ thể và trả lời trực tiếp CÓ/KHÔNG.
- Mọi kết quả Verify phải có timestamp và latency thật.
- Khi thay policy/model/fixture phải commit manifest và chạy lại benchmark; không sửa expected để hợp thức hóa output sai.
