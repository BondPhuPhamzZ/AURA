# AURA - Live deployment evidence audit

Cập nhật: 04/10/2026. Phạm vi kiểm tra là `12_smarterasp_final_2026-10-03`, live URL SmartASP và cấu trúc sơ bộ của `09_blind_holdout_2026-10-04`. Ảnh hóa đơn thật vẫn ở ngoài Git.

## 1. Kết luận ngắn

Live deployment đang hoạt động: health HTTP 200/status `ok`, database đã cập nhật, storage khả dụng, OpenRouter được cấu hình và fallback tắt. Official Verify đạt 5/5 với đúng 3 `AUTO_APPROVE`, 1 `ESCALATE_FACT`, 1 `ESCALATE_POLICY`. Manager YES/NO/UNDO và audit có bằng chứng tốt.

Tuy nhiên **chưa ký FINAL GO**. Bốn khoảng trống còn lại là: smoke AUTO độc lập, smoke fail-safe FACT độc lập, bằng chứng mobile/recycle thật và chạy blind holdout đã khóa. Ba phiên người dùng thật vẫn là gate riêng theo brief/seminar.

## 2. Audit từng folder live

| Folder | Đánh giá | Kết luận/evidence còn thiếu |
|---|---|---|
| `00_release_metadata` | PASS | Commit `8ebe18c8...`, branch, clean status, DLL hash, Release build, 102/102 test và EF model check được ghi lại rõ. Đây là revision đang chạy trước bản vá UX ngày 04/10. |
| `01_health_and_config` | PASS | Health HTTP 200, `status=ok`, DB/storage/migration/provider đúng; ảnh cho thấy 22 biến tồn tại mà không in giá trị. |
| `02_ui_desktop_mobile` | PARTIAL | Desktop đủ rõ. Sáu ảnh chưa chứng minh viewport điện thoại thật; cần thêm một ảnh khoảng 390x844 cho Upload và một ảnh cho Manager/Audit. |
| `03_security_negative` | PASS cho nhóm cốt lõi | Có 404 static policy, 404 fake status, HTTP->HTTPS 307, HSTS, antiforgery 400. Ngày 04/10 bổ sung `upload-validation-negative.json`: JPEG giả chữ ký và JPEG 5 MB + 1 byte đều HTTP 400, bị chặn trước storage/DB/queue/AI. |
| `04_auto_smoke` | FAIL nhãn evidence | File thực tế là `TK-05-policy-beer.jpg`; final JSON là `ESCALATE_POLICY`, không phải AUTO. Không được dùng folder này để claim AUTO smoke. |
| `05_fail_safe_smoke` | FAIL tính độc lập | Toàn bộ file byte-identical với `04_auto_smoke`, cùng case ID và cùng `ESCALATE_POLICY`. Đây không phải một lượt fail-safe riêng và cũng không chứng minh FACT fail-safe. |
| `06_verify_5cases` | PASS | 5/5; latency lần lượt 4,693 / 24,165 / 7,861 / 7,494 / 11,527 ms; max 24,165 ms; đúng split 3/2; OpenRouter, fallback false. |
| `07_human_workflow_audit` | PASS có điều kiện | Forward, YES, NO, UNDO, quyết định lại và timeline đều rõ. Hiện tượng nháy Upload đã xác định là first-paint UI, không phải mất DB; bản vá server-render tab đúng đã có regression 108/108 nhưng cần deploy lại và chụp một lượt xác nhận. |
| `08_refresh_recycle_persistence` | PARTIAL | Chứng minh refresh/resume giữ PENDING rồi trả final, ảnh và audit còn tồn tại. Chưa có ảnh/timestamp thao tác recycle Pool SmartASP nên chưa được gọi là recycle evidence. |
| `09_concurrency_optional` | PENDING/optional | Không dùng multi-select một form. Cần 2 hoặc 5 HTTP client độc lập gửi gần đồng thời bằng `tools/Invoke-ConcurrentUploadSmoke.ps1`, dùng fixture synthetic và ghi Completed, Accepted P95, End-to-end P95. Chạy sau khi deploy commit candidate. |
| `10_external_network` | PARTIAL PASS | `codex-independent-http-smoke.json` chứng minh client HTTP độc lập gọi health ba lần đều 200/ok và trang chủ 200. Vẫn nên có ảnh điện thoại dùng 4G/5G, không dùng Wi-Fi cùng máy chủ. |
| `11_final_go_no_go` | CHƯA ĐƯỢC ĐIỀN | Chỉ ký sau khi các dòng bắt buộc bên trên pass trên cùng commit candidate. |

Không dùng cùng một request/file evidence để chứng minh hai hành vi khác nhau. Có thể tái sử dụng ảnh fixture, nhưng mỗi test phải có request ID, timestamp, expected purpose và final JSON riêng.

## 3. Nguyên nhân và bản vá lỗi nháy tab

Trước bản vá, HTML luôn render Upload và nút Applicant ở trạng thái active. JavaScript chỉ đọc `?tab=reviewer` hoặc `?tab=audit` ở cuối trang, sau first paint mới ẩn Upload và hiện tab đích. Vì vậy người dùng thấy Upload nháy trong một khoảnh khắc sau manager action.

Bản vá ngày 04/10:

- `HomeController` chuẩn hóa tab chỉ trong allow-list `applicant/reviewer/audit`;
- layout và dashboard render đúng active tab/display ngay từ response đầu tiên;
- full navigation vẫn được giữ để bảng workflow được dựng lại từ committed database state;
- thêm sáu regression case; Release build sạch và 108/108 automated test pass.

