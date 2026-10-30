# Gói nộp bài chính thức AURA

**Hosting:** SmartASP trial được chủ dự án xác nhận hết hạn ngày **20/11/2026**. Link trong `AURA_SUBMISSION_LINKS.*` thuộc bản thi lịch sử; sau ngày hết hạn hãy clone repo và dùng hướng dẫn local trong README gốc, rồi liên hệ chủ dự án riêng để nhận API key. Bản source mới chỉ được ghi là live sau khi publish và smoke thành công.

**Evidence bổ sung 27/10:** [Báo cáo kiểm thử Drive synthetic và privacy gate](../docs/USER_DRIVE_PRIVACY_CHECK_2026-10-27.md) là ghi chú source hiện hành. Các PDF/DOCX đã nộp vẫn là artifact lịch sử, không được hiểu là báo cáo triển khai SmartASP hoặc kiểm thử ảnh thật bằng model. Ảnh evidence riêng tư không nằm trong repo.

Proposal và build log có **ghi chú hậu cuộc thi ngày 26/10** đặt tách khỏi số liệu lúc nộp. Không đọc 163/181 test hoặc Verify 5/5 của bản cũ như kết quả của source mới. Slide hiện hành là `AURA_SLIDE.pptx`; bản cũ còn trong Git history. Xem [addendum](AURA_POST_CONTEST_ADDENDUM_2026-10-23.md) và [trạng thái source hiện hành](../docs/CURRENT_PROJECT_STATUS.md).

Review source/Drive/UX/deploy ngày 26/10 nằm tại [assessment hiện hành](../docs/READINESS_AND_SCALE_ASSESSMENT_2026-10-23.md).
Default `master` cập nhật source/README và ghi chú mốc mới trong artifact; điều này
không phải xác nhận candidate đã publish hoặc đạt production.

| Hạng mục | File chốt |
|---|---|
| Slide 5 trang | `AURA_SLIDE.pptx` |
| Proposal tiếng Việt | `AURA_PROPOSAL_VI.md`, `.docx`, `.pdf` |
| Proposal tiếng Anh | `AURA_PROPOSAL_EN.md`, `.docx`, `.pdf` |
| Build Log một trang của toàn dự án | `AURA_BUILD_LOG.md`, `.docx`, `.pdf` |
| Danh sách liên kết nộp bài | `AURA_SUBMISSION_LINKS.md`, `.docx`, `.pdf` |
| Video demo chính thức | Lưu trên Team Drive; URL nằm trong `AURA_SUBMISSION_LINKS.*` |
| Phiếu phản hồi người dùng | `AURA_USER_FEEDBACK_FORM.docx`, `.pdf` |
| Đặc tả workflow | `AURA_WORKFLOW_SPEC.md` |
| Báo cáo sau feedback | `AURA_AFTER_FEEDBACK_REPORT.docx`, `.pdf` |
| Addendum hậu cuộc thi | `AURA_POST_CONTEST_ADDENDUM_2026-10-23.md` — không thay thế proposal/build log đã nộp |

Proposal và Build Log giữ số liệu nộp ngày 14/10 dưới ghi chú hậu cuộc thi. Trạng thái kiến trúc hiện hành, thay đổi candidate và các giới hạn mới phải đọc cùng `AURA_AFTER_FEEDBACK_REPORT.*`, `../docs/CURRENT_PROJECT_STATUS.md` và `../docs/AFTER_FEEDBACK_ARCHITECTURE_CHECKLIST.md`; không dùng số liệu lịch sử để suy ra candidate đã publish.

Ba file Build Log là ba định dạng của cùng một tài liệu toàn dự án, không phải các sprint log riêng. Phiên bản lịch sử được giữ trong Git history thay vì đặt cạnh bản chốt.

Tài liệu ôn tập cá nhân, kịch bản quay video, kịch bản pitching và Q&A của BGK được giữ ngoài repository, không nằm trong gói submission.

Video chính thức nằm trên Team Drive, không đưa vào Git. User evidence chỉ hoàn tất sau khi có ba người tham gia đã đồng ý, feedback nguyên văn được xác nhận, một thay đổi sản phẩm truy vết được và một tác động tiêu cực được ghi nhận. Không đưa API key, hóa đơn thật, raw provider payload, database, ảnh billing hoặc secret vào thư mục này.
