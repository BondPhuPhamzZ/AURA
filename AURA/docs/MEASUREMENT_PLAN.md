# Measurement Plan (Chuẩn bị cho Sprint 2)

## 1. Missed-escalation rate
Tỷ lệ hóa đơn đáng lẽ phải bị chặn (Escalate) nhưng hệ thống lại Auto-Approve.
- **Cách đo**: Auditor rà soát ngẫu nhiên 5% lượng Auto-Approve mỗi tuần.

## 2. Over-escalation rate
Tỷ lệ hóa đơn hoàn toàn hợp lệ nhưng bị hệ thống ném vào hàng đợi Escalate, làm mất thời gian của sếp.
- **Cách đo**: Thống kê số lượng ca Escalate được sếp "Approve" thẳng tay mà không cần chỉnh sửa.

## 3. Extraction Accuracy
- **Cách đo**: Đối chiếu thủ công JSON do Vision Model xuất ra so với ảnh gốc trên 100 mẫu ngẫu nhiên.

## 4. Thời gian xử lý
- **Cách đo**: So sánh Latency trung bình của AI (thường < 5s) so với thời gian duyệt tay của kế toán (thường > 5 phút).
