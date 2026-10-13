# Đề xuất giải pháp AURA cho bài toán hoàn ứng

**Track:** OrganizationAI
**Đề bài:** Spec A The Escalation Referee
**Đội:** The Deciders
**Tác giả:** Pham Gia Phu
**Phiên bản bằng chứng:** 13/10/2026

## Tóm tắt điều hành

AURA là trợ lý thẩm định tuyến đầu cho quy trình hoàn ứng. Hệ thống nhận ảnh hóa đơn cùng số tiền nhân viên khai, dùng mô hình thị giác để trích xuất dữ kiện có cấu trúc, kiểm tra chéo dữ kiện và áp dụng policy C# tất định. Hồ sơ rõ ràng có thể đi tiếp tự động; hồ sơ thiếu bằng chứng, ngoài chính sách, vượt thẩm quyền hoặc gặp lỗi hạ tầng được chuyển cho con người cùng một câu hỏi cụ thể và nhật ký kiểm toán.

Quyết định thiết kế cốt lõi là tách quyền đọc khỏi quyền duyệt. Qwen3.8 Flash qua OpenRouter/Alibaba chỉ đọc dữ kiện. `PolicyDecisionEngine` mới có quyền tạo `AUTO_APPROVE` hoặc một trạng thái `ESCALATE_*`. Con người xác nhận chuyển tiếp, trả lời câu hỏi ngoại lệ, có thể hoàn tác quyết định gần nhất và vẫn giữ toàn bộ lịch sử.

Candidate hiện phù hợp cho demo có giám sát. Build Release sạch, 163/163 test offline đạt, Verify Harness live đạt 5/5 trong 65,004 giây. Prospective mini-holdout 10 hóa đơn thật mới đạt 8/10 quyết định đúng, 0 missed-escalation, 2 over-escalation fail-safe và 0 lỗi hệ thống/provider/429. Đây là mẫu nhỏ có redaction confounder, không phải production accuracy. Ba phiên người dùng thực tế vẫn là cổng chưa hoàn tất.

## 1 Bài toán và mục tiêu

Quy trình hoàn ứng có hai rủi ro đối nghịch. Nếu tự động hóa quá tự tin, một hóa đơn mờ, thiếu căn cứ hoặc chứa hạng mục bị cấm có thể được duyệt nhầm. Nếu hệ thống chuyển mọi hồ sơ cho quản lý, tổ chức không giảm được công việc thủ công. AURA tối ưu ranh giới giữa hai lỗi này, ưu tiên không bỏ sót ca cần chuyển tiếp nhưng vẫn đo và công khai chi phí over-escalation.

Mục tiêu Sprint 2 của AURA gồm bốn phần:

- xử lý trọn luồng từ upload đến quyết định và audit;
- tách ba nhóm bất định FACT, POLICY và AUTHORITY;
- tạo câu hỏi để người duyệt có thể quyết định ngay;
- đo missed-escalation, over-escalation, field accuracy, latency và khả năng phục hồi thay vì chỉ trình diễn một output đẹp.

## 2 Phạm vi nghiệp vụ

AURA nhận một ảnh JPG hoặc PNG tối đa 5 MB và số tiền hoàn ứng do nhân viên khai. Candidate chưa hỗ trợ PDF, nhiều trang, hóa đơn điện tử có chữ ký số, xác minh cơ quan thuế, chống malware, authentication/RBAC production hoặc object storage.

| Trạng thái | Điều kiện chính | Người có trách nhiệm tiếp theo |
|---|---|---|
| `AUTO_APPROVE` | Dữ kiện đủ, số tiền khớp, không có hạng mục cấm, không vượt thẩm quyền | Quy trình demo đi tiếp tự động |
| `ESCALATE_FACT` | Ảnh/dữ kiện thiếu, mâu thuẫn, ngày cuối tuần/quá hạn hoặc evidence không đủ | Người kiểm tra hóa đơn gốc |
| `ESCALATE_POLICY` | Có hạng mục bị cấm hoặc cần ngoại lệ policy | Quản lý quyết định ngoại lệ |
| `ESCALATE_AUTHORITY` | Tổng hợp lệ nhưng vượt 1.000.000 VND | Người có thẩm quyền cao hơn |
| `ESCALATE_SYSTEM_ERROR` | Provider, contract hoặc hạ tầng không cho kết quả tin cậy | Operator xử lý sự cố, không tạo quyết định giả |

Policy áp dụng thứ tự `FACT → POLICY → AUTHORITY`. FACT đứng trước vì không được tự động ra quyết định trên dữ kiện chưa đáng tin. Claimed amount không đi vào prompt trích xuất để tránh model bị neo theo số người dùng khai; backend chỉ so sánh sau khi extraction hoàn tất.

