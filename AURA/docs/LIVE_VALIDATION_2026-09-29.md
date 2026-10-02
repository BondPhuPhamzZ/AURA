# Live validation — 29/09/2026

## 1. Kết luận điều hành

Trên cùng build, cùng manifest 15 ca, cùng JSON contract, cùng deterministic policy và fallback tắt, OpenRouter đạt **15/15 quyết định**, trong khi Ollama local đạt **14/15**. OpenRouter cũng nhanh hơn đáng kể trên máy demo: P95 end-to-end **16.459 giây** so với **58.318 giây**. Vì vậy cấu hình demo hợp lý là **OpenRouter primary**; Ollama chỉ nên là tùy chọn offline/manual fallback trên laptop, chưa nên bật tự động cho mọi request.

Lý do không chọn chỉ theo field exact: Ollama đạt 73/75 field, cao hơn OpenRouter 70/75, nhưng Ollama bỏ sót một escalation ở TK-12 và tự duyệt chứng từ có tổng tiền cuối bị làm mờ. Với bài toán hoàn ứng, missed escalation là lỗi an toàn nghiêm trọng hơn vài metadata field không ảnh hưởng nhánh quyết định.

Đây là bằng chứng regression trên dữ liệu tổng hợp có nhãn, không phải tuyên bố accuracy production. Cổng còn thiếu là 10–15 hóa đơn thật đã đồng thuận/ẩn danh, không dùng trong prompt tuning, được hai người đối chiếu độc lập.

## 2. Phạm vi và tính công bằng

- Manifest tuyển chọn: `test_kit/judge-manifest.json`, 15 ca gồm 5 routine, 4 FACT, 3 POLICY và 3 AUTHORITY.
- Expected facts được resolve từ `test_kit/manifest.json` theo case ID; metadata lưu SHA-256 của cả hai manifest.
- Runner gọi đúng `POST /Applicant/UploadReceipt`, nhận `202`, poll status, đi qua private storage, SQL queue, vision provider, semantic validator, deterministic policy và audit.
- Mỗi provider dùng database và thư mục receipt sạch riêng.
- `Vision:FallbackEnabled=false` trong cả hai lượt, nên không trộn kết quả provider.
- Source đã build trước khi dùng `dotnet run --no-build`; `/healthz` xác nhận AI, policy, database và storage đều available.

## 3. Kết quả provider

| Chỉ số | OpenRouter 8B | Ollama local 4B |
|---|---:|---:|
| Decision exact | 15/15 — 100% | 14/15 — 93,33% |
| Field exact | 70/75 — 93,33% | 73/75 — 97,33% |
| Missed escalation | 0 | 1 |
| Over-escalation | 0 | 0 |
| System error | 0 | 0 |
| HTTP accepted P50 / P95 | 11 / 184 ms | 13 / 184 ms |
| End-to-end P50 / P95 | 3,614 / 16,459 giây | 52,238 / 58,318 giây |

### Sai lệch field của OpenRouter

- TK-12: đúng nhánh `ESCALATE_FACT`, nhưng không đọc merchant và intentionally để `totalAmount=null` vì tổng cuối bị làm mờ — 3/5 field.
- TK-15: đúng nhánh `ESCALATE_FACT`, nhưng không đọc merchant và total bị thiếu — 3/5 field.
- TK-22: đúng nhánh `ESCALATE_POLICY` nhờ phát hiện “Vé karaoke”, nhưng merchant được chuẩn hóa thành “Nhà hàng” thay vì tên fixture — 4/5 field.

Các sai lệch này phải tiếp tục được giữ trong evidence; không sửa expected result để làm đẹp benchmark.

### Lỗi an toàn còn lại của Ollama

TK-12 hiển thị một line item 336.000 VND nhưng dòng `TỔNG THANH TOÁN` bị làm mờ. Ollama suy diễn tổng tiền từ line item và trả `AUTO_APPROVE`. Sau khi prompt được bổ sung quy tắc “không suy diễn tổng cuối từ line item/subtotal”, targeted regression vẫn trả `AUTO_APPROVE` sau 50,476 giây. Do đó:

- không hard-code theo filename/case ID;
- không dùng Ollama 4B hiện tại làm fallback tự động cho mọi trường hợp;
- nếu bật cho demo offline, người vận hành phải mô tả đây là phương án có giới hạn và giữ human escalation cho dữ kiện mơ hồ.

