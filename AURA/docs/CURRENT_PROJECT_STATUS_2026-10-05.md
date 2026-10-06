# AURA — Current project status

Cập nhật: 06/10/2026. Đây là nguồn trạng thái hiện hành; các báo cáo có ngày cũ hơn được giữ để truy vết lịch sử.

## 1. Kết luận điều hành

AURA vẫn là **MVP demo candidate có điều kiện**, chưa phải production-ready hay accuracy-ready. Official blind holdout thật đã hoàn tất đúng một lượt và lộ một fail-open nghiêm trọng cùng các lỗi semantic bị che. Source hậu holdout đã bổ sung evidence contract v2, fail-safe dòng tổng bị cắt, raw-date verification, chuẩn hóa giờ có giây/AM-PM, money-role/identifier guard và taxonomy personal-item; Release checkpoint đạt 120/120 automated tests. Chưa được publish nên live `c42c5ef` chưa có các bảo vệ này.

Deployment SmartASP ngày 04/10 vẫn chứng minh health/DB/storage/migration, UI first-paint, security-negative, AUTO/FACT smoke, Verify 5/5, workflow/audit, persistence và concurrency của baseline cũ. Evidence đó không chứng minh contract v2; sau publish bắt buộc chạy live regression riêng.

Chưa được ký **FINAL GO**. Các cổng còn mở là:

1. publish đúng commit hậu holdout và pass health + live contract-v2 regression (đặc biệt total bị rách phải FACT);
2. ba phiên người dùng thật có giám sát với feedback do chính người dùng xác nhận;
3. mobile/4G và thao tác không nháy tab được quan sát thủ công;
4. ba dress rehearsal có bấm giờ trên cùng commit freeze.

Live URL được xác minh trong evidence ngày 04/10. Trước khi công bố trên README hoặc dùng làm đường demo chính, phải kiểm tra lại health và smoke vì khả dụng hiện thời có thể thay đổi độc lập với code.

## 2. Baseline kỹ thuật đã kiểm

| Hạng mục | Kết quả |
|---|---|
| Source revision | working candidate hậu holdout; SHA chốt sau commit/push |
| Release build | 0 warning, 0 error |
| Automated tests | 120/120 pass, offline, không gọi API trả phí |
| EF model | Không có pending model change |
| Official Verify | 5/5, đúng 3 AUTO + 1 FACT + 1 POLICY |
| OpenRouter judge set | 15/15 decision; 70/75 field; P50/P95 3,614/16,459 giây |
| Ollama judge set | 14/15 decision; 73/75 field; P50/P95 52,238/58,318 giây; missed escalation TK-12 |
| Fallback isolated regression | 1/1; `FallbackUsed=true`; E2E 84,425 giây |
| Live postfix gate | PASS_WITH_PERFORMANCE_WARNING; 7/7 concurrent request hoàn tất |
| Live concurrent E2E P95 | 20,941 giây cho 2 request; 23,290 giây cho 5 request |
| Official real holdout raw | 10/15 decision; missed 1/9; over-escalation 3/6; system error 0; fallback 0 |
| Holdout adjudicated view | 11/15 sau khi xác nhận BH-04 ground-truth sai; missed 1/10; over 2/5; raw không đổi |
| Holdout field metric | 28/30 chỉ đo currency + totalAmount; không phải full extraction accuracy |

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

## 5. Official holdout và kết luận

Manifest 15 ca đã được khóa trước request đầu tiên, hash manifest `B13EB77621A89DCFABC3E09F27810F9E7921F5E15433F03E3F9250115BA9CDE9`. OpenRouter chạy đúng một official run, fallback tắt; raw evidence phải giữ bất biến. Evidence manifest cuối có SHA-256 `FDD4B86B5B7D57E9DB0E4E44D410ED0321FD1D300E0DA5FFC0621973FD0C7035`.

- Raw: 10/15 decision đúng (66,67%), 1 missed escalation, 3 over-escalation, 0 system error.
- Post-hoc adjudication: BH-04 GS25 được khóa `AUTO_APPROVE` nhưng ảnh in 04/10/2026 là Chủ nhật, nên quyết định FACT của hệ thống đúng. Adjusted view là 11/15 (73,33%), missed 1/10 và over 2/5; đây chỉ là phân tích, không thay raw.
- Critical BH-07 Circle K: giá trị dòng Total bị rách nhưng model suy/copy 21.000 từ subtotal/items và hệ thống AUTO. Đây là blocker fail-open.
- BH-06 Ministop: `09:46:21` bị parser HH:mm từ chối, FACT che POLICY bia. BH-08 Katinat: `04-10-26` bị đổi thành 2026-04-10. BH-01 money roles cash/change/VAT bị gán sai. BH-15 PTT/Mã CQT bị lặp identifier.

Vì vậy accuracy claim và unattended real-user pilot là **NO-GO**. Bộ 15 ảnh không còn là blind sau official run; chỉ được gọi là post-holdout regression. Một blind claim mới cần receipts unseen mới.

## 6. Thứ tự công việc tiếp theo

1. Chốt commit/push hậu holdout sau build/test/EF/package gates.
2. Publish đúng SHA: recycle trước → Visual Studio Web Deploy Release → `Publish succeeded` → recycle sau → health OK.
3. Chạy live contract-v2 smoke và post-holdout regression; không đổi tên thành blind. Circle K phải FACT, Ministop phải POLICY, ngày/money/id phải đúng nguyên nhân.
4. Nếu live regression pass, thực hiện ba user session có giám sát; chưa cho vận hành unattended.
5. Hoàn tất mobile/4G/no-flash evidence và ba rehearsal.
6. Đồng bộ slide/build log/video bằng số holdout trung thực; chỉ public live URL sau final go/no-go.

## 7. Giới hạn công bố

AURA hiện hỗ trợ một ảnh JPG/JPEG/PNG tối đa 5 MB. PDF/nhiều trang, authentication/RBAC thực, malware scan, object storage, retention/deletion, backup/restore, distributed queue/circuit và production monitoring là backlog sau cổng Chung kết. Không mô tả chúng là đã triển khai.
