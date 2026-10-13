# Sổ tay AURA hiện hành

**Cập nhật:** 14/10/2026

**Release đóng băng:** `aura-demo-day-2026-10-14`

**Commit:** `efc65d835a3b271369ed07c0a129533d1a0b5264`

## 1 AURA giải quyết vấn đề gì

AURA là trợ lý thẩm định tuyến đầu cho hoàn ứng. Hệ thống nhận ảnh hóa đơn và số tiền nhân viên khai, dùng vision model để chuyển nội dung ảnh thành dữ kiện có cấu trúc, kiểm tra tính nhất quán rồi dùng policy C# tất định để chọn một trong bốn hướng: tự động đi tiếp, hỏi lại dữ kiện, xin ngoại lệ policy hoặc chuyển cấp có thẩm quyền cao hơn. Provider hay contract lỗi luôn dừng an toàn, không tạo quyết định nghiệp vụ giả.

## 2 Nguyên tắc thiết kế

- AI xử lý perception; code xử lý rule; con người giữ authority.
- FACT đứng trước POLICY và AUTHORITY vì không thể kết luận policy trên dữ kiện chưa đáng tin.
- Mọi số tiền muốn AUTO phải có evidence của dòng tổng cuối được in rõ; không suy từ subtotal hoặc số người dùng khai.
- Repair tối đa một lần. Nếu vẫn mâu thuẫn thì chuyển người.
- Audit và Undo bổ sung sự kiện mới; không xóa lịch sử cũ.

## 3 Kiến trúc đang chạy

```text
JPG/PNG + claimed amount
-> kiểm extension, MIME, magic bytes, size
-> lưu request PENDING và AI job trong SQL
-> trả HTTP 202 + status URL
-> worker claim bằng lease
-> VisionInvocationCoordinator lấy một AI permit
-> OpenRouter gọi Qwen3.8 Flash route Alibaba, reasoning none
-> JSON Schema + semantic validation + tối đa một repair
-> PolicyDecisionEngine C# quyết định route
-> nhân viên xác nhận chuyển tiếp nếu ESCALATE
-> quản lý YES/NO
-> Audit và Undo
```

SQL là nguồn sự thật cho job. Bounded channel chỉ giảm độ trễ đánh thức worker. Refresh hoặc recycle có thể làm mất signal trong RAM nhưng không làm mất row PENDING; lease hết hạn cho phép reclaim.

## 4 Concurrency và nhiều giám khảo

Năm người upload năm hóa đơn khác nhau vẫn được nhận HTTP 202 và xếp hàng trong SQL. AI concurrency một giúp tránh khuếch đại request lên provider nhưng làm request sau chờ lâu hơn. Test năm upload hoàn tất 5/5 với E2E P95 93,684 giây. Đây là bằng chứng không mất job trên một process, chưa chứng minh năng lực nhiều instance.

Verify là benchmark độc quyền. Khi nhiều người bấm cùng lúc, `WorkflowOperationGate` cho một batch chạy và trả 409 cho click trùng. Vì vậy không nhân năm ca thành 25 API call.

## 5 Provider và fallback

Primary live là OpenRouter/Qwen3.8 Flash qua Alibaba với `reasoning=none`. Cấu hình này tránh trường hợp reasoning tiêu hết completion budget nhưng không trả JSON usable. Shared permit, cooldown và retry có giới hạn giúp giảm burst; 429 vẫn có thể xảy ra vì quota/capacity của upstream.

Ollama/Qwen3-VL-4B 16K đạt gate chất lượng synthetic nhưng latency cao. SmarterASP không thể gọi loopback trên laptop. Vì vậy Ollama chỉ là contingency local có giám sát và live fallback mặc định tắt.

## 6 Bằng chứng hiện hành

| Phép đo | Kết quả | Cách diễn giải đúng |
|---|---|---|
| Release build | 0 warning, 0 error | Gate source hiện hành |
| Offline tests | 163/163 | Policy, contract, queue, fallback, audit và UI; không phải receipt accuracy |
| Live Verify | 5/5 trong 65,004 giây | Đạt gate năm ca dưới 90 giây |
| Prospective holdout 10 | 8/10, 0 missed, 2 over | Mẫu nhỏ đã khóa; phù hợp supervised demo, chưa đủ production |
| Regression source bytes 12 | 10/12 | Dữ liệu đã biết, kiểm stability và queue |
| Sequential repeat | 11/12, P95 32,903 giây | Lặp stability, không phải holdout mới |
| Năm upload đồng thời | 5/5, P95 93,684 giây | Queue integrity, không phải Verify SLA |

Hai over còn lại phản ánh fail-safe. FAHASA bị giữ FACT vì validator chưa chấp nhận chắc chắn nhãn `Thanh toán 91.500 đ` là final-payable; GS25_2 từng biến thiên ở merchant gần vùng blackout. Không vá keyword sát ngày thi vì có thể nhầm dòng tender, tiền khách đưa hoặc tiền thối thành tổng cuối.

## 7 Evidence localization

Prototype hiện chỉ minh họa overlay SVG gắn với hash ảnh và region JSON. Nó chưa có OCR word boxes đáng tin, annotation độc lập, benchmark region recall hoặc false-highlight. Nếu highlight sai số tiền, reviewer có thể tự tin sai. Vì vậy tính năng chưa nối vào production pipeline và không làm thay đổi quyết định.

## 8 Trạng thái sẵn sàng

Đủ cơ sở quay video và demo có giám sát trên release đóng băng. Các gate con người còn mở là: quay video chính thức tối đa 180 giây, thu ba phiên user study thật, kiểm quyền Team Drive bằng cửa sổ ẩn danh và rehearsal có bấm giờ. Không thay logic trước khi các việc này hoàn tất.
