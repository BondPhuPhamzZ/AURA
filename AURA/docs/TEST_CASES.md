# AURA Test Matrix

Mốc cập nhật: 21/09/2026. Ký hiệu: `A` đã tự động hóa bằng xUnit; `V` nằm trong Verify Vision; `P` backlog mở rộng, chưa tuyên bố đã đạt.

## Verify một nút

| ID | Fixture tổng hợp | Claimed | Kỳ vọng | Phủ |
|---|---|---:|---|---|
| TC-01 | Grab e-receipt, đủ Trip ID/MST, weekday | 150.000 | AUTO_APPROVE | V |
| TC-02 | Phở + nước, đủ MST, weekday | 80.000 | AUTO_APPROVE | V |
| TC-03 | Văn phòng phẩm, đủ MST, weekday | 350.000 | AUTO_APPROVE | V |
| TC-04 | Phiếu viết tay ghi không có MST | 200.000 | ESCALATE_FACT | V |
| TC-05 | Nhà hàng có Tiger Beer | 850.000 | ESCALATE_POLICY | V |

Ngày 20/09/2026, Gemini 3.6 Flash đạt 5/5; latency từng ca xấp xỉ 8,7s, 9,1s, 3,9s, 5,7s và 4,1s. Đây là benchmark fixture nội bộ, không phải accuracy độc lập.

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
| P08 | Hóa đơn giấy thiếu seller tax ID | ESCALATE_FACT | A |
| P09 | Ngoại tệ chưa có tỷ giá | ESCALATE_FACT | A |
| P10 | Ngày không đúng ISO/không chắc chắn | ESCALATE_FACT | A |
| P11 | Ngày tương lai | ESCALATE_FACT | A |
| P12 | Quá thời hạn 90 ngày | ESCALATE_FACT | A |
| P13 | Cuối tuần | ESCALATE_FACT | A |
| P14 | Ngoài 06:00-22:00 | ESCALATE_FACT | A |
| P15 | Warning blur/crop critical | ESCALATE_FACT | A |
| P16 | SHA-256 trùng hồ sơ trước | ESCALATE_FACT | A |
| P17 | Beer/alcohol | ESCALATE_POLICY | A |
| P18 | Tobacco | ESCALATE_POLICY | A |
| P19 | Karaoke/entertainment | ESCALATE_POLICY | A |
| P20 | Personal item | ESCALATE_POLICY | A |
| P21 | Tổng 1.000.001 VND | ESCALATE_AUTHORITY | A |
| P22 | Đồng thời mờ + bia + quá hạn mức | ESCALATE_FACT theo precedence | A |
| P23 | JPG extension nhưng magic bytes sai | HTTP 400, không gọi AI | smoke |
| P24 | POST Verify không CSRF | HTTP 400 | smoke |
| P25 | BUSINESS_RULES qua static URL | HTTP 404 | smoke |
| P26 | API Gemini 429/503 | retry tối đa 3, sau đó SYSTEM_ERROR | code/live observed |
| P27 | Hai hóa đơn trong một ảnh | FACT hoặc tách document theo policy tương lai | P |
| P28 | PDF/nhiều trang | Từ chối có giải thích ở Sprint 1 | P |
| P29 | Prompt injection in trên hóa đơn | Bỏ qua chỉ dẫn, ghi suspicious signal, FACT | P |
| P30 | Tổng dòng hàng không khớp grand total | FACT | P |
| P31 | Sửa amount bằng font/overlay bất thường | FACT, không cáo buộc gian lận | P |
| P32 | Ride-hailing thiếu Trip/Booking ID nhưng có MST | Policy hiện cho phép định danh bằng MST; cần benchmark | P |
| P33 | Receipt không có giờ | Được phép nếu các dữ kiện khác đủ; giờ chỉ kiểm khi nhìn thấy | policy |
| P34 | Mốc 06:00 và 22:00 | Inclusive; AUTO nếu điều kiện khác đạt | P |
| P35 | Tổng đúng 1.000.000 VND | AUTO nếu điều kiện khác đạt | P |
| P36 | Claim bằng 0/âm | HTTP 400, không gọi AI | P |
| P37 | Ảnh màn hình hóa đơn còn “chưa cấp số/lưu và phát hành” | ESCALATE_FACT vì DRAFT | A |
| P38 | Chứng từ CANCELLED/đã hủy | ESCALATE_FACT | A |
| P39 | POS slip có MID/TID nhưng không có seller tax ID | MID/TID được trích xuất riêng; vẫn ESCALATE_FACT | policy/live |
| P40 | Gemini trả `MAX_TOKENS` hoặc JSON lỗi | SYSTEM_ERROR, không dùng dữ kiện bị cắt | code |

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
