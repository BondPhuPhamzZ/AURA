# AURA — Gói nộp Drive Chung kết

Cập nhật ngày 13/10/2026. Tài liệu này đối chiếu đề Track OrganizationAI với thông báo mới nhất của BTC về việc đưa slide, proposal, GitHub, video demo và tài liệu liên quan vào Drive của Team.

## 1. Kết luận về video

- Hạng mục bắt buộc là **một video demo chính thức, tối đa 3 phút**.
- Chỉ cần quay **một nội dung chính**. Xuất thêm một bản sao MP4 dung lượng thấp để mang theo USB/laptop là dự phòng kỹ thuật, không phải video nộp thứ hai.
- Không nộp `D:\aura\vid\final.mkv`: bản này dài khoảng 6 phút 43 giây và thuộc baseline cũ.
- Không cần quay video riêng cho kiến trúc, Verify, evidence overlay hay benchmark. Các nội dung cốt yếu được gom trong cùng video product-only 2:45–2:58.
- Evidence overlay hiện là prototype synthetic cô lập; không đưa vào video chính thức như tính năng production.

### Kịch bản video mục tiêu 2 phút 50 giây

| Thời gian | Màn hình | Lời nói và bằng chứng |
|---:|---|---|
| 0:00–0:12 | Thanh địa chỉ Live URL và trang chủ | Nêu một câu: AURA là Escalation Referee cho hoàn ứng; AI đọc dữ kiện, policy C# quyết định, con người xử lý ngoại lệ. |
| 0:12–1:22 | Bấm Verify đúng một lần | Để bảng chạy đủ 5 ca. Trong lúc chờ, giải thích 3 AUTO + 2 ESCALATE, output có timestamp, không mock PASS và mục tiêu dưới 90 giây. Không chạy upload song song. |
| 1:22–1:35 | Bảng Verify hoàn tất | Chỉ vào 5/5, tổng thời gian và không có system/provider error. Không gọi 163 test là 163 hóa đơn. |
| 1:35–2:18 | Một hồ sơ synthetic ESCALATE đã chuẩn bị sẵn | Chỉ facts/reason, nhân viên bấm Chuyển tiếp, quản lý Đồng ý/Từ chối, mở Audit và Undo. Đây là thao tác live trên hệ thống, không cần chờ thêm một lượt model. |
| 2:08–2:28 | Audit và Undo trong UI | Chỉ timeline của AI decision, forward, manager action và Undo; không mở slide. |
| 2:28–2:42 | Facts, provider và trạng thái ngay trong hồ sơ | Nói Qwen3.8 Flash/Alibaba chỉ extraction; durable SQL queue giữ job; policy và audit không phụ thuộc model. |
| 2:42–2:58 | Giữ AURA trên màn hình | Công khai giới hạn một ảnh JPG/PNG tối đa 5 MB, chưa có auth/RBAC production, PDF nhiều trang/object storage và evidence bounding box production. |

Quay một take nếu có thể. Không cắt để làm latency ngắn hơn thực tế. Không hiện API key, Pool Manager, billing, connection string, receipt cá nhân thật hoặc thư mục evidence riêng tư.

## 2. Sáu hạng mục chính thức theo đề Track

| # | Hạng mục | Nội dung AURA cần cung cấp | Trạng thái 13/10 |
|---:|---|---|---|
| 1 | Live URL | URL công khai, không login/cài đặt; trang chủ có hướng dẫn thao tác đầu tiên | Sẵn sàng kỹ thuật: `https://bondphupham-001-site1.ltempurl.com/`; vẫn cần rehearsal cuối |
| 2 | Verify harness + test cases + runbook | Một nút chạy 5 ca Spec A, bảng expected/actual/pass-fail/timestamp; runbook clone sạch đến lúc chạy | Sẵn sàng: 5/5 trong 65,004 giây và `docs/RUNBOOK.md` |
| 3 | Public repository | GitHub public, đầy đủ commit; không squash/force-push | Sẵn sàng: `https://github.com/BondPhuPhamzZ/AURA`; ref bàn giao là tag `aura-demo-day-2026-10-14`, SHA được ghi tự động vào `PACKAGE_STATUS.json` sau khi push |
| 4 | Video demo | Một video MP4 tối đa 3 phút, cho thấy hệ thống chạy thật và giới hạn | Chưa sẵn sàng; phải quay video mới |
| 5 | Đúng 5 slide | Đúng cấu trúc cố định, không thêm slide | Deck V2 đã đồng bộ 163/163 và câu chuyện phát triển; giữ đúng 5 slide |
| 6 | Build Log một trang | Công cụ AI, cách dùng, phần hiệu quả, chi phí/thời gian phát sinh, tính năng lớn nhất bị cắt và lý do | DOCX/PDF một trang đã đồng bộ; phần cắt lớn nhất là production evidence localization |

Thông báo mới của BTC còn yêu cầu **proposal** và các tài liệu liên quan. Proposal hiện tại trong `D:\aura\important_Docs` là bài nghỉ phép HR cũ, vì vậy không được upload nguyên trạng.

## 3. Cấu trúc Drive nên dùng

