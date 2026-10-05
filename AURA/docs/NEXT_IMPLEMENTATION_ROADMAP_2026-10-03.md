# AURA - Next implementation roadmap and acceptance gates

Cập nhật trạng thái: 05/10/2026. Mục tiêu là chốt MVP có bằng chứng trước 10/10, khóa bài nộp ngày 15/10 và demo ngày 17/10. Trạng thái chuẩn: [`CURRENT_PROJECT_STATUS_2026-10-05.md`](CURRENT_PROJECT_STATUS_2026-10-05.md); audit deployment: [`LIVE_DEPLOYMENT_EVIDENCE_AUDIT_2026-10-04.md`](LIVE_DEPLOYMENT_EVIDENCE_AUDIT_2026-10-04.md).

## 1. Trạng thái so với feedback Sprint 1

| Feedback | Trạng thái | Evidence hiện tại |
|---|---|---|
| Bỏ global upload gate | Hoàn thành | Upload không còn dùng `WorkflowOperationGate`; gate chỉ ngăn Verify chạy chồng |
| HTTP không chờ AI | Hoàn thành | Upload trả 202, DB-backed job, hosted worker, polling |
| Không mất job khi refresh/restart | Hoàn thành | Lease/reclaim và restart persistence pass |
| Lưu người nộp | Hoàn thành | Submitter code/name/department trong DB, UI và audit |
| Index audit | Hoàn thành | Index `(RequestId, Timestamp)` đã có migration |
| Fallback OpenRouter -> Ollama | Hoàn thành về chức năng, chưa bật mặc định | Live regression 1/1; audit provider đúng; E2E 84,425 s |
| Đo trên tập độc lập | Chưa hoàn tất | Judge 15 là synthetic locked set; receipt thật hiện là development samples |
| Ba người dùng thực tế | Chưa hoàn tất | Đang chờ kết nối/đồng thuận |
| Live URL sau hardening | Technical pass, final conditional | `c42c5ef`: health/DB/storage, tab, AUTO/FACT, Verify, workflow, recycle/persistence và concurrency pass; mobile 390 px + manual 4G/no-flash còn mở |

## 2. Traceability với yêu cầu Track A

| Yêu cầu | Hiện trạng | Gate còn lại |
|---|---|---|
| Policy cho quy trình cụ thể | Đạt | Không đổi rule sát demo nếu không có evidence |
| Tập tối thiểu 15 ca | Đạt bằng synthetic judge set 15/30 | Công bố rõ synthetic/real trên slide |
| Ba kiểu dừng FACT/POLICY/AUTHORITY | Đạt | Rehearsal phải show ít nhất một escalation rõ lý do |
| Câu hỏi chuyển tiếp cụ thể | Đạt | Kiểm lại trên 5 input mới của BGK |
| Không đoán khi input đáng ngờ | Đạt trong regression | Blind holdout phải có ca mờ/cắt/che |
| Không chuyển tiếp mọi ca | Đạt trong Verify và judge set | Báo missed/over-escalation trên holdout |
| Verify một thao tác, 3 auto + 2 escalate | Đạt và không thay expected | Ba dress rehearsal 5/5 dưới 90 s |
| Nhận input mới, không hard-code | Đạt về pipeline | Test 5 ca unseen hoặc holdout khóa trước |
| Audit, can thiệp, dừng/undo | Đạt | Rehearsal YES/NO/UNDO có timeline |
| Live URL | Automated technical gate pass, chưa final GO | Manual mobile/4G/no-flash, holdout, users và rehearsal |
| Accuracy trên tập độc lập | Chưa đạt đầy đủ | Khóa ground truth/hash trước khi chạy |
| Ít nhất 3 người dùng thực tế | Chưa đạt | Có danh tính/chức danh và phản hồi do chính họ viết |
| Điều chỉnh threshold từ feedback | Chưa triển khai | Chỉ làm calibration có governance; không tự sửa policy online |

## 3. P0 - bắt buộc hoàn tất trước khi thêm tính năng

### Gate A - Blind holdout 15 ca