## 4. Khả năng tải và giới hạn 90 giây

Concurrent smoke 5 upload trên OpenRouter hoàn tất 5/5:

- HTTP accepted P95: 93 ms;
- end-to-end P95: 17,072 giây;
- không system error.

Kết quả này ủng hộ kịch bản 4–5 BGK cùng thao tác trong một phiên demo, nhưng chưa phải load test production. Ollama trên RTX 3050 Laptop 4 GB mất khoảng 44–58 giây cho một ca tuần tự; nhiều request đồng thời sẽ xếp hàng và có nguy cơ vượt 90 giây. Không trình bày Ollama laptop như concurrent capacity.

## 5. UI, responsive và design system

Đối chiếu `D:\aura\system_design\DESIGN-linear.app.md` và kiểm tra thật trên CocCoc:

- palette dùng surface tối, primary `#5E6AD2`, không gradient/glow thừa;
- mọi component nhìn thấy dùng radius 6 px; chỉ spinner thật được phép hình tròn;
- desktop 1280×720, tablet 768×1024 và mobile 390×844 đều không có document-level horizontal overflow;
- mobile tabs reflow 2 + 1, không kéo ngang;
- bảng rộng dùng vùng scroll nội bộ thay vì làm tràn trang;
- Console không có warning/error trong phiên audit;
- alert rỗng và trạng thái có contrast rõ hơn;
- nhãn tab/heading dùng sentence case nhất quán.

Browser automation không thể tự gắn file vì extension CocCoc/ChatGPT chưa bật `Allow access to file URLs`. Đây là giới hạn quyền của extension, không phải lỗi AURA. Upload endpoint và toàn pipeline đã được test bằng runner thật; operator vẫn cần thao tác upload thủ công để quay minh chứng UI.

## 6. F12 evidence cần người vận hành ghi

1. Mở AURA và nhấn `F12` **trước** khi upload.
2. Chọn `Network` → `Fetch/XHR`, nhấn Clear; bật `Preserve log` nếu sẽ refresh.
3. Upload một hóa đơn đã ẩn danh, nhập claimed amount và nhấn `AI tự động kiểm`.
4. Mở request `Applicant/UploadReceipt`: ghi status `202`, Response có request/status URL, Timing và timestamp.
5. Mở các request `/Applicant/Status/{id}`: ghi chuỗi `PENDING/PROCESSING` đến `COMPLETED` hoặc `FAILED`.
6. Với Verify, mở `/Verify/RunHarness` và chụp bảng expected/actual của đủ 5 ca.
7. Chụp Console không có lỗi và tab Network; sau đó `Save all as HAR with content`.
8. Không chụp/đưa lên Git API key, Authorization header, cookie hoặc hóa đơn có PII.

## 7. Evidence và cách tái lập

- OpenRouter: `test_kit/results/openrouter-final-20260929` (local, Git-ignored).
- Ollama full: `test_kit/results/ollama-final-20260928` (local, Git-ignored).
- Ollama TK-12 regression: `test_kit/results/ollama-regression-tk12-20260929` (local, Git-ignored).
- Workbook cá nhân: `D:\aura\phan_bien\benchmarks\provider\AURA_Provider_Comparison_2026-09-29.xlsx`.
- Automated tests không gọi API trả phí; Verify và extended evaluator có gọi provider thật.

## 8. Quyết định và bước tiếp theo

1. Giữ OpenRouter làm primary cho demo, fallback tắt khi benchmark.
2. Không bật auto-fallback Ollama cho buổi chấm cho tới khi TK-12 hoặc một cơ chế uncertainty gate độc lập được xử lý và test lại.
3. Thu thập 10–15 hóa đơn thật đã ẩn danh; label trước, khóa tập holdout, rồi chạy mỗi provider đúng một lượt.
4. Ghi F12/HAR cho một upload manual, một Verify 5 ca và một tình huống manager approve/reject/undo.
5. Trong presentation chỉ chạy 1 upload + Verify 5; dùng workbook/ảnh đã ghi cho bộ 15 và concurrency, tránh tiêu thời gian/quota trực tiếp.
6. Sau hackathon, mở rộng tối thiểu 100 ảnh, auth/role thật, managed object storage, retention policy, antivirus và queue/circuit breaker phân tán trước production.
