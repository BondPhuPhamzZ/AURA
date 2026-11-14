# Checklist kiểm thử thủ công — các tính năng của PR #1 (11/2026)

Checklist này dùng để tự kiểm tra từng tính năng trên nhánh `codex/drive-pipeline-hardening`
trước khi merge. Mọi bước chạy **local với ảnh synthetic** trong `test_kit/`; không dùng hóa
đơn thật và không thao tác trên site SmartASP. Cột "Kỳ vọng" là kết quả đúng; kết quả khác
thì ghi lại bước, ảnh chụp màn hình và mã hồ sơ (8 ký tự đầu).

## 0. Chuẩn bị (một lần)

Chạy trong PowerShell, tại thư mục `AURA\AURA` của repo:

```powershell
git fetch origin
git checkout codex/drive-pipeline-hardening
git pull
dotnet tool restore
dotnet restore
.\tools\Get-TesseractData.ps1
.\tools\Start-AuraRetentionSandbox.ps1 -PrepareOnly -EnableEvidenceLocalization
```

- [ ] `Get-TesseractData.ps1` báo `vie.traineddata` và `eng.traineddata` "downloaded and verified" (hoặc "already present and verified").
- [ ] Bước prepare in ra tên DB `AURA_RetentionSandbox_yyyyMMdd` và dòng "Local Tesseract evidence boxes are enabled".
- [ ] Mục 3–5 cần gọi AI. Kiểm tra key thử nghiệm đã có trong User Secrets
  (`dotnet user-secrets list` phải có `OpenRouter:ApiKey`; không chụp hay gửi giá trị key).

Khởi động app và giữ cửa sổ này mở:

```powershell
.\tools\Start-AuraRetentionSandbox.ps1
```

- [ ] Mở `http://127.0.0.1:5000/healthz` → `status=ok`, `databaseUpToDate=true`, `evidenceLocalizationEnabled=true`.

## 1. Kiểm thử tự động

Mở một cửa sổ PowerShell khác, tại `AURA\AURA`:

```powershell
$env:AURA_RUN_LOCALDB_INTEGRATION = '1'
$env:AURA_RUN_OCR_INTEGRATION = '1'
$env:AURA_TESSDATA_PATH = (Resolve-Path .\.tmp\tessdata).Path
dotnet test .\tests\AURA.Tests\AURA.Tests.csproj -c Debug
node --test .\tests\frontend\workflow-refresh.test.cjs .\tests\frontend\dashboard-escape.test.cjs .\tests\frontend\receipt-viewer.test.cjs .\tests\frontend\evidence-annotation.test.cjs
```

- [ ] .NET: **458/458** pass, 0 skip.
  - Không đặt hai biến integration thì vẫn phải pass (các ca LocalDB/OCR tự bỏ qua).
  - Bật thêm `AURA_RUN_POWERSHELL_INTEGRATION=1` cần cả Windows PowerShell 5.1 và PowerShell 7 (`pwsh`).
- [ ] Frontend: **18/18** pass.
- [ ] Dùng **Debug** khi app đang chạy: app khóa file Release đang dùng.

## 2. Cửa sổ nhân viên — giao diện và validation

| # | Bước | Kỳ vọng |
| --- | --- | --- |
| 2.1 | Mở `http://127.0.0.1:5000`, chưa chọn ảnh | Chữ trong khung **Nội dung AI** nằm giữa khung theo cả chiều ngang và dọc |
| 2.2 | Xoá trống ô **Mã nhân viên**, chọn ảnh `test_kit\v3_1\images\R3-01-vat-invoice-number.jpg`, bấm **Bắt đầu xử lý** | Toast đỏ "Vui lòng nhập Mã nhân viên."; con trỏ nhảy về ô đó; không có hồ sơ mới trong bảng |
| 2.3 | Dán chuỗi dài hơn 100 ký tự vào **Mã nhân viên** | Ô chỉ nhận tối đa 100 ký tự (giới hạn sinh từ model) |
| 2.4 | Điền lại `EMP-001`, thu nhỏ cửa sổ về khoảng 400 px rộng | Bố cục xếp một cột, không có thanh cuộn ngang |

