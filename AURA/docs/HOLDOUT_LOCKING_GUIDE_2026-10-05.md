# AURA — Khóa blind holdout và ground truth

Cập nhật: 05/10/2026. Dùng cùng `test_kit/holdout-manifest.template.json` và `tools/Invoke-ExtendedDatasetEvaluation.ps1`.

## 1. Câu cần hiểu đúng

> Khóa claimed amount, expected decision, expected facts và SHA-256 trước request đầu tiên.

Nghĩa là: trước khi bất kỳ ảnh nào của official holdout được gửi tới AURA/OpenRouter/Ollama, con người phải hoàn tất nhãn chuẩn và fingerprint của đúng file ảnh, rồi đóng băng chúng. Sau khi đã xem output model, không được sửa nhãn để biến kết quả thành PASS.

Ghi nhãn trên giấy có thể dùng làm worksheet review độc lập, nhưng **không đủ làm artifact official duy nhất**. Trước request đầu tiên phải chép nguyên nhãn đó vào manifest máy đọc được, review lại lỗi nhập liệu và hash manifest. Nếu chỉ chạy rồi mới nhập hoặc đối chiếu giấy, runner không thể chứng minh nhãn đã tồn tại trước output và không thể tính metric tái lập.

### Khóa

“Khóa” là freeze/version dữ liệu đánh giá, không nhất thiết là đặt mật khẩu hay mã hóa file. Sau thời điểm khóa:

- không sửa ảnh, tên file, claimed amount, expected status/facts hoặc rationale;
- không thay policy/prompt/model trong giữa official run;
- mọi sửa bắt buộc tạo version mới và official run mới, đồng thời giữ version cũ;
- ghi `lockedAtUtc`, commit, policy version và SHA-256 của manifest.

### Claimed amount

Số tiền người nộp **yêu cầu hoàn ứng** và nhập vào form. Đây là input nghiệp vụ của policy, không phải câu trả lời model.

- Nếu hoàn toàn bộ hóa đơn có giảm giá, dùng số tiền cuối cùng thực phải trả sau giảm.
- Không tự chọn subtotal, tiền khách đưa hoặc giá trước giảm.
- Nếu muốn test cố ý khai sai, ghi đúng số khai sai vào manifest và expected thường là `ESCALATE_FACT`.
- Baseline hiện đối chiếu claim với tổng thanh toán; partial reimbursement chưa phải luồng được hỗ trợ đầy đủ.

### Expected decision

Quyết định chuẩn do con người áp policy hiện hành trước khi thấy model output. Field manifest là `expectedStatus`, ví dụ `AUTO_APPROVE`, `ESCALATE_FACT`, `ESCALATE_POLICY`, `ESCALATE_AUTHORITY`.

- Đây không phải “dự đoán AI sẽ trả gì”.
- FACT dùng khi dữ kiện bắt buộc thiếu/mờ/mâu thuẫn.
- POLICY dùng khi facts đủ tin cậy nhưng vi phạm policy.
- AUTHORITY dùng khi facts/policy hợp lệ nhưng vượt quyền tự duyệt.
- Nếu nhãn còn tranh cãi, giải quyết bằng review thứ hai; không dùng output model để phân xử ground truth.

### Expected facts

Các dữ kiện chuẩn con người đọc trực tiếp từ ảnh: loại chứng từ, merchant, identifier, ngày, currency, subtotal, discount, tax, total và các field quan trọng khác.

- Chép đúng ký tự/số nhìn thấy; không đoán.
- Field không tồn tại hoặc không đọc được dùng `null` và giải thích trong rationale.
- Phân biệt “không có trên chứng từ” với “có nhưng bị che/mờ”.
- Expected facts giúp tách lỗi OCR/extraction khỏi lỗi policy.

### SHA-256

Fingerprint 256-bit của **đúng bytes file ảnh**, hiển thị thành 64 ký tự hex. Runner dùng nó để chắc chắn file chạy đúng là file đã được gán nhãn.

- Resize, crop, redaction, recompress hoặc sửa một byte đều tạo hash khác.
- Tính hash sau khi đã ẩn danh và chốt file cuối.
- SHA-256 không mã hóa ảnh, không chứng minh ảnh thật, không chứng minh consent và không xóa PII.

### Trước request đầu tiên

“Request đầu tiên” là lần đầu một ảnh official holdout được gửi qua upload endpoint tới pipeline/model mục tiêu. Health check, kiểm file, tính hash và review nhãn offline không phải AI request.

Nếu ảnh đã từng được gửi model để phân tích, sửa prompt/policy hoặc chọn rule thì ảnh đó không còn blind; chuyển nó sang development regression set.

## 2. Cấu trúc thư mục private

```text
09_blind_holdout_2026-10-04/
  00_consent_private/
  01_candidates_private/
  02_selected_images/
  03_ground_truth_locked/
  04_preflight/
  05_openrouter_official_run/
  06_metrics/
  07_mismatch_review/
```

Không commit ảnh thật, consent, manifest thật hoặc raw response có PII vào Git.

## 3. Quy trình step by step

### Bước 1 — Kiểm eligibility

Với từng ảnh, ghi `caseId`, chủ sở hữu/quyền sử dụng, đã từng gửi AI chưa, có dùng để tune không và source type. Loại ảnh không có quyền sử dụng hoặc chính giao dịch/ảnh đó đã dùng để phát triển. Trùng merchant với regression cũ không tự làm mất tính blind: `NewVinamilk.jpg` và `NewKatinat.jpg` là candidate nếu là giao dịch mới và chưa từng gửi pipeline/model; ảnh đổi tên/crop/che QR của một giao dịch đã chạy vẫn phải để ở regression.

