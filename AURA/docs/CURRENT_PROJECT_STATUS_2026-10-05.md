# AURA — Current project status

Cập nhật: 05/10/2026. Đây là nguồn trạng thái hiện hành; các báo cáo có ngày cũ hơn được giữ để truy vết lịch sử.

## 1. Kết luận điều hành

AURA đã đạt mức **MVP demo candidate** cho luồng cốt lõi: upload bất đồng bộ, Vision AI trích xuất, semantic validation/repair có giới hạn, policy C# quyết định, chuyển tiếp cho quản lý, audit và undo. Baseline code hiện tại là `master` tại `c42c5ef`; Release build sạch, 108/108 automated tests pass và EF không có model change chưa migration.

Deployment SmartASP sau bản vá đã có evidence ngày 04/10: health/DB/storage/migration pass, UI first-paint đúng tab, security-negative pass, AUTO smoke và FACT fail-safe smoke độc lập pass, Official Verify 5/5, human workflow/audit pass, persistence sau refresh/recycle pass và concurrent smoke hoàn tất 7/7 request (2 + 5). Đây là evidence staging/MVP, không phải chứng nhận production accuracy.

Chưa được ký **FINAL GO** vì còn bốn cổng do con người xác nhận:

1. blind holdout đúng 15 ca, khóa nhãn/hash trước request đầu tiên;
2. ba phiên người dùng thật với feedback do chính người dùng xác nhận;
3. mobile/4G và thao tác không nháy tab được quan sát thủ công;
4. ba dress rehearsal có bấm giờ trên cùng commit freeze.

Live URL được xác minh trong evidence ngày 04/10. Trước khi công bố trên README hoặc dùng làm đường demo chính, phải kiểm tra lại health và smoke vì khả dụng hiện thời có thể thay đổi độc lập với code.

## 2. Baseline kỹ thuật đã kiểm

| Hạng mục | Kết quả |
|---|---|
| Source revision | baseline `master` / `49071c4`; responsive/holdout update đang được xác minh trong working tree ngày 05/10 |
| Release build | 0 warning, 0 error |
| Automated tests | 108/108 pass, offline, không gọi API trả phí |
| EF model | Không có pending model change |
| Official Verify | 5/5, đúng 3 AUTO + 1 FACT + 1 POLICY |
| OpenRouter judge set | 15/15 decision; 70/75 field; P50/P95 3,614/16,459 giây |
| Ollama judge set | 14/15 decision; 73/75 field; P50/P95 52,238/58,318 giây; missed escalation TK-12 |
| Fallback isolated regression | 1/1; `FallbackUsed=true`; E2E 84,425 giây |
| Live postfix gate | PASS_WITH_PERFORMANCE_WARNING; 7/7 concurrent request hoàn tất |
| Live concurrent E2E P95 | 20,941 giây cho 2 request; 23,290 giây cho 5 request |

Các số trên thuộc những tập và revision đã nêu. Không diễn giải thành “accuracy thực tế 100%” hoặc “production-ready”.

## 3. Kiến trúc và authority

```text
JPG/JPEG/PNG + claimed amount
  -> kiểm extension/MIME/magic bytes/5 MB
  -> lưu ảnh riêng tư + SHA-256
  -> SQL PENDING + audit, trả HTTP 202/statusUrl
  -> worker claim bằng lease/reclaim/backoff
  -> OpenRouter Qwen3-VL-8B (Ollama 4B tùy chọn)
  -> schema + semantic validation + tối đa một repair
  -> PolicyDecisionEngine C# theo FACT > POLICY > AUTHORITY
  -> AUTO_APPROVE hoặc ESCALATE_*
  -> nhân viên chuyển tiếp
  -> quản lý YES/NO/UNDO
  -> audit timeline
```

AI không có quyền phê duyệt. Model chỉ tạo facts có cấu trúc; C# policy là authority. `claimedAmount` không được đưa vào semantic-repair prompt để tránh model bị dẫn theo đáp án người nộp.

## 4. Evidence live ngày 04/10