Sau khi deploy commit mới, chỉ cần recheck targeted: manager YES/NO, UNDO và mở trực tiếp `/?tab=reviewer`, `/?tab=audit`; không cần tiêu quota chạy lại toàn bộ logic AI chỉ vì thay đổi này.

## 4. Blind holdout hiện tại

Inventory sơ bộ ghi nhận 8 ảnh và SHA-256 tại `01_candidates_private/preliminary-hash-inventory-8-of-15.json`. File này có trạng thái `PRELIMINARY_NOT_LOCKED`: nó không thay thế consent, redaction review hoặc ground truth.

Không thể coi đủ 8/15 một cách máy móc:

- `Vinamilk.jpg` đã được dùng nhiều lần để phát triển/kiểm tra discount và line-item guard, vì vậy phải loại khỏi blind holdout và chỉ giữ ở development regression;
- người kiểm thử đã mô tả Katinat là smoke input. Nếu ảnh này từng được gửi provider, cũng phải loại khỏi blind holdout. Evidence live hiện lại chứa `TK-05-policy-beer.jpg`, nên phải đối chiếu lịch sử trước khi quyết định;
- sáu ảnh còn lại mới chỉ là candidate cho tới khi xác nhận quyền sử dụng, ẩn danh và ground truth trước lần gọi model đầu tiên.

Do đó cần thêm **ít nhất 9 ảnh unseen** nếu loại cả Vinamilk và Katinat; cần 8 nếu xác minh được Katinat chưa từng chạy. Không gửi bất kỳ candidate nào cho AURA/OpenRouter trước khi manifest được khóa.

Cấu trúc local khuyến nghị:

```text
09_blind_holdout_2026-10-04/
  00_consent_private/          # quyền sử dụng, không public
  01_candidates_private/       # inventory/hash sơ bộ
  02_selected_images/          # đúng 15 ảnh ẩn danh sau review
  03_ground_truth_locked/      # manifest.json + manifest.sha256.txt
  04_preflight/                # health/config/commit trước run
  05_openrouter_official_run/  # metadata/results/summary, đúng một lượt
  06_metrics/                  # confusion matrix và metric đã kiểm
  07_mismatch_review/          # giữ nguyên mọi mismatch
```

Sao chép `test_kit/holdout-manifest.template.json` ra folder local, không sửa template trong Git thành dữ liệu thật. Mỗi ca phải có claimed amount, expected status/facts, rationale, SHA-256, consent reference, redaction status và xác nhận chưa dùng để tune. Hai người review nhãn khi có thể. `Invoke-ExtendedDatasetEvaluation.ps1` hiện xác minh toàn bộ SHA-256 trước request đầu tiên và ghi hash vào results/metadata.

## 5. Thứ tự đóng các gate

1. Commit/push và deploy bản vá tab; cập nhật release metadata theo commit mới.
2. Targeted UX smoke: reviewer/audit không nháy Upload.
3. Tạo một AUTO smoke độc lập và một FACT fail-safe độc lập; lưu pending/final JSON, request ID, ảnh UI và expected rationale.
4. Bổ sung hai screenshot phone viewport và một recycle Pool thật với timestamp trước/sau, health, status/receipt/audit.
5. Nếu cần claim live concurrency, chạy 2 trước rồi 5 synthetic request bằng runner; đây là nhiều client đồng thời, không phải chọn nhiều ảnh trong form.
6. Hoàn tất 15 ảnh unseen, consent/redaction và khóa manifest/hash; chạy OpenRouter đúng một lượt, fallback false.
7. Thực hiện ba user sessions, chọn một cải tiến nhỏ truy vết được từ feedback, rồi regression.
8. Ba dress rehearsal trên commit freeze; lúc đó mới quay video cuối, điền `11_final_go_no_go` và public live URL trong README.

GPU BTC là POC sau các gate trên, không phải blocker của MVP. Chuẩn bị trước Docker/runtime, model+quantization, prompt/schema, synthetic manifest, lệnh đo 1/2/5 concurrency, VRAM/P50/P95 và yêu cầu TLS/auth/private network; không đưa receipt thật lên GPU mượn ở lượt đầu.

## 6. Nội dung báo cáo tiến độ ngắn

> Ngày 04/10, em đã hoàn tất smoke deployment SmartASP: health HTTP 200/status ok, database không còn pending migration, storage và OpenRouter sẵn sàng, fallback tắt. Official Verify đạt 5/5 đúng 3 auto/2 escalate; manager YES/NO/UNDO và audit hoạt động. Security-negative bổ sung xác nhận ảnh giả chữ ký và file vượt 5 MB đều bị chặn HTTP 400 trước storage/queue/AI; client mạng độc lập gọi health ba lần đều thành công. Em cũng xác định lỗi nháy Upload khi chuyển Manager/Audit là first-paint UI và đã sửa server-render tab, build sạch, 108/108 regression pass. Qua audit evidence, hai folder AUTO/fail-safe cũ thực chất là cùng một POLICY request nên em chưa dùng chúng để claim hai gate riêng. Việc tiếp theo là deploy commit mới, bổ sung AUTO + FACT smoke độc lập, mobile/recycle evidence, khóa blind holdout 15 ca và tổ chức ba phiên người dùng thật trước khi ký final go/no-go và public live URL.
