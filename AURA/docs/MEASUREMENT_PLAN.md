# Measurement Plan (Chuẩn bị cho Sprint 2)

## 1. Missed-escalation rate
Tỷ lệ hóa đơn đáng lẽ phải bị chặn (Escalate) nhưng hệ thống lại Auto-Approve.
- **Cách đo**: Với tập độc lập có nhãn bởi hai người kiểm tra, tính `missed escalations / tổng ca bắt buộc escalate`; báo cáo khoảng tin cậy và bất đồng nhãn.

## 2. Over-escalation rate
Tỷ lệ hóa đơn hoàn toàn hợp lệ nhưng bị hệ thống ném vào hàng đợi Escalate, làm mất thời gian của sếp.
- **Cách đo**: `routine cases bị escalate / tổng routine cases` trên tập độc lập. Manager approve không tự động chứng minh AI đã over-escalate.

## 3. Extraction Accuracy
- **Cách đo**: Đối chiếu từng field với ground truth trên tối thiểu 100 ảnh đa dạng; báo precision/recall cho identifier và prohibited item, exact-match cho amount/date/currency.

## 4. Thời gian xử lý
- **Cách đo**: Ghi p50/p95 end-to-end từ upload đến decision và thời gian human review. Đo baseline thủ công với cùng người/cùng loại hồ sơ; không dùng ước tính cảm tính.

## 5. Tác động tiêu cực và gánh nặng mới

- Số phút quản lý dành cho queue và số lần phải mở lại chứng từ gốc.
- Tỷ lệ người dùng chấp nhận output mà không kiểm tra (automation bias).
- Chi phí thời gian sửa dữ liệu OCR sai và xử lý lỗi quota/API.
- Khảo sát 3 người dùng thực tế trước/sau, ghi chức danh và phản hồi nguyên văn khi có đồng thuận.

## 6. Ngân sách benchmark và quota

- Test Kit v2 có 30 ca nhưng Verify demo chỉ gọi 5 ca đại diện.
- Build và 46 automated test (policy/workflow + Test Kit integrity) không gọi Gemini.
- Sau deploy: 1 request smoke test; nếu pass mới chạy 1 lượt Verify = 5 request. Giữ tối thiểu 10 request dự phòng cho BGK/video.
- Không retry thủ công liên tục khi 429. Ghi lỗi và chờ đúng cửa sổ reset trong AI Studio.
- Benchmark 30 ca chỉ chạy trong một phiên đo riêng khi đã xác nhận quota/billing; không dùng trong luồng demo.
