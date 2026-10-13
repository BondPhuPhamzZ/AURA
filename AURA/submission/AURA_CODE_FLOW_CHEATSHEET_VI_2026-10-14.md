# Bảng tra luồng xử lý và mã nguồn AURA

**Cập nhật:** 14/10/2026

## Luồng request

| Bước | Trách nhiệm | Điểm cần nói với BGK |
|---|---|---|
| Upload | Controller kiểm file, lưu receipt/request/job | Không giữ request chờ AI; trả 202 |
| Durable queue | SQL row PENDING, lease và reclaim | Bounded channel không phải queue chính |
| AI invocation | Coordinator giữ một permit, adapter gọi provider | Gate hiện là per-process |
| Extraction | Model trả JSON theo schema | Claimed amount không đi vào prompt |
| Semantic validation | Kiểm identifier, money, date, evidence; tối đa một repair | Schema-valid vẫn có thể sai nghĩa |
| Policy | C# tạo AUTO hoặc ESCALATE | Model không có quyền approve |
| Human workflow | Forward, manager YES/NO | Người chịu trách nhiệm ở ngoại lệ |
| Audit và Undo | Append event, hiển thị timeline | Undo không xóa lịch sử |

## Ba gate dễ bị nhầm

1. SQL job queue bảo đảm độ bền.
2. `VisionInvocationCoordinator` giới hạn số AI call đang chạy trong một process.
3. `WorkflowOperationGate` chống hai batch Verify chạy trùng.

Không gate nào ở trên là distributed limiter giữa nhiều IIS worker hoặc nhiều server. Production cần Redis/distributed semaphore, circuit state chia sẻ, metrics và autoscaling policy.

## Thứ tự quyết định

```text
SYSTEM_ERROR nếu hạ tầng/contract không tin cậy
FACT nếu dữ kiện thiếu hoặc mâu thuẫn
POLICY nếu có hạng mục cấm/ngoại lệ
AUTHORITY nếu tổng hợp lệ vượt 1.000.000 VND
AUTO_APPROVE nếu mọi gate đều đạt
```

## Vì sao claimed amount không vào prompt

Nếu model thấy số nhân viên khai trước, nó có thể bị neo và tìm cách hợp thức hóa số đó. AURA buộc model đọc tổng in trên hóa đơn độc lập, sau đó backend mới so sánh với claim. Đây là một lớp kiểm soát tách biệt, không phải prompt trick.

## Khi provider trả 429 hoặc timeout

- Không retry song song.
- Giữ shared permit và cooldown để tránh amplification.
- Chỉ fallback khi cấu hình bật và lỗi thuộc nhóm hạ tầng được phép.
- Live fallback tắt nên request kết thúc bằng system escalation thay vì giả quyết định.

## Câu trả lời ngắn về SemaphoreSlim

`SemaphoreSlim` hoặc gate tương đương chỉ còn vai trò giới hạn capacity trong process. Nó không còn là nguồn job bền vững; SQL mới là queue chính. Khi scale nhiều process phải thay hoặc bao quanh gate bằng distributed coordination.