Nguồn: `D:\aura\demo_evidence\13_smarterasp_postfix_c42c5ef_2026-10-04\10_final_go_no_go\automated-gate-summary.json`.

- Release/test/migration: pass.
- Health trước và sau recycle: pass.
- Initial-tab rendering: pass.
- Upload/HTTP security-negative: pass.
- AUTO smoke: pass.
- FACT fail-safe smoke: pass.
- Official Verify: pass.
- Human workflow: pass.
- Persistence sau recycle: pass.
- Concurrency: pass 2/2 và 5/5, có performance warning vì accepted clock bao gồm GET antiforgery, tạo job và network, không phải SLO POST thuần.
- Mobile: responsive UI đã được chỉnh ở source ngày 05/10 (header 3 tab, form một cột, bảng dạng card, touch target và input nhân viên). Browser local xác nhận ở 360/390/1440 px không có horizontal page overflow; Audit 148 dòng render dạng card ở 360 px. Còn phải xác nhận trên điện thoại thật/4G sau publish.

Các bằng chứng thủ công còn phải bổ sung: ảnh/timestamp recycle Pool, điện thoại thật qua 4G/5G, quan sát không còn nháy Upload sau YES/NO/UNDO, holdout, ba user session và ba rehearsal.

## 5. Holdout hiện tại

`D:\aura\demo_evidence\09_blind_holdout_2026-10-04\02_selected_images` hiện chứa 12 file; inventory cũ không còn khớp và phải tạo lại sau khi chốt redaction/tên file. Tập này đang là **candidate, chưa locked**.

- Người dùng xác nhận `NewVinamilk.jpg` và `NewKatinat.jpg` là giao dịch mới, khác dữ liệu regression cũ. Hai file được giữ làm candidate nếu chính file/giao dịch này chưa từng gửi AURA/OpenRouter/Ollama và chưa dùng để tune; trùng merchant không tự làm mất tính blind.
- Đối chiếu SHA-256 ngày 05/10 không tìm thấy file byte-identical của 12 candidate trong các thư mục evidence cũ. Kết quả này không phát hiện được ảnh cùng giao dịch đã crop/che QR/nén lại, nên vẫn cần xác nhận provenance của con người.
- Những ảnh còn lại chỉ hợp lệ sau khi có quyền sử dụng, redaction review và ground truth độc lập.

Không chạy official holdout cho tới khi đúng 15 ảnh eligible, tên file ổn định và manifest đã khóa. Xem [`HOLDOUT_LOCKING_GUIDE_2026-10-05.md`](HOLDOUT_LOCKING_GUIDE_2026-10-05.md) và [`HOLDOUT_MATRIX_15_CASES_2026-10-05.md`](HOLDOUT_MATRIX_15_CASES_2026-10-05.md).

## 6. Thứ tự công việc tiếp theo

1. Phân loại 12 candidate theo BH-01…BH-15; loại mọi ảnh đã dùng để tune/test và mọi ảnh thiếu quyền sử dụng.
2. Thu thập đủ 15 ảnh eligible, ẩn danh xong mới tính SHA-256.
3. Hai người review claimed amount, expected status, expected facts và rationale; xử lý bất đồng trước khi chạy.
4. Khóa manifest, manifest hash, commit/policy version và thời điểm khóa.
5. Chạy OpenRouter đúng một official run, fallback tắt; giữ cả mismatch và system error.
6. Thực hiện ba user session; triển khai một cải tiến nhỏ truy vết được nếu feedback chỉ ra blocker.
7. Hoàn tất mobile/4G/no-flash evidence và ba rehearsal.
8. Khi toàn bộ gate pass: cập nhật slide/build log/video, ký go/no-go, rồi mới public live URL.

## 7. Giới hạn công bố

AURA hiện hỗ trợ một ảnh JPG/JPEG/PNG tối đa 5 MB. PDF/nhiều trang, authentication/RBAC thực, malware scan, object storage, retention/deletion, backup/restore, distributed queue/circuit và production monitoring là backlog sau cổng Chung kết. Không mô tả chúng là đã triển khai.
