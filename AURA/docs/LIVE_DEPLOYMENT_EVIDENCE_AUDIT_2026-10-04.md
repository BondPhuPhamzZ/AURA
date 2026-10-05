# AURA - Live deployment evidence audit

Cập nhật: 05/10/2026. Phạm vi hiện hành là `13_smarterasp_postfix_c42c5ef_2026-10-04`; phần audit `12_smarterasp_final_2026-10-03` bên dưới được giữ làm lịch sử trước bản vá. Ảnh hóa đơn thật vẫn ở ngoài Git.

## 1. Kết luận ngắn

Live deployment trên commit `c42c5ef` đã đạt automated technical gate với cảnh báo hiệu năng: health/DB/storage/migration pass, first-paint tab đúng, security-negative pass, AUTO và FACT fail-safe smoke độc lập pass, Official Verify 5/5, human workflow/audit pass, persistence sau recycle pass và concurrency hoàn tất 2/2 + 5/5 request.

Tuy nhiên **chưa ký FINAL GO**. Còn bằng chứng điện thoại thật/4G và xác nhận không nháy tab bằng mắt, blind holdout 15 ca đã khóa, ba phiên người dùng thật và ba dress rehearsal trên commit freeze. Live URL chỉ được công bố sau lần health/smoke cuối vì availability có thể thay đổi ngoài source code.

## 2. Evidence postfix trên `c42c5ef`

Nguồn máy đọc: `13_smarterasp_postfix_c42c5ef_2026-10-04/10_final_go_no_go/automated-gate-summary.json`.

| Gate | Kết quả | Ghi chú |
|---|---|---|
| Release/build/test/EF | PASS | Build sạch, 108/108 test, EF model clean |
| Health trước/sau | PASS | Hai checkpoint health hợp lệ |
| Initial tab rendering | PASS | Bản vá không render Upload trước tab đích |
| Upload + HTTP security | PASS | File giả signature/quá 5 MB và endpoint âm bị chặn |
| AUTO smoke | PASS | Request/evidence độc lập |
| FACT fail-safe smoke | PASS | Request/evidence độc lập |
| Official Verify | PASS | Đúng 3 AUTO + 1 FACT + 1 POLICY |
| Human workflow | PASS | Forward, YES/NO/UNDO và audit |
| Post-recycle persistence | PASS | Status/ảnh/audit còn |
| Concurrency | PASS có cảnh báo | 2/2 E2E P95 20,941 s; 5/5 E2E P95 23,290 s. Accepted clock gồm antiforgery GET, setup và network, không phải POST-only SLO |
| Mobile | PARTIAL | Core render; navigation chưa vừa 390 px, Audit còn cuộn ngang |

Các mục thủ công còn thiếu: ảnh/timestamp recycle Pool, điện thoại thật trên mạng ngoài, video/click xác nhận không nháy Upload sau YES/NO/UNDO, holdout, user sessions và rehearsals.

## 3. Audit lịch sử của folder 12

### 3.1 Audit từng folder live

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

## 4. Nguyên nhân và bản vá lỗi nháy tab

Trước bản vá, HTML luôn render Upload và nút Applicant ở trạng thái active. JavaScript chỉ đọc `?tab=reviewer` hoặc `?tab=audit` ở cuối trang, sau first paint mới ẩn Upload và hiện tab đích. Vì vậy người dùng thấy Upload nháy trong một khoảnh khắc sau manager action.

Bản vá ngày 04/10:

- `HomeController` chuẩn hóa tab chỉ trong allow-list `applicant/reviewer/audit`;
- layout và dashboard render đúng active tab/display ngay từ response đầu tiên;
- full navigation vẫn được giữ để bảng workflow được dựng lại từ committed database state;
- thêm sáu regression case; Release build sạch và 108/108 automated test pass.

Sau khi deploy commit mới, chỉ cần recheck targeted: manager YES/NO, UNDO và mở trực tiếp `/?tab=reviewer`, `/?tab=audit`; không cần tiêu quota chạy lại toàn bộ logic AI chỉ vì thay đổi này.

## 5. Blind holdout hiện tại

Thư mục `02_selected_images` hiện có 9 file, nhưng inventory sơ bộ `preliminary-hash-inventory-8-of-15.json` chỉ ghi 8 và dùng hai tên cũ không còn khớp (`Katinat.jpg`, `Vinamilk.jpg` thay vì `NewKatinat.jpg`, `NewVinamilk.jpg`); `SachNhanVan.jpg` chưa có trong inventory. File inventory có trạng thái `PRELIMINARY_NOT_LOCKED`: không thay consent, redaction review hoặc ground truth và phải được tạo lại sau khi chốt đúng 15 ảnh.

Không thể coi đủ 8/15 một cách máy móc:

- `NewVinamilk.jpg`/Vinamilk đã được dùng nhiều lần để phát triển/kiểm tra discount và line-item guard, vì vậy phải loại khỏi blind holdout và chỉ giữ ở development regression;
- người kiểm thử đã mô tả Katinat là smoke input. Nếu `NewKatinat.jpg` từng được gửi provider, cũng phải loại khỏi blind holdout;
- sáu ảnh còn lại mới chỉ là candidate cho tới khi xác nhận quyền sử dụng, ẩn danh và ground truth trước lần gọi model đầu tiên.

Sau khi loại Vinamilk, tối đa còn 8 candidate; nếu Katinat đã chạy thì tối đa còn 7. Vì vậy cần thêm ít nhất 7 ảnh eligible, hoặc 8 nếu Katinat không còn blind; con số thực tế có thể cao hơn nếu consent/redaction/label review loại thêm ảnh. Không gửi bất kỳ candidate nào cho AURA/OpenRouter trước khi manifest được khóa.

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

## 6. Thứ tự đóng các gate

1. Bổ sung screenshot điện thoại thật/4G và video/click xác nhận reviewer/audit không nháy Upload; lưu timestamp recycle Pool nếu chưa có ảnh quản trị.
2. Tạo lại inventory sau khi chốt đúng 15 ảnh eligible; consent/redaction, label review và khóa manifest/hash trước request đầu tiên.
3. Chạy OpenRouter đúng một official run, fallback false; giữ mọi mismatch.
4. Thực hiện ba user sessions, chọn một cải tiến nhỏ truy vết được từ feedback, rồi regression.
5. Ba dress rehearsal trên commit freeze; lúc đó mới quay video cuối, điền `11_final_go_no_go` và public live URL trong README.

GPU BTC là POC sau các gate trên, không phải blocker của MVP. Chuẩn bị trước Docker/runtime, model+quantization, prompt/schema, synthetic manifest, lệnh đo 1/2/5 concurrency, VRAM/P50/P95 và yêu cầu TLS/auth/private network; không đưa receipt thật lên GPU mượn ở lượt đầu.

## 7. Nội dung báo cáo tiến độ ngắn

> Ngày 04/10, em đã deploy và tái kiểm bản vá `c42c5ef`: health/DB/storage/migration, security-negative, first-paint tab, AUTO smoke, FACT fail-safe smoke, Official Verify 5/5, human workflow/audit, persistence sau recycle và concurrency 2 + 5 request đều đạt; build sạch và 108/108 regression pass. Mobile core render được nhưng navigation 390 px và Audit horizontal scroll còn cần hoàn thiện. Đây là technical staging evidence, chưa phải production accuracy. Việc tiếp theo là khóa blind holdout 15 ca trước request đầu tiên, tổ chức ba user session, bổ sung manual mobile/4G/no-flash evidence và chạy ba dress rehearsal trước final go/no-go/public live URL.