```text
AURA_FINAL_SUBMISSION/
  00_README_FIRST/
    AURA_SUBMISSION_LINKS.pdf
  01_SLIDES/
    AURA_5_SLIDES_FINAL.pptx
  02_PROPOSAL/
    AURA_PROPOSAL_VI_FINAL.pdf
    AURA_PROPOSAL_EN_FINAL.pdf
  03_VIDEO/
    AURA_DEMO_OFFICIAL_2M50S.mp4
  04_BUILD_LOG/
    AURA_BUILD_LOG_1_PAGE.docx
    AURA_BUILD_LOG_1_PAGE.pdf
  05_RELATED_DOCUMENTS/
    AURA_BUSINESS_RULES.pdf
    AURA_WORKFLOW_SPEC.pdf
    AURA_RUNBOOK.pdf
    AURA_CURRENT_VERIFIED_STATUS.pdf
    AURA_USER_FEEDBACK_FORM_BLANK.pdf
    AURA_USER_FEEDBACK_EVIDENCE.pdf   # chỉ thêm sau khi đủ ba session
  06_VERIFY_PACKAGE/
    AURA_VERIFY_CASES_EXPECTED.pdf
```

Không nén source thành ZIP nếu Drive không yêu cầu vì GitHub public đã là nguồn chính. Không upload API key, publish profile, raw evidence, ảnh hóa đơn thật, database, log provider, dashboard billing hoặc user-secrets.

## 4. Nội dung file AURA_SUBMISSION_LINKS

File đầu tiên trong Drive nên có đúng các trường sau:

1. Tên dự án: AURA — Automated Underwriting & Reimbursement AI.
2. Đội/Track: The Deciders — OrganizationAI, Spec A The Escalation Referee.
3. Thành viên: Pham Gia Phu.
4. Live URL: `https://bondphupham-001-site1.ltempurl.com/`.
5. Health URL: `https://bondphupham-001-site1.ltempurl.com/healthz`.
6. Public GitHub: `https://github.com/BondPhuPhamzZ/AURA`.
7. Frozen commit SHA sau cùng.
8. Link video trong cùng Drive.
9. Hướng dẫn nhanh: mở Live URL → bấm Verify Harness → xem bảng 3 AUTO + 2 ESCALATE → mở một hồ sơ ESCALATE → Chuyển tiếp → Quản lý → Audit/Undo.
10. Claim boundary: 163/163 là automated tests; 8/10 là prospective mini-holdout với 0 missed và 2 over; 11/12 là stability repeat trên dữ liệu đã biết, không phải holdout mới.

Sau commit chốt, cập nhật SHA trong cả bản Markdown, DOCX/PDF và bản Drive. Không điền trước một SHA dự kiến.

## 5. Proposal AURA mới phải có gì

Proposal nên mô tả đúng AURA hoàn ứng, không tái sử dụng nội dung nghỉ phép HR cũ:

1. Bài toán hiện trạng và tác hại của missed-escalation/over-escalation.
2. Phạm vi nghiệp vụ và `BUSINESS_RULES`.
3. Luồng upload → queue → extraction → validation/repair → policy → human decision → audit/undo.
4. Hai điểm con người quyết định và trách nhiệm giải trình.
5. Kiến trúc triển khai thực tế; phân biệt phần thật, synthetic và roadmap.
6. Phương pháp đo: Verify, automated tests, prospective holdout, post-holdout regression và concurrency.
7. Kết quả có claim boundary: 163/163 tests; Verify 5/5 trong 65,004 giây; prospective 8/10, 0 missed, 2 over; sequential known-data 11/12 chỉ đo stability.
8. Phân tích hai over-escalation và lý do ưu tiên fail-safe.
9. Bằng chứng ba người dùng thật, chức danh, trích dẫn nguyên văn, cải tiến từ feedback và một tác động tiêu cực.
10. Privacy, consent, redaction, giới hạn và roadmap evidence localization.

## 6. Gate ba người dùng thực tế

Đây là mục có trọng số 20 điểm và xuất hiện trong compliance check. Chỉ đóng gate khi có đủ:

- ba người trực tiếp xử lý hoàn ứng/kế toán/quản lý trong thực tế;
- họ tên và chức danh thật, với đồng ý sử dụng;
- trích dẫn nguyên văn do từng người cung cấp;
- ít nhất một thay đổi trong sản phẩm truy được bằng commit hoặc ảnh trước/sau;
- ít nhất một bất cập/tác động tiêu cực cụ thể như thời gian chờ AI, tải review dồn cục, nguy cơ ỷ lại hoặc thêm bước xác nhận.

Không dùng chính chủ project thay cho cả ba người, không dựng persona và không viết hộ phản hồi.

## 7. Kiểm tra ngay trước khi upload

1. Xác nhận deadline trực tiếp trong Drive/email. Đề Track ghi 15/10, ghi nhận nội bộ trước đó là 14/10; dùng 14/10 làm mốc an toàn.
2. Kiểm Live URL và `/healthz` bằng mạng khác/ẩn danh.
3. Chạy đúng một rehearsal Verify; không chạy đồng thời upload.
4. Mở PPTX và PDF, xác nhận đúng 5 slide.
5. Xác nhận video ≤ 180 giây, nghe rõ, không lộ bí mật/PII và có nêu giới hạn.
6. Mở Build Log, xác nhận đúng một trang và số test 163/163.
7. Mở proposal và tìm toàn văn các từ `nghỉ phép`, `BHXH`, `C65-HD`; bản AURA final không được còn nội dung từ proposal HR cũ.
8. Xác nhận GitHub public và frozen SHA khớp file `AURA_SUBMISSION_LINKS`.
9. Đặt quyền Drive để người có link xem được; thử từ trình duyệt ẩn danh.
10. Mang offline một bản sao video MP4, deck PPTX/PDF, proposal PDF và Build Log PDF trên laptop/USB.

Nếu chưa có PDF của slide, PowerPoint vẫn là hạng mục bắt buộc theo đề; có thể xuất PDF thủ công từ chính deck đã kiểm tra để làm bản dự phòng, không thay PPTX chính thức.
