# Checklist nộp bài chính thức AURA

**Cập nhật:** 17/10/2026

**Trạng thái hiện hành:** `CURRENT_PROJECT_STATUS.md`

**Danh mục gói nộp:** `../submission/README.md`

## Hạng mục bắt buộc

| Hạng mục | Trạng thái | Nguồn chốt |
|---|---|---|
| Repository source công khai và README | Sẵn sàng | `README.md` ở root repository |
| Ứng dụng live | Baseline 14/10 đang khỏe; candidate sau feedback chưa publish/smoke | URL trong README và `CURRENT_PROJECT_STATUS.md` |
| Slide 5 trang | Sẵn sàng | `../submission/AURA_5_SLIDES_AFTER_FEEDBACK_FINAL.pptx` |
| Proposal tiếng Việt và tiếng Anh | Sẵn sàng | `../submission/AURA_PROPOSAL_VI.*`, `../submission/AURA_PROPOSAL_EN.*` |
| Build Log một trang của toàn dự án | Sẵn sàng | `../submission/AURA_BUILD_LOG.*` |
| Video demo sản phẩm tối đa 3 phút | Đã upload | URL trong `../submission/AURA_SUBMISSION_LINKS.*` |
| Verify Harness và hướng dẫn BGK | Sẵn sàng | `JUDGE_LOCAL_SETUP.md`, `RUNBOOK.md` |
| Ba biên bản phản hồi người dùng thật | Gate còn mở | `USER_STUDY_PROTOCOL.md`, biểu mẫu trong `../submission/` |
| Quyền Team Drive và liên kết cuối | Đã upload; còn kiểm tra ẩn danh lần cuối | `../submission/AURA_SUBMISSION_LINKS.*` |

## Sơ đồ thư mục Team Drive

| Thư mục Drive | File lấy từ repository | Việc cần làm |
|---|---|---|
| `00_READ_FIRST` | `../submission/AURA_SUBMISSION_LINKS.pdf` và `README.md` ngắn | Chỉ giữ một PDF hiện hành; mở từng URL trong cửa sổ ẩn danh |
| `01_SLIDES` | `../submission/AURA_5_SLIDES_AFTER_FEEDBACK_FINAL.pptx` | Sẵn sàng |
| `02_PROPOSAL` | `../submission/AURA_PROPOSAL_VI.pdf`, `../submission/AURA_PROPOSAL_EN.pdf` | Sẵn sàng |
| `03_VIDEO` | MP4 chính thức được dẫn trong `../submission/AURA_SUBMISSION_LINKS.*` | Đánh dấu rõ một video dưới 3 phút là bản chính thức; các video hóa đơn thật chỉ là bổ sung |
| `04_BUILD_LOG` | `../submission/AURA_BUILD_LOG.pdf` | Chỉ dùng một Build Log một trang cho toàn dự án |
| `05_GITHUB_AND_RUNBOOK` | README root, `RUNBOOK.md`, `JUDGE_LOCAL_SETUP.md`, `TEST_CASES.md`, `../BUSINESS_RULES.md`, `../submission/AURA_WORKFLOW_SPEC.md` | Upload sáu file; không sao chép source code hoặc secret vào Drive |
| `06_USER_EVIDENCE_PENDING` | `USER_STUDY_PROTOCOL.md`, `../submission/AURA_USER_FEEDBACK_FORM.docx`, `../submission/AURA_USER_FEEDBACK_FORM.pdf` | Upload template; chỉ thêm record thật có consent khi đã thu thập |
| `07_EVIDENCE_SUMMARY` | `CURRENT_PROJECT_STATUS.md`, `EVALUATION_EVIDENCE.md` | Upload hai bản tổng hợp hiện hành |

## Trước khi trình bày

1. Dùng candidate Qwen3.8 Flash đã đóng băng với reasoning `none` và live fallback tắt.
2. Kiểm `/healthz`: status ok; database, storage và migration khỏe; provider/model đúng.
3. Chạy Verify Harness 5 ca đúng một lần; kết quả phải dưới 90 giây với 3 AUTO và 2 ESCALATE.
4. Không chạy Verify đồng thời với benchmark upload hoặc một Verify batch khác.
5. Ẩn browser history, API dashboard, secret, console có key và PII hóa đơn thật.
6. Ôn phương án dự phòng: refresh trước, kiểm status/Audit, sau đó dùng video đã quay nếu upstream provider không sẵn sàng.

## Trước khi chốt Team Drive

1. Điền đủ các thư mục trong bảng trên và chỉ giữ một bản cuối cho mỗi hạng mục.
2. Chỉ giữ DOCX/PPTX trên Team Drive khi BTC cần file nguồn; PDF là định dạng đọc mặc định.
3. Mở mọi liên kết bằng cửa sổ ẩn danh hoặc tài khoản không có quyền owner.
4. Xác nhận video chính thức không quá 180 giây và có lời thoại nghe rõ.
5. Xác nhận repository không chứa hóa đơn thật, API key, user secrets, database, publish profile, provider payload hoặc ảnh billing.
6. Ghi lại commit Git cuối và thời điểm upload Drive; không âm thầm thay evidence sau hạn nộp.

## File không được đưa lên Git hoặc Drive công khai

API key, production connection string, `.env`, user secrets, publish credential, `.vs/`, `bin/`, `obj/`, `.tmp/`, database local, hóa đơn thật, ảnh chưa redaction, raw provider response và ảnh billing/key dashboard.
