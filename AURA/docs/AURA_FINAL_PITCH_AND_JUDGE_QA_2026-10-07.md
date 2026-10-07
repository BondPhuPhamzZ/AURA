# AURA — Pitch hiện hành và Judge Q&A

Cập nhật: 07/10/2026. Dùng bản 7–8 phút cho vòng bảng, giữ 5 phút Q&A và 2 phút buffer.

## Script 7–8 phút

### 0:00–0:45 — Vấn đề

“Hoàn ứng không khó chỉ vì thiếu OCR. Vấn đề là xác định hồ sơ nào đủ bằng chứng để đi tiếp và
hồ sơ nào phải dừng cho con người. AURA giảm đọc lặp lại nhưng không giao quyền duyệt cho AI.”

### 0:45–1:30 — Giải pháp

“Qwen3-VL đọc dữ kiện có cấu trúc. Backend kiểm evidence; PolicyDecisionEngine C# quyết định
theo FACT, POLICY, AUTHORITY. Ca bất định luôn fail-safe và con người sở hữu ngoại lệ.”

### 1:30–4:15 — Demo

1. Upload một ảnh và chỉ HTTP 202/status polling.
2. Mở facts: raw date, printed-total evidence, identifier.
3. Chỉ một AUTO và một escalation.
4. Forward → manager YES/NO → Audit → Undo.
5. Nói rõ Verify là regression fixture, không phải accuracy thực tế.

### 4:15–5:10 — Kiến trúc

“Job nằm trong SQL với lease/reclaim nên refresh/restart không làm mất hồ sơ. Hai provider dùng
cùng contract/validator/policy. Fallback chỉ dành cho lỗi hạ tầng allowlist và hiện đang tắt.”

### 5:10–6:35 — Evidence trung thực

“Candidate có 142 code tests. Official holdout raw đạt 10/15; một nhãn người sai tạo
adjudicated view 11/15 nhưng raw không sửa. Sau hardening, chạy lại cùng 15 receipt dưới nhãn
post-holdout regression vẫn 11/15 exact, nhưng missed escalation giảm từ 1 xuống 0; hệ thống
an toàn hơn và over-escalate 4/5 routine. V3.1 tổng hợp đạt 0 missed và 0 system error. Đây là
supervised MVP evidence, không phải production accuracy.”

### 6:35–7:20 — Giá trị và giới hạn

“AURA ưu tiên explainability, Audit và fail-safe thay vì săn AUTO. Còn thiếu auth/RBAC,
PDF/multi-page, retention/object storage và validation trên dataset lớn.”

### 7:20–8:00 — Tiếp theo

“Team benchmark Ollama 8B trên GPU BTC bằng cùng v3.1, hoàn tất user session/rehearsal và giữ
OpenRouter làm baseline cho tới khi fallback đạt safety gate.”

## Bản nén 5 phút

- 0:00–0:30: vấn đề/user.
- 0:30–1:05: VLM reads, C# decides, human owns exceptions.
- 1:05–2:50: upload → result → forward → Audit/Undo.
- 2:50–3:30: feature onsite và acceptance evidence.
- 3:30–4:35: holdout/regression/safety/limits.
- 4:35–5:00: giá trị và kết luận.

## Q&A cốt lõi

### Vì sao dùng AI nếu C# quyết định?

AI xử lý layout/chữ biến thiên cao; C# giữ authority cho rule cần tất định, test, audit và rollback.

### Vì sao regression 11/15 vẫn được coi là cải thiện?

Exact bằng adjudicated view cũ, nhưng lỗi nguy hiểm BH-07 AUTO sai đã thành FACT và missed giảm
từ 1 xuống 0. Đổi lại over-escalation tăng 2→4; đây là safety/automation trade-off được báo rõ.

### 142 tests có phải 142 hóa đơn?

Không. Đó là code tests offline. Chất lượng vision được đo riêng bằng Verify, synthetic kits,
real holdout và post-holdout regression.

### Vì sao không nới validator để BH-14/BH-15 AUTO?

Cùng mã đang nằm ở hai vai trò identifier. Nếu tự đoán để tăng AUTO có thể liên kết nhầm giao
dịch. Trước demo, fail-safe và human review tốt hơn heuristic chưa có evidence.

### Vì sao 15 receipt cũ không còn blind?

Output đã được xem và dùng để harden code. Lượt chạy lại chỉ đo regression; blind claim mới cần
receipt unseen, nhãn/hash khóa trước request đầu tiên.

### Fallback Ollama đang chạy trên SmartASP đúng không?

Không. Live fallback tắt. Ollama chỉ chạy khi process self-host trên cùng máy AURA; GPU BTC là
benchmark cô lập, không phải backend 24/7 cho SmartASP.

### Tại sao một request chỉ có một hóa đơn?

Mỗi ảnh có claimed amount, facts, decision và Audit riêng. Gộp nhiều ảnh vào một form hiện tại
sẽ làm mất mapping. Batch runner chỉ tự động gửi nhiều request độc lập.

### PDF có khả thi không?

Có, nhưng minimum safe scope phải có signature/MIME check, page/size limit, raster hóa,
provenance theo trang, malware guard và benchmark lại. Không chỉ thêm `.pdf` vào accept list.

### Hệ thống production-ready chưa?

Chưa. Đây là supervised MVP. Còn auth/RBAC, storage/retention, malware scan, monitoring/rate
limit, backup/restore và dataset lớn.

### Nếu đề onsite quá lớn?

Chia Must/Should/Cut, khóa acceptance criteria và làm vertical slice nhỏ nhất có negative case,
test và evidence. Công bố phần cắt thay vì hy sinh authority/fail-safe.

## Câu chèn feature onsite

“Sáng nay BTC yêu cầu [...]. Team chuyển thành acceptance criteria [...], chọn vertical slice
[...], sửa ở lớp [...], và chứng minh bằng [...]. Phần [...] được cắt vì [...].”

## Những câu không được nói

- “AURA chính xác 100%.”
- “142 test nghĩa là 142 hóa đơn đúng.”
- “11/15 là production accuracy.”
- “Ollama đang fallback trên SmartASP.”
- “Hệ thống đã production-ready.”
- “Lượt chạy lại 15 receipt là blind holdout mới.”
