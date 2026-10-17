# Tài liệu kỹ thuật AURA

Thư mục này chỉ chứa các tài liệu kỹ thuật hiện hành dành cho BGK. Các báo cáo tiến độ cũ và snapshot theo ngày đã được đưa khỏi cây tài liệu đang dùng để tránh hướng dẫn mâu thuẫn; nội dung vẫn có thể truy xuất từ Git history và release tag đóng băng.

| Tài liệu | Mục đích |
|---|---|
| `CURRENT_PROJECT_STATUS.md` | Nguồn sự thật duy nhất về baseline đã xác minh và các gate còn mở |
| `AFTER_FEEDBACK_IMPLEMENTATION.md` | Phạm vi triển khai ngày 17/10, bằng chứng test, demo flow mới và các ranh giới chưa-production |
| `AFTER_FEEDBACK_ARCHITECTURE_CHECKLIST.md` | Đối chiếu recommend check, 10 architecture gates, khác biệt trước/sau và thứ tự phát triển tiếp theo |
| `EVALUATION_EVIDENCE.md` | Bản đồ kết quả công khai, ranh giới claim và vị trí evidence thô riêng tư |
| `RUNBOOK.md` | Quy trình vận hành và khôi phục |
| `JUDGE_LOCAL_SETUP.md` | Hướng dẫn BGK chạy AURA tại máy local |
| `DEPLOYMENT.md` | Cấu hình triển khai và ranh giới an toàn |
| `LOCAL_OLLAMA.md` | Phương án local có giám sát, không bắt buộc |
| `TEST_CASES.md` | Danh mục test và hành vi kỳ vọng |
| `DATASET_EVALUATION_GUIDE.md` | Dataset, manifest và protocol đánh giá |
| `MEASUREMENT_PLAN.md` | Metric và quy tắc diễn giải |
| `EVIDENCE_LOCALIZATION_PLAN.md` | Tính khả thi của overlay khoanh vùng và giới hạn chưa-production |
| `USER_STUDY_PROTOCOL.md` | Protocol lấy ý kiến ba người dùng dựa trên consent |
| `SUBMISSION_CHECKLIST.md` | Checklist nộp bài và rehearsal cuối |
| `MAILBOX_SANDBOX_RUNBOOK.md` | Cấu hình, migration và smoke cho mailbox synthetic; không phải Gmail connector |
| `CURRENT_ACTION_CHECKLIST.md` | Checklist publish, bật mailbox sandbox, smoke và gate hóa đơn tiếng Trung |
| `../BUSINESS_RULES.md` | Bản tiếng Anh chuẩn đang đồng bộ với model contract và policy hiện hành |
| `BUSINESS_RULES_VI.md` | Bảng dịch tiếng Việt công khai cho BGK; được loại khỏi build output và gói publish |

Hóa đơn thô, raw provider response, API dashboard, database, secret và dữ liệu định danh cá nhân được chủ động loại khỏi Git. Gói evidence riêng tư giữ nguyên timestamp và hash ban đầu; không đổi tên hoặc tạo lại để làm chúng trông mới hơn.

Ngày cũ trong `DATASET_EVALUATION_GUIDE.md`, `DEPLOYMENT.md`, `LOCAL_OLLAMA.md` hoặc `TEST_CASES.md` là ngày của protocol hay baseline đo tương ứng, không có nghĩa candidate hiện tại vẫn dùng model cũ. Cấu hình và release gate hiện hành nằm trong `CURRENT_PROJECT_STATUS.md`; số liệu công khai hiện hành nằm trong `EVALUATION_EVIDENCE.md`.

Các artifact nộp bài được lập chỉ mục riêng tại `../submission/README.md`.