Tạo một manifest mới và khóa trước lần gọi model đầu tiên. Mỗi record có `caseId`, nguồn dữ liệu, consent/redaction, SHA-256 ảnh, claimed amount, expected decision, expected critical fields và lý do nhãn.

Ma trận khuyến nghị:

- 5 ca routine/AUTO: nhiều merchant, receipt number, transaction reference, giảm giá, thuế/phụ phí;
- 4 ca FACT: mờ/cắt/che line items, tổng cạnh tranh, thiếu identifier truy vết;
- 3 ca POLICY: hàng cấm hoặc điều kiện policy không đạt;
- 3 ca AUTHORITY: vượt hạn mức hoặc cần người có thẩm quyền.

Có thể dùng tập mixed real + synthetic vì brief cho phép, nhưng phải công bố loại của từng ca. Ảnh thật phải có quyền sử dụng và được ẩn danh. Không dùng ảnh generative AI làm ground truth chữ/số chính.

Chạy OpenRouter đúng một lượt chính thức với fallback tắt. Không sửa expected sau khi thấy output. Báo cáo:

- decision exact;
- missed escalation = expected escalate nhưng actual AUTO / tổng expected escalate;
- over-escalation = expected AUTO nhưng actual escalate / tổng expected AUTO;
- critical-field exact;
- system-error rate, repair rate, P50/P95 end-to-end;
- danh sách bất đồng và phân tích nguyên nhân.

Gate pass tối thiểu cho demo: không missed escalation ở các ca safety-critical, không system error chưa giải thích, và mọi mismatch được giữ nguyên trong báo cáo.

### Gate B - ba người dùng thật

Brief Sprint 2 yêu cầu ít nhất ba người trực tiếp xử lý loại quy trình này. Trừ khi BTC/doanh nghiệp xác nhận miễn bằng văn bản, coi đây là bắt buộc.

Mỗi phiên 15-20 phút:

1. Ghi consent, họ tên/chức danh/đơn vị ở mức được phép công bố.
2. Giao cùng ba tác vụ: upload routine, xử lý escalation, tìm audit và undo.
3. Đo task completion, thời gian, số lần cần trợ giúp, hiểu đúng lý do quyết định và mức tin cậy 1-5.
4. Hỏi điểm gây nhầm, nguy cơ chấp nhận AI máy móc và thay đổi họ muốn nhất.
5. Người dùng tự viết/xác nhận feedback; không viết thay.
6. Chọn ít nhất một cải tiến có thể truy vết từ feedback, triển khai nhỏ và chạy regression.

Không chỉ đo “nhanh hơn”. Cần đánh giá workload, tính minh bạch, khả năng phản biện AI và chất lượng phối hợp người-máy.

### Gate C - Live URL revision cuối

Localhost vẫn là demo chính theo xác nhận của BTC. Live URL là evidence bàn giao và phải smoke lại nếu còn xuất hiện trong README/form/slide.

Pass khi health 200, database/migration/storage pass, UI desktop/mobile pass, một AUTO + một fail-safe đúng, Verify 5/5, YES/NO/UNDO có audit và recycle không làm mất ảnh/status/audit. Nếu fail, không dùng URL làm đường demo và ghi rõ trạng thái staging.

### Gate D - ba dress rehearsal

Chạy trên cùng commit candidate, OpenRouter primary, fallback tắt:

1. Preflight trước app: 0 failure.
2. Full preflight khi app chạy: health `ok`, DB/migration/storage/provider pass.
3. Verify 5/5 dưới 90 giây.
4. Một input mới hoặc fail-safe receipt.
5. Forward -> manager YES/NO -> audit -> UNDO.
6. Kết thúc đúng thời lượng, không lộ secret/PII và có video dự phòng.

Chỉ sửa P0 sau rehearsal; mọi sửa phải build/test và chạy lại gate bị ảnh hưởng.

## 4. P1 - chỉ làm khi bốn gate P0 đã ổn

### Calibration có governance

Không cho hệ thống tự thay policy tài chính trực tiếp từ vài feedback. Thay vào đó tạo báo cáo recommendation-only từ holdout/user feedback: threshold hiện tại, candidate threshold, confusion matrix và thay đổi missed/over-escalation. Con người phê duyệt version policy; audit ghi ai đổi, đổi gì, khi nào và rollback được.