## 3 Luồng xử lý và vai trò con người

```text
Ảnh + claimed amount
  -> kiểm file và lưu hồ sơ PENDING
  -> trả HTTP 202
  -> SQL worker claim bằng lease
  -> Qwen trích xuất JSON có evidence
  -> semantic validation và tối đa một repair
  -> policy C# quyết định
  -> AUTO_APPROVE hoặc ESCALATE_*
  -> nhân viên xác nhận chuyển tiếp
  -> quản lý Đồng ý hoặc Từ chối
  -> Audit và Undo
```

Con người xuất hiện tại hai điểm rõ ràng. Nhân viên quyết định có chuyển hồ sơ ngoại lệ vào hàng đợi quản lý hay không; AURA không chuyển âm thầm. Quản lý trả lời câu hỏi có dữ kiện và lựa chọn cụ thể. `Undo` không xóa quyết định cũ mà thêm một sự kiện mới, giữ trách nhiệm giải trình.

## 4 Kiến trúc và khả năng phục hồi

| Lớp | Thành phần thực tế | Ranh giới |
|---|---|---|
| Giao diện | ASP.NET Core Razor, upload bất đồng bộ, polling trạng thái, reviewer và audit | Chưa có identity/RBAC production |
| Tiếp nhận | Kiểm extension, MIME, magic bytes, kích thước, tên GUID và SHA-256 | Chưa có antivirus/CDR |
| Hàng đợi | SQL là nguồn bền vững; worker dùng lease/reclaim; in-memory channel chỉ đánh thức | Một process demo, chưa có distributed queue |
| AI extraction | OpenRouter/Qwen3.8 Flash, Alibaba route, reasoning none | Phụ thuộc Internet và upstream quota |
| Validation | JSON Schema, identifier/money/date/evidence guard, một repair | Không suy số cuối từ subtotal khi evidence rách |
| Quyết định | Policy C# tất định | Model không có quyền phê duyệt |
| Con người và audit | Forward, manager YES/NO, Undo, timeline | Audit app-level, chưa phải WORM compliance |

AI concurrency được giữ ở 1 để giảm burst upstream. Verify có khóa riêng để hai batch không chạy chồng; bốn client phụ nhận HTTP 409 thay vì nhân số lần gọi AI. Nhiều upload khác nhau vẫn được nhận HTTP 202 và xếp hàng trong SQL. Gate hiện tại là per-process; scale nhiều IIS worker/server cần distributed limiter và circuit state.

## 5 Phương pháp đánh giá

AURA tách các loại bằng chứng để tránh trộn lẫn mục đích:

| Phép đo | Chứng minh | Không chứng minh |
|---|---|---|
| 163 test offline | Correctness của policy, contract, queue, fallback, audit và UI contract | Độ chính xác trên 163 hóa đơn |
| Verify 5 ca | Gate nhanh đúng yêu cầu Spec A, 3 AUTO và 2 ESCALATE | Production accuracy |
| Prospective holdout 10 | Khả năng tổng quát tại lượt đầu trên mẫu nhỏ đã khóa | Hiệu năng sản xuất quy mô lớn |
| Regression 12 ảnh đã biết | Stability, variance và queue/concurrency | Holdout mới hoặc causal A/B |
| User study | Usability, review time, tải công việc và tác động tiêu cực | Model accuracy |

Ground truth, expected status và SHA-256 được khóa trước request đầu tiên. Raw evidence không bị sửa sau khi xem output. Lượt chạy lại trên dữ liệu đã biết luôn được gọi đúng là regression hoặc stability repeat.

## 6 Kết quả đã kiểm chứng

| Gate | Kết quả | Diễn giải an toàn |
|---|---:|---|
| Release build | 0 warning, 0 error | Gate build hiện hành |
| Automated tests | 163/163 | Test code offline, không gọi paid AI API |
| Live Verify | 5/5 trong 65,004 giây | Đạt gate 5 ca dưới 90 giây |
| Prospective mini-holdout | 8/10, 0 missed, 2 over, 0 lỗi hệ thống/provider/429 | Một official run, không rerun để săn số |
| Field trọng yếu prospective | 28/30 | Chỉ date, canonical identifier và final total |
| Regression 12 source bytes | 10/12, 0 missed, 2 over, 35/36 field | Dữ liệu đã biết, không phải holdout |
| Sequential repeat | 11/12, 0 missed, 1 over, P95 32,903 giây | Stability evidence |
| Năm upload đồng thời | 5/5 hoàn tất, E2E P95 93,684 giây | Queue integrity, không phải Verify SLA |