### Bước 2 — Ẩn danh

Che tên người, số điện thoại, email, địa chỉ cá nhân, số thẻ/tài khoản, loyalty ID và QR nhạy cảm. Không che field đang dùng làm expected fact. Lưu ảnh đã ẩn danh thành file cuối; không sửa tiếp sau bước hash.

### Bước 3 — Chốt ma trận 15 ca

Khuyến nghị 5 AUTO, 4 FACT, 3 POLICY, 3 AUTHORITY; ưu tiên đa merchant/layout/ánh sáng/identifier. Đây là mục tiêu coverage, không được bẻ nhãn của ảnh để ép đủ quota. Nếu thiếu một nhánh, thu thập thêm ảnh hoặc dùng synthetic case và khai báo `sourceType` trung thực. Checklist chi tiết cho từng BH-01…BH-15 nằm tại [`HOLDOUT_MATRIX_15_CASES_2026-10-05.md`](HOLDOUT_MATRIX_15_CASES_2026-10-05.md).

### Bước 4 — Tạo inventory mới

Từ `D:\aura\AURA\AURA`:

```powershell
$holdoutRoot = 'D:\aura\demo_evidence\09_blind_holdout_2026-10-04'
$imageDir = Join-Path $holdoutRoot '02_selected_images'
$inventory = Join-Path $holdoutRoot '01_candidates_private\preliminary-hash-inventory.json'
```

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File '.\tools\New-HoldoutImageInventory.ps1' -ImageDirectory $imageDir -OutputPath $inventory
```

Mở JSON và xác nhận `imageCount=15`, tên file trùng tuyệt đối với thư mục. Inventory chỉ là hash sơ bộ, chưa phải manifest đã khóa.

### Bước 5 — Điền ground truth

Sao chép `test_kit/holdout-manifest.template.json` ra `03_ground_truth_locked\holdout-manifest.json`. Với mỗi ca điền tối thiểu:

- `id`, `fileName`, `sha256`;
- `sourceType`, `consentReference`, `redactionStatus`;
- `usedForPromptOrPolicyDevelopment=false`;
- `claimedAmount`, `expectedStatus`, `category`, `labelRationale`;
- `expectedFacts` theo những gì thực sự nhìn thấy.

Không dùng secret/PII trong `consentReference`; dùng mã tham chiếu tới hồ sơ private.

### Bước 6 — Review nhãn độc lập

Reviewer 1 điền nhãn; reviewer 2 kiểm ảnh và policy mà chưa xem output AI. Ghi người review, thời gian và bất đồng trong `labelReview`. Chỉ khóa khi bất đồng đã được giải quyết bằng evidence/policy.

### Bước 7 — Khóa manifest

```powershell
$manifest = Join-Path $holdoutRoot '03_ground_truth_locked\holdout-manifest.json'
$manifestHashFile = Join-Path $holdoutRoot '03_ground_truth_locked\manifest.sha256.txt'
```

```powershell
Get-FileHash -LiteralPath $manifest -Algorithm SHA256
```

Ghi hash 64 ký tự vào `manifest.sha256.txt` cùng `lockedAtUtc`, Git commit, policy version, provider/model và `FallbackEnabled=false`. Sau đó đặt folder read-only nếu muốn hỗ trợ kỷ luật vận hành; hash mới là bằng chứng phát hiện thay đổi.

### Bước 8 — Preflight không gửi ảnh

Ghi `git rev-parse HEAD`, `git status --short`, health JSON, provider/model/fallback, DLL hash và manifest hash vào `04_preflight`. Health phải `ok`, DB up-to-date, storage available, OpenRouter đúng model và fallback false.

### Bước 9 — Chạy đúng một official run

```powershell
$output = Join-Path $holdoutRoot '05_openrouter_official_run'
```

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File '.\tools\Invoke-ExtendedDatasetEvaluation.ps1' -BaseUrl 'https://YOUR-LIVE-URL' -ManifestPath $manifest -ImagesDirectory $imageDir -MaxCases 15 -InterCaseDelaySeconds 4 -OutputDirectory $output
```

Runner xác minh toàn bộ selected image/hash trước request đầu tiên. Chỉ một mismatch cũng phải dừng với **zero request sent**. Không chạy lại để thay kết quả xấu; nếu có sự cố hạ tầng, giữ run cũ và tạo run ID mới với lý do.

### Bước 10 — Báo cáo và giữ mismatch

Báo decision exact, missed escalation, over-escalation, critical-field exact theo field, system-error rate, repair rate, P50/P95. Không xóa ca fail. Phân loại nguyên nhân thành label, image/OCR, schema, semantic, policy, infrastructure hoặc measurement.

## 4. Gate pass an toàn

- không missed escalation ở ca safety-critical;
- không system error chưa giải thích;
- mọi mismatch giữ raw evidence và có phân tích;
- manifest/file hash vẫn khớp sau run;
- không có ảnh đã tune nằm trong tập blind;
- không có secret/PII bị đưa vào Git hoặc báo cáo công khai.

Holdout 15 ca đủ làm evidence MVP cho cuộc thi, nhưng vẫn quá nhỏ để tuyên bố accuracy production hoặc đại diện thị trường.
