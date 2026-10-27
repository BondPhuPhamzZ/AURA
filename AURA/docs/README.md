# Tài liệu kỹ thuật AURA

**Mốc local 27/10:** [Đối chiếu ba ca Drive synthetic và ba ca real bị chặn trước inference](USER_DRIVE_PRIVACY_CHECK_2026-10-27.md). Đây là evidence supervised local; không đo upload-to-result latency và không đổi gate SmartASP.

Thư mục này chứa tài liệu kỹ thuật hiện hành **và các báo cáo evidence theo ngày**. Báo cáo có ngày là snapshot tại thời điểm đo; không dùng số liệu trong đó thay cho trạng thái source hiện tại. Tài liệu cá nhân về pitching và ôn phản biện nằm ngoài repository tại `D:\aura\phan_bien`.

| Tài liệu | Mục đích |
|---|---|
| `CURRENT_PROJECT_STATUS.md` | Nguồn sự thật duy nhất về baseline đã xác minh và các gate còn mở |
| `DRIVE_PROGRESS_UI_VERIFICATION_2026-10-27.md` | Evidence AJAX hiển thị hồ sơ Drive đang chờ/chạy/kết quả, test và ranh giới publish |
| `MODEL_ROUTE_COMPARISON_2026-10-24.md` | Regression v3.1 qua policy/Semantic và egress gate; giữ nguyên số đo trước/sau cùng giới hạn claim |
| `SHARED_RECEIPT_STORAGE.md` | Adapter GCS opt-in, cấu hình bucket private và các gate integration/restore còn thiếu |
| `READINESS_AND_SCALE_ASSESSMENT_2026-10-23.md` | Kết luận supervised MVP/Track A/production, phạm vi phòng ban, UX refresh và đa máy chủ |
| `STORAGE_AND_DATASET_GATE_2026-10-23.md` | Gate storage dưới `wwwroot`, kiểm hash dataset và smoke local |
| `DRIVE_MVP_VALIDATION_2026-10-22.md` | Bằng chứng batch Drive 5/20/100 và 15 ảnh consent qua manual intake; không phải accuracy |
| `DRIVE_PIPELINE_HARDENING_2026-10-22.md` | Thay đổi idempotency và giới hạn scan Drive |
| `DRIVE_SANDBOX_LOCAL.md` | Setup và rollback connector Drive synthetic chỉ cho localhost Development |
| `DRIVE_SANDBOX_EVIDENCE_TEMPLATE.md` | E2E Drive→SQL→AI ngày 22/10, false positive đã sửa và giới hạn claim |
| `DRIVE_SANDBOX_CHECKLIST.md` | Checklist đã xác minh/chưa có evidence, bước kiểm thử tiếp theo và GO/NO-GO hiện tại |
| `AFTER_FEEDBACK_IMPLEMENTATION.md` | Phạm vi triển khai ngày 17/10, bằng chứng test, demo flow mới và các ranh giới chưa-production |
| `POST_CONTEST_HARDENING_2026-10-21.md` | Gate Verify/rate limit mới, evidence 196 test và điều kiện NO-GO pilot dữ liệu thật |
| `AFTER_FEEDBACK_ARCHITECTURE_CHECKLIST.md` | Đối chiếu recommend check, 10 architecture gates, khác biệt trước/sau và thứ tự phát triển tiếp theo |
| `EVALUATION_EVIDENCE.md` | Chỉ mục tách rõ baseline nộp bài và evidence hậu cuộc thi |
| `RUNBOOK.md` | Quy trình vận hành và khôi phục |
| `JUDGE_LOCAL_SETUP.md` | Hướng dẫn BGK chạy AURA tại máy local |
| `DEPLOYMENT.md` | Cấu hình triển khai và ranh giới an toàn |
| `LOCAL_OLLAMA.md` | Phương án local có giám sát, không bắt buộc |
| `TEST_CASES.md` | Danh mục test và hành vi kỳ vọng |
| `DATASET_EVALUATION_GUIDE.md` | Dataset, manifest và protocol đánh giá |
| `MEASUREMENT_PLAN.md` | Metric và quy tắc diễn giải |
| `EVIDENCE_LOCALIZATION_PLAN.md` | Tính khả thi của overlay khoanh vùng và giới hạn chưa-production |
| `USER_STUDY_PROTOCOL.md` | Protocol lấy ý kiến ba người dùng dựa trên consent |
| `SUBMISSION_CHECKLIST.md` | Checklist điều phối nộp bài và rehearsal lịch sử; không phải release gate hiện hành |
| `CURRENT_ACTION_CHECKLIST.md` | Giai đoạn local synthetic đã chốt, gate dữ liệu thật/hosting/scale còn mở; checklist thi cũ tách riêng |
| `../BUSINESS_RULES.md` | Bản tiếng Anh chuẩn đang đồng bộ với model contract và policy hiện hành |
| `BUSINESS_RULES_VI.md` | Bảng dịch tiếng Việt công khai cho BGK; được loại khỏi build output và gói publish |
| `PROMPT_BUSINESS_RULES_BENCHMARK_20261023.md` | Phản hồi đánh giá prompt/business rules, thay đổi đã test và benchmark mới cần chạy |

Hóa đơn thô, raw provider response, API dashboard, database, secret và dữ liệu định danh cá nhân được chủ động loại khỏi Git. Gói evidence riêng tư giữ nguyên timestamp và hash ban đầu; không đổi tên hoặc tạo lại để làm chúng trông mới hơn.

Review 26/10 về 12 ảnh tham khảo deploy/architecture, Drive 3 ảnh + restart,
UX/HITL và boundary publish/Docker nằm trong [assessment hiện hành](READINESS_AND_SCALE_ASSESSMENT_2026-10-23.md).
Default source/README là `master`; mốc cũ được giữ qua tag
`archive/master-before-hardening-20261026`. [Gate SmartASP](DEPLOYMENT.md) vẫn tách
biệt với việc đồng bộ Git; chưa publish candidate mới.

Ngày cũ trong `DATASET_EVALUATION_GUIDE.md`, `DEPLOYMENT.md`, `LOCAL_OLLAMA.md` hoặc `TEST_CASES.md` là ngày của protocol hay baseline đo tương ứng, không có nghĩa candidate hiện tại vẫn dùng model cũ. Cấu hình và release gate hiện hành nằm trong `CURRENT_PROJECT_STATUS.md`; số liệu công khai hiện hành nằm trong `EVALUATION_EVIDENCE.md`.

Các artifact nộp bài được lập chỉ mục riêng tại `../submission/README.md`.