## 3. Xử lý một hóa đơn, định dạng tiền và tên file

| # | Bước | Kỳ vọng |
| --- | --- | --- |
| 3.1 | Tải lên R3-01 với thông tin người gửi hợp lệ | Bảng hiện dòng chờ/đang xử lý; sau vài giây có kết quả **AUTO_APPROVE**; khung Nội dung AI hiện dữ kiện, tổng `241.920 VND` |
| 3.2 | Tải lên `R3-06-destroyed-final-total.jpg` | Kết quả **ESCALATE_FACT**, nút **Chuyển tiếp** hiện ở dòng đó |
| 3.3 | Nhìn cột Ảnh của dòng có tên file dài | Tên nằm một dòng có "…"; rê chuột vào thấy tên đầy đủ |

## 4. Xem ảnh trong trang và khung bằng chứng OCR

Khung được đặt theo **dữ kiện AI thực tế** của từng hồ sơ. Nếu AI trả giá trị khác chữ in,
trường đó sẽ ghi "Không định vị được" kèm lý do. Đó là hành vi fail-closed hợp lệ, không phải
lỗi; ghi lại mã hồ sơ và lý do để đối chiếu.

| # | Bước | Kỳ vọng |
| --- | --- | --- |
| 4.1 | Bấm ảnh nhỏ của dòng R3-01 | Mở dialog trong trang (không mở tab mới), ảnh vừa khung, tiêu đề có tên file và mã hồ sơ |
| 4.2 | Chờ 1–3 giây | Mục **Vùng bằng chứng · OCR cục bộ, thử nghiệm** báo đã định vị 4 trường; khung xanh nằm đúng tên công ty, `Số hóa đơn: 000142`, `Ngày hóa đơn: 05/10/2026` và hàng `TỔNG THANH TOÁN … 241.920 đ` (không phải dòng tạm tính) |
| 4.3 | Bấm thẻ **Tổng thanh toán** ở danh sách, sau đó bấm thẳng vào một khung | Khung và thẻ tương ứng chuyển màu cam cùng lúc |
| 4.4 | Bấm **Kích thước gốc**, cuộn trong ảnh | Ảnh hiện cỡ thật; khung vẫn trùng chữ; nút đổi thành **Vừa khung** |
| 4.5 | Bỏ chọn **Hiện khung** | Khung biến mất, danh sách vẫn còn |
| 4.6 | Nhấn **Esc** | Dialog đóng, focus quay về đúng ảnh nhỏ vừa bấm |
| 4.7 | Mở dialog của dòng R3-06 | Trường tổng ghi "Không định vị được: …", **không** có khung ở dòng tạm tính 234.000 |
| 4.8 | Giữ **Ctrl** và bấm ảnh nhỏ | Mở tab mới như link thường, không mở dialog |
| 4.9 | Mở dialog rồi để yên hơn 5 giây (bảng tự làm mới) | Dialog vẫn mở |
| 4.10 | Bấm ra vùng tối ngoài dialog | Dialog đóng |

## 5. Cửa sổ quản lý — thời gian chờ và quyết định

| # | Bước | Kỳ vọng |
| --- | --- | --- |
| 5.1 | Ở dòng R3-06 bấm **Chuyển tiếp**, sang tab **Cửa sổ quản lý** | Tiêu đề "Hồ sơ chờ quản lý duyệt"; chip "1 hồ sơ đang chờ"; cột **Thời gian chờ** hiện "Dưới 1 phút" và giờ chuyển tiếp; số tiền hiển thị với dấu chấm phân nhóm (ví dụ `0 VND` nếu AI không xác định được tổng) |
| 5.2 | Chuyển tiếp thêm một ca nữa (ví dụ R3-13) | Ca chuyển tiếp **sớm hơn** nằm trên |
| 5.3 | (Tùy chọn) Dừng app, chạy `$env:ReviewSla__TargetHours='1'` rồi khởi động lại, chờ hơn 1 giờ | Ca chờ quá 1 giờ có nhãn đỏ **Quá hạn**, viền đỏ, chip "1 quá 1 giờ"; hệ thống **không** tự duyệt hay từ chối |
| 5.4 | Bấm **Đồng ý duyệt** ở một ca | Toast xác nhận; ca biến khỏi hàng đợi mà không tải lại trang |
| 5.5 | Sang **Lịch sử hành vi**, bấm **Hoàn tác** ở ca vừa duyệt | Ca quay lại hàng đợi quản lý; thời gian hiển thị dạng `dd/MM/yyyy HH:mm:ss` |