FAHASA là over-escalation ổn định vì validator chưa công nhận dòng `Thanh toán 91.500 đ` là final-payable label. GS25_2 biến thiên ở merchant name gần vùng blackout. Team không hard-code merchant và không nới guard ngay trước demo. Bản sửa an toàn cần fixture dương cho final payable và fixture âm cho tender, tiền khách đưa và tiền thối.

## 7 Câu chuyện phát triển sản phẩm

Điểm ngoặt của AURA không đến từ một lần đạt 15/15 regression. Official real-receipt holdout từng phát hiện một missed escalation: model có thể trả JSON hợp lệ nhưng dữ kiện vẫn sai nghĩa. Sau đó AURA chuyển từ “model đọc được hóa đơn” sang “mọi dữ kiện quan trọng phải có vai trò và provenance”. Evidence contract v2 tách identifier, raw date, printed final total và discount; semantic validator chặn mâu thuẫn; kết quả không chắc chắn luôn dừng cho người.

Từ đó, nhóm không dùng một con số đẹp làm kết luận. Mỗi phép đo có nhãn đúng: blind holdout, post-holdout regression, stability repeat hoặc user study. Đây là câu chuyện chính khi trình bày với doanh nghiệp: AURA trưởng thành qua việc phát hiện giới hạn, khóa evidence và chuyển lỗi thành guard có thể kiểm thử.

## 8 Nghiên cứu người dùng

Đề Track yêu cầu ít nhất ba người đang trực tiếp xử lý quy trình, nêu danh tính và chức danh thật, phản hồi nguyên văn, một thay đổi sản phẩm từ feedback và một tác động tiêu cực cụ thể. Tại thời điểm phiên bản này, gate đó chưa hoàn tất. AURA đã chuẩn bị protocol và biểu mẫu đồng thuận; proposal không điền giả người dùng hoặc trích dẫn.

Mỗi phiên dự kiến 20–25 phút với dữ liệu tổng hợp: một upload routine, một escalation/handoff, một manager decision/Audit/Undo. Người điều phối ghi task success, review time, số lần cần trợ giúp, quote do người tham gia tự viết hoặc xác nhận, điểm khó hiểu và tác động tiêu cực như thời gian chờ AI, tải review dồn hoặc nguy cơ ỷ lại.

## 9 Quyền riêng tư và đạo đức

Ảnh hóa đơn thật chỉ được dùng khi chủ dữ liệu cho phép và đã redaction. Raw receipt, raw provider response, database, log vận hành và secret được giữ ngoài repository công khai. Proposal, slide và video chỉ dùng dữ liệu tổng hợp hoặc số liệu tổng hợp không chứa PII.

Evidence localization bằng bounding box hiện là prototype tổng hợp cô lập. Ảnh gốc giữ nguyên bytes, region JSON gắn SHA-256 và browser vẽ overlay. Tính năng chưa có annotation unseen và chưa tham gia quyết định policy, nên không được trình bày như capability production.

## 10 Giới hạn và lộ trình

Candidate là supervised MVP, chưa phải hệ thống hoàn ứng production. Ba ưu tiên tiếp theo là:

1. hoàn tất ba user session có consent và một thay đổi truy được bằng commit;
2. sau submission freeze, giảm over-escalation bằng fixture dương/âm độc lập mà không tăng missed-escalation;
3. bổ sung authentication/RBAC, private object storage, retention, monitoring, distributed provider limiter và safe PDF pipeline trước pilot thực tế.

Evidence overlay chỉ đi tiếp sau khi có OCR word boxes, annotation độc lập và benchmark region recall/false highlight. Ollama 4B được giữ làm contingency local có giám sát; fallback trên live vẫn tắt vì latency và endpoint loopback không phù hợp hosting công cộng.

## 11 Khả năng tái lập

- Live: https://bondphupham-001-site1.ltempurl.com/
- Health: https://bondphupham-001-site1.ltempurl.com/healthz
- Repository: https://github.com/BondPhuPhamzZ/AURA
- Runbook: `AURA/docs/RUNBOOK.md`
- Business rules: `AURA/BUSINESS_RULES.md`
- Workflow: `AURA/submission/AURA_WORKFLOW_SPEC.md`
- Current status: `AURA/docs/CURRENT_PROJECT_STATUS_2026-10-13.md`
- User-study protocol: `AURA/docs/USER_STUDY_PROTOCOL_2026-10-13.md`

## Kết luận

AURA chứng minh một cách triển khai AI có trách nhiệm cho hoàn ứng: model xử lý dữ liệu biến thiên, code tất định giữ quyền quyết định, con người xử lý ngoại lệ và evidence được giữ nguyên để tái lập. Bằng chứng hiện tại đủ cho demo có giám sát và một pilot nhỏ có kiểm soát; chưa đủ cho claim accuracy sản xuất hoặc vận hành không giám sát.