### Remote Ollama/GPU POC

Chỉ làm bằng synthetic data trước. Yêu cầu private network/VPN hoặc reverse proxy TLS + auth + rate limit; AURA hiện chưa có bearer/API-key option cho remote Ollama. Đo 1, 2, 5 concurrent requests, VRAM, queue depth, P50/P95 và decision safety. Không public cổng 11434.

### UX từ user feedback

Ưu tiên thay đổi giảm lỗi thao tác: copy rõ lý do/câu hỏi escalation, trạng thái pending/failed, empty state, focus/scroll và thông báo retry. SignalR chỉ cần khi polling 5 giây thực sự gây vấn đề trong user test; không triển khai vì “realtime trông hay hơn”.

## 5. Backlog sau Chung kết

- Authentication/SSO/RBAC và tenant isolation.
- Object storage mã hóa, retention/deletion policy, backup/restore.
- Malware scanning, PDF/multi-page và e-invoice/tax lookup.
- Distributed queue/outbox và distributed circuit state.
- Observability: structured logs, metrics, traces, alerting và cost quota.
- Remote model authentication, secret rotation và private networking.
- Bounding boxes/visual grounding để highlight vùng lỗi sau khi model/provider chứng minh tọa độ ổn định.

Các mục này tăng production readiness nhưng không nên đánh đổi holdout, user validation hoặc rehearsal.

## 6. Kế hoạch từ 03/10 đến 15/10

| Ngày | Việc chính | Điều kiện kết thúc |
|---|---|---|
| 05-06/10 | Chốt 15 ảnh eligible, consent/redaction/ground truth; chuẩn bị user protocol | Manifest có hash, không còn nhãn sau khi xem output |
| 06-07/10 | Chạy holdout một lượt và phân tích lỗi | Có metrics + mismatch report trung thực |
| 08-09/10 | Ba user sessions và một cải tiến nhỏ | Có feedback gốc, before/after và regression |
| 10/10 | Feature freeze; smoke Live URL | Không P0 mở; URL có quyết định go/no-go |
| 11-12/10 | Cập nhật slide/video/pitch từ evidence | Slide không có claim vượt evidence |
| 13-14/10 | Ba dress rehearsal, backup package | Ba lượt pass hoặc blocker được loại khỏi demo |
| 15/10 | Chốt commit/artifact/hash | Repo sạch, link và artifact cùng revision |

Nếu đến 06/10 chưa có ba người dùng do phụ thuộc doanh nghiệp, gửi ticket/email nêu rõ dependency và xin xác nhận lịch hoặc phương án thay thế; không tự tạo người dùng/feedback giả.

## 7. Câu chuyện demo theo seminar

1. **Business problem:** tài chính mất thời gian ở receipt routine nhưng vẫn cần trách nhiệm giải trình.
2. **User need:** tự động hóa phần rõ ràng, dừng đúng lúc và nói rõ con người cần trả lời gì.
3. **Technical approach:** VLM đọc facts; semantic validator kiểm/repair; C# policy quyết định; SQL queue và audit bảo toàn tiến trình.
4. **MVP:** upload -> 202 -> AI -> AUTO/ESCALATE -> human action -> audit/undo.
5. **Validation:** synthetic locked set, blind holdout, user sessions, latency và failure evidence.
6. **Iteration:** feedback Sprint 1 tạo durable queue/submitter/fallback; receipt thật tạo guard ngày/discount/line items.
7. **Measurable impact:** auto rate an toàn, missed/over-escalation, thời gian/cost mỗi receipt, reviewer effort và mức hiểu quyết định.

Pitch phải bắt đầu từ vấn đề và tác động, không bắt đầu từ tên model hoặc cloud.

## 8. Definition of done

MVP sẵn sàng Chung kết khi suite candidate hiện tại 108/108 pass, build sạch, bốn gate P0 hoàn tất, Verify không đổi 3/2, không có P0/P1 crash blocker, evidence liên kết commit/provider/model/timestamp, ảnh thật không lộ PII và người trình bày tự giải thích được toàn bộ luồng. Production readiness là một mốc khác và không được tuyên bố ở giai đoạn này.