## 6. Tổng quan và báo cáo

| # | Bước | Kỳ vọng |
| --- | --- | --- |
| 6.1 | Mở tab **Tổng quan** | Số tiền dạng `1.234.567 VND` (dấu chấm), không có dạng `1,234,567` |
| 6.2 | Mở ô **Kỳ báo cáo** | Chỉ chọn được từ 01/2000 đến 12/2100 |
| 6.3 | Bấm **Tải Excel kỳ này** | Tải file `AURA_Operations_yyyy-MM.xlsx`, mở được, số dòng khớp số hồ sơ của tháng |

## 7. Khi tắt khung OCR (cấu hình mặc định)

```powershell
.\tools\Start-AuraRetentionSandbox.ps1 -PrepareOnly -EnableEvidenceLocalization:$false
.\tools\Start-AuraRetentionSandbox.ps1
```

- [ ] `/healthz` báo `evidenceLocalizationEnabled=false`.
- [ ] Dialog xem ảnh vẫn hoạt động nhưng **không** có mục "Vùng bằng chứng".
- [ ] Mở `http://127.0.0.1:5000/Evidence/Regions/<mã hồ sơ đầy đủ>` → **404**.

## 8. Công cụ đánh dấu và bộ đo (giai đoạn 3)

```powershell
node .\tools\evidence_annotation\serve.mjs
```

| # | Bước | Kỳ vọng |
| --- | --- | --- |
| 8.1 | Mở `http://127.0.0.1:8766/`, bấm **Chọn ảnh**, chọn 2–3 ảnh trong `test_kit\v3_1\images` | Danh sách bên trái hiện các ảnh; ảnh đầu tiên hiện ở giữa |
| 8.2 | Ở **Đơn vị bán** chọn **Có trên ảnh**, kéo chuột quanh dòng tên | Khung cam ôm dòng tên; dòng "Khung: x=… (thang 0..1000)" cập nhật |
| 8.3 | Đánh dấu đủ 4 trường; với R3-06 chọn **Không có / không đọc được** cho Tổng thanh toán; nhập giá trị in trên ảnh | Tên ảnh trong danh sách có dấu ✓; "Ảnh này đã đủ thông tin." |
| 8.4 | Bấm **Xuất annotations.json** | Tải về file; thanh trạng thái hiện SHA-256 của file để khóa |
| 8.5 | Chép `annotations.json` và đúng các ảnh đó vào một thư mục, chạy bộ đo theo [README công cụ](../tools/evidence_annotation/README.md) với `AURA_LOCALIZATION_GATE=report-only` | Có `test_kit\results\localization-eval-*\report.md` ghi SHA-256 annotation và bảng recall/khoanh nhầm; với ít ảnh, gate báo **CHƯA ĐẠT** do chưa đủ 30 ảnh/5 đối chứng âm — đúng thiết kế |

## 9. Dọn dẹp

- [ ] Dừng app (`Ctrl+C`) và server công cụ đánh dấu.
- [ ] Không commit các thư mục `.tmp\`, `test_kit\results\` hoặc file annotation của bộ ảnh
  thử. Chúng đã bị Git-ignore; kiểm tra lại bằng `git status`.
- [ ] Muốn bỏ DB sandbox của ngày thử thì xoá theo [RUNBOOK](RUNBOOK.md). Không xoá `AuraDb`
  dùng cho cuộc thi.
