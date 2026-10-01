# AURA Test Matrix

Mốc cập nhật: 01/10/2026. Ký hiệu: `A` đã tự động hóa bằng xUnit; `V` nằm trong Verify Vision; `R` chạy bằng evaluator ngoài UI; `P` backlog mở rộng, chưa tuyên bố đã đạt.

## Verify một nút

| ID | Fixture tổng hợp | Claimed | Kỳ vọng | Phủ |
|---|---|---:|---|---|
| TC-01 | Mobile e-commerce hoàn tất, có mã SPX và ngày thanh toán | 295.199 | AUTO_APPROVE | V2; Ollama 5/5 ngày 27/09/2026 |
| TC-02 | Phiếu in nhiệt tiếng Việt có dấu, đủ MST/biên nhận | 88.000 | AUTO_APPROVE | V2; Ollama 5/5 sau targeted repair ngày 27/09/2026 |
| TC-03 | Hóa đơn VAT văn phòng phẩm, layout ngang | 420.000 | AUTO_APPROVE | V2; Ollama 5/5 ngày 27/09/2026 |
| TC-04 | Phiếu in nhiệt để trống MST thật sự | 210.000 | ESCALATE_FACT | V2; Ollama 5/5 ngày 27/09/2026 |
| TC-05 | Nhà hàng có Bia Tiger x 6 | 780.000 | ESCALATE_POLICY | V2; Ollama 5/5 ngày 27/09/2026 |

Ngày 20/09/2026, provider Gemini cũ đạt 5/5 trên fixture v1. Fixture v2 đa layout được sinh offline ngày 21/09/2026; người dùng xác nhận đạt đúng 5/5 local bằng Qwen3-VL-8B-Instruct/OpenRouter ngày 22/09/2026. Ngày 27/09/2026, Ollama/Qwen3-VL-4B đạt 25/25 qua năm batch liên tiếp sau semantic hardening; upload thủ công `HoaDon1.jpg` đạt `AUTO_APPROVE` 3/3. Production upload/AI/audit đã smoke thành công và video demo đã được liên kết từ README. Các kết quả fixture tổng hợp không phải accuracy tổng quát.

Năm ca Verify chính thức không thay đổi trong hardening ngày 28/09/2026. Pipeline upload thật đã chuyển sang background processing; cần chạy bổ sung các ca vận hành dưới đây mà không sửa expected result của TC-01..TC-05.

Gói 15/30 ca không có nút riêng trên dashboard. `tools/Invoke-ExtendedDatasetEvaluation.ps1` đọc manifest, gọi đúng upload endpoint cho từng ảnh/claimed amount, poll trạng thái và ghi evidence CSV/JSON. Ngày 29/09/2026, judge set 15 ca đã chạy thật với fallback tắt: OpenRouter đạt 15/15 quyết định, Ollama đạt 14/15 và bỏ sót escalation TK-12. Gói 30 ca chưa chạy vì không cần cho phần trình bày; xem `LIVE_VALIDATION_2026-09-29.md`.

| ID vận hành | Kịch bản | Kỳ vọng |
|---|---|---|
| OP-01 | Refresh sau khi upload nhận `202` | Job vẫn hoàn tất; tab nối lại status từ `sessionStorage` |
| OP-02 | Restart app khi job `PROCESSING` | Sau khi lease hết hạn, worker reclaim; không tạo request mới |
| OP-03 | 5 upload đồng thời | Không trả global `409`; tất cả được ghi `AI_QUEUED` |
| OP-04 | Primary timeout/429, fallback bật | Gọi fallback một lần; audit đúng primary/served provider |
| OP-05 | Schema/semantic invalid, fallback bật | Không đổi provider; fail-safe theo contract/policy |
| OP-06 | Nhiều lỗi hạ tầng đến ngưỡng | Circuit mở; hết cooldown có primary probe |
| OP-07 | Hai manager action cùng hồ sơ | Một thao tác thành công, thao tác stale nhận conflict |

Quan sát UI bắt buộc: sau một lượt phân tích phải có tổng số `AUTO_APPROVE`/`ESCALATE_*`; facts trích xuất hiển thị cạnh upload; auto approve xuất hiện ngay trong Audit; escalation còn trong bảng kết quả cho tới khi chuyển quản lý. Upload/Verify cập nhật trong workspace; mutation workflow điều hướng toàn trang sau commit để tránh trạng thái cũ hoặc trùng lặp.

