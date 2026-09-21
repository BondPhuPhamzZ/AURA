# Trạng thái chuẩn bị nộp Sprint 1

Ngày cập nhật: 21/09/2026. Chuẩn đối chiếu: Track A trong `Challenge_Brief_OrganizationAI_VN.docx.pdf`.

## Hạng mục kỹ thuật Track A

| Yêu cầu | Trạng thái | Bằng chứng |
|---|---|---|
| Quy trình và policy rõ | Đạt cơ bản | `BUSINESS_RULES.md`, policy engine tất định |
| Tối thiểu 15 tình huống | Đạt | 33 automated policy/workflow tests; ma trận mở rộng trong `TEST_CASES.md` |
| Ba nhóm FACT/POLICY/AUTHORITY | Đạt | status và precedence trong `PolicyDecisionEngine` |
| Câu hỏi chuyển tiếp cụ thể | Đạt | câu hỏi tiếng Việt có dữ kiện và lựa chọn CÓ/KHÔNG |
| Không over-escalate | Đạt trên fixture nội bộ | 3/3 routine cases auto-approve |
| Không khẳng định input nghi vấn | Đạt | FACT có ưu tiên cao nhất; lỗi AI -> SYSTEM_ERROR/manual |
| Verify một nút 3 auto + 2 escalate | Đạt local | benchmark Gemini 5/5 ngày 20/09/2026, khoảng 32 giây tổng |
| Input mới | Đạt local | upload JPG/PNG + claimed amount dùng cùng production path |
| Audit, human override, undo | Đạt local | Nhân viên chuyển tiếp; quản lý Có/Không; DB audit và undo |
| Lưu chứng từ để tra cứu | Đạt local | private storage + metadata + SHA-256 + route download |
| Live URL public | **Chưa đạt** | Chưa có URL deploy được ghi nhận |

## Sáu sản phẩm phải chuẩn bị

1. **Live URL:** chưa có; phải deploy và kiểm thử từ trình duyệt/thiết bị không đăng nhập.
2. **Verify harness + bảng test + runbook:** code và tài liệu đã có; cần ghi lại một run từ live URL.
3. **Public repository:** `https://github.com/BondPhuPhamzZ/AURA`; phải push toàn bộ commit, không squash/force-push.
4. **Video demo tối đa 3 phút:** chưa quay. Phải thể hiện live URL, Verify 5 ca, upload mới, manager decision và audit/undo; giữ lại cả giới hạn thực tế.
5. **Đúng 5 slide:** chưa xác minh artifact cuối. Cấu trúc: vấn đề; input-xử lý-output/HITL; before-after + phương pháp đo; kiến trúc + thật/giả; giới hạn/rủi ro.
6. **Build log một trang:** đã có bản nháp `BUILD_LOG.md`; cần xác nhận thời gian thật và quyết định cắt giảm trước khi xuất bản.

## Blocker trước khi bấm nộp

- Chọn dịch vụ deploy có SQL Server và persistent disk/volume, cấu hình secret Gemini.
- Chạy migration trên database deploy.
- Xác minh 5/5 trên live URL dưới 90 giây và không chạm rate limit.
- Điền live URL vào README, slide, video description và form nộp.
- Hoàn thành đúng 5 slide và video dưới 3 phút.
- Push commit và xác minh repository public từ cửa sổ ẩn danh.
