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
- Build và 70 automated test (policy/workflow/audit + chống thao tác chồng + OpenRouter/Ollama contract + semantic validation/repair + Test Kit integrity) không gọi API AI.
- Sau deploy: 1 request smoke test; nếu pass mới chạy 1 lượt Verify = 5 request. Giữ tối thiểu 10 request dự phòng cho BGK/video.
- Không retry thủ công liên tục khi 429. Ghi lỗi và chờ đúng cửa sổ rate-limit của OpenRouter/provider.
- Benchmark 30 ca chỉ chạy trong một phiên đo riêng khi đã xác nhận quota/billing; không dùng trong luồng demo.

## 7. So sánh OpenRouter 8B và Ollama 4B

- Chạy cùng ảnh, policy, JSON Schema và expected result; ghi rõ provider, model/tag, context và cấu hình lượng tử hóa.
- Báo schema success, exact match của amount/date/currency/identifier, missed-escalation, over-escalation, P50/P95 latency, RAM tối đa và chi phí trên hồ sơ hợp lệ.
- Cổng tối thiểu trước khi cân nhắc đổi mặc định: Verify 5/5 trong ba lượt liên tiếp; 15 ca BGK đi đúng nhánh; không missed escalation trong ca rủi ro khóa; không có lỗi chưa bắt; có rollback cấu hình về OpenRouter.
- Nếu local 4B chậm hơn giới hạn Verify hoặc giảm chất lượng field quan trọng, giữ OpenRouter làm mặc định và ghi Ollama là phương án privacy/cost có giới hạn. Không sửa expected result để làm đẹp benchmark.
- Kết quả kiểm soát ngày 27/09/2026: Ollama 4B đạt 25/25 qua năm lượt Verify liên tiếp sau semantic hardening; upload thủ công `HoaDon1.jpg` đạt `AUTO_APPROVE` 3/3. Ba lượt có đo chi tiết mất khoảng 303-304 giây mỗi batch và ca TC-02 cần một repair khoảng 100 giây; hai lượt xác nhận bổ sung chưa tổng hợp latency. Cổng fixture 5 ca đã đạt, nhưng OpenRouter chưa được chạy lại trên cùng build, còn gói BGK 15 ca và tập độc lập chưa được chạy/ghi nhãn nên chưa đủ điều kiện đổi provider mặc định.