## Ma trận policy mở rộng

| ID | Tình huống | Kỳ vọng | Phủ |
|---|---|---|---|
| P01 | Receipt hợp lệ dưới 1 triệu | AUTO_APPROVE | A |
| P02 | Extraction null | ESCALATE_FACT | A |
| P03 | Confidence dưới 0,70 | ESCALATE_FACT | A |
| P04 | Không đọc được total | ESCALATE_FACT | A |
| P05 | Claimed amount lệch total | ESCALATE_FACT | A |
| P06 | Thiếu merchant | ESCALATE_FACT | A |
| P07 | Chứng từ giấy thiếu cả invoice number, receipt number và transaction reference | ESCALATE_FACT | A |
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
| P22 | Đồng thời mờ + bia + quá hạn mức | ESCALATE_FACT theo precedence; reason vẫn nêu policy finding thứ cấp | A |
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
| P47 | Model đọc `295.199 đ` thành JSON number `295.199` thay vì `295199` | Phát hiện sai ngữ nghĩa; repair đúng một lần; không tự nhân tiền | A/V |
| P48 | Model gọi đơn hàng giao qua SPX/GHN/GHTK/J&T là `RIDE_HAILING` | Phát hiện mâu thuẫn loại chứng từ và yêu cầu repair | A/V |
| P49 | Model đưa định danh chứng từ giấy vào `orderId` hoặc lặp cùng mã ở nhiều field | Repair/canonicalize về đúng `invoiceNumber`, `receiptNumber` hoặc `transactionReference`; không giữ định danh số giả | A/V |
| P50 | Repair vẫn còn mâu thuẫn ngữ nghĩa | `ESCALATE_FACT`, không AUTO_APPROVE và không gọi repair vô hạn | A |
| P51 | E-commerce không có line items đáng tin cậy | `ESCALATE_FACT` | A |
| P52 | Restaurant/retail receipt có `Receipt No`/`Bill No`, không có MST | `receiptNumber`; AUTO nếu các dữ kiện khác đạt | A |
| P53 | Restaurant bill có `Check: 221196` là mã riêng của lần mua | `transactionReference`; AUTO nếu các dữ kiện khác đạt | A |
| P54 | Receipt chỉ có ShopID + POS/register + pager/table | `ESCALATE_FACT`; các mã vận hành không thay định danh giao dịch | A |
| P55 | Cùng mã bị gán đồng thời vào receiptNumber và transactionReference | Semantic issue, repair một lần; còn lặp thì `ESCALATE_FACT` | A/V |
| P56 | Paper receipt có `invoiceDate=null` nhưng model đặt ngày in trên phiếu vào `transactionDate` | Semantic repair đúng một lần; chỉ tiếp tục policy khi ngày được xác nhận ở `invoiceDate`, nếu không `ESCALATE_FACT` | A/V |
| P57 | Paper receipt lặp cùng ngày ở `invoiceDate` và `transactionDate` | Chuẩn hóa lossless: giữ `invoiceDate`, xóa bản sao `transactionDate`; không gọi repair thừa | A |
| P58 | Paper receipt có `invoiceDate` và `transactionDate` khác nhau | Semantic repair để đọc lại nhãn; còn khác nhau thì `ESCALATE_FACT` | A/V |
| P59 | Receipt in `183.114 - 2.828 = 180.286`, claim `180.286` | `discountAmount=2828`; AUTO nếu các dữ kiện khác đạt | A/V |
| P60 | Cùng receipt nhưng claim `183.114` trước giảm | `ESCALATE_FACT` vì claimed amount không bằng final payable | A |
| P61 | Warning chứa “giảm giá/voucher” nhưng thiếu `discountAmount` và line total lệch | Không bypass arithmetic; semantic repair một lần rồi FACT nếu chưa sửa | A |
| P62 | `discountAmount` âm, vượt số trước giảm hoặc phép tính vẫn sai | Semantic issue; không AUTO_APPROVE | A |
| P63 | `discountAmount` có phần lẻ với VND | Semantic issue; repair hoặc FACT | A |
| P64 | Receipt không có khoản giảm được in rõ | `discountAmount=null`; không tự suy ra từ chênh lệch hoặc claimed amount | A/V |

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
