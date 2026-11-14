# Evidence viewer — kiểm chứng local 14/11/2026

## Kết luận và nguyên nhân không thấy khung

Profile `.tmp/retention-sandbox/active.json` của sandbox ngày 26/10 chưa có section
`EvidenceLocalization`. Cấu hình mặc định là tắt, nên dialog chỉ hiển thị ảnh gốc.
Đây là thiếu cấu hình opt-in local, không phải model chưa trả tọa độ: Tesseract local
định vị bốn facts đã lưu; model và policy không dùng các khung này để quyết định.

Đã bổ sung section vào profile local và xác minh dữ liệu `vie`/`eng` theo SHA-256 ghim.
Launcher nay luôn in rõ trạng thái OFF cùng lệnh bật để tránh hiểu nhầm khi dùng profile cũ.

## Evidence do chủ dự án cung cấp

Đã xem đủ 12 PNG trong thư mục evidence riêng của chủ dự án; không đưa ảnh gốc lên Git.

| Nhóm ảnh | Nội dung quan sát được |
| --- | --- |
| `R3_01_1`, `R3_01_2` | AUTO, tổng 241.920 VND; thời gian xử lý hiển thị 4.726 ms |
| `R3_06_1` đến `R3_06_4_Undo` | FACT, tổng không đọc được; chuyển tiếp, hàng đợi và thông báo Undo; 10.752 ms |
| `R3_13_1`, `R3_13_2` | AUTHORITY, tổng 1.050.000 VND; 6.791 ms |
| `R3_06_And_R3_13` | R3-06 đứng trước R3-13 theo thời gian chuyển tiếp |
| `Audit_log_R3_01_06_13` | AUTO, RETURNED_FOR_MORE_EVIDENCE và FORWARDED_TO_AUTHORITY |
| `Check_img_1`, `Check_img_2` | Dialog R3-14, vừa khung/kích thước gốc; chưa hiện mục OCR |

Ba thời gian trên là số hiển thị của từng lượt xử lý, không phải latency Drive đầu cuối
hoặc benchmark mới. Screenshot không chứng minh Excel, bàn phím, hai tab hoặc mọi bước
trong checklist. Chủ dự án báo đã thử chức năng; chỉ các quan sát trên có ảnh đối chiếu.

## Kiểm thử runtime trên bản sao synthetic

Sao chép bốn hồ sơ đã COMPLETED cùng facts, audit và ảnh sang DB/storage probe riêng.
Giữ nguyên DB nguồn. Probe tắt retention/Drive/Verify, dùng `BlockExternal`, không gọi AI.
Facts dùng ở đây là kết quả đã lưu của lượt test người dùng, không phải facts từ manifest.

| Hồ sơ | Các trường được định vị | Trường bỏ qua |
| --- | --- | --- |
| R3-01 | merchantName, identifier, documentDate, totalAmount | Không |
| R3-06 | merchantName, identifier, documentDate | totalAmount: AI không trả tổng thanh toán |
| R3-13 | merchantName, identifier, documentDate, totalAmount | Không |
| R3-14 | merchantName, identifier, documentDate, totalAmount | Không |

Chrome headless: xem ảnh desktop 1440×1000/mobile 390×844; zoom; bật/tắt overlay; Esc;
dialog tồn tại qua 5,5 giây polling; không có page error. Đã xem ảnh render của R3-01 và
R3-06, tổng bị phá không bị khoanh sang tạm tính. R3-14 dùng link hàng đợi thật; ba hồ sơ
đã đóng dùng link fixture thêm vào DOM trong probe để gọi cùng viewer. Audit trong sản phẩm
vẫn mở ảnh bằng link thường; không diễn giải fixture DOM là thay đổi nghiệp vụ.

Development với cờ false: health báo localization false và endpoint trả 404.
Production với cờ true: health vẫn false, endpoint 404, không có URL overlay trong dialog.
Các kiểm tra này không sửa facts/quyết định và không tạo benchmark model mới.

## Cách mở trên máy chủ dự án

Tại thư mục app, dừng app đang chạy rồi thực hiện:

```powershell
.\tools\Get-TesseractData.ps1
.\tools\Start-AuraRetentionSandbox.ps1 -PrepareOnly -EnableEvidenceLocalization
.\tools\Start-AuraRetentionSandbox.ps1
```

Hoặc dùng F5 profile AURA sau bước prepare. `/healthz` phải có
`evidenceLocalizationEnabled=true`. Bấm ảnh/link **Xem hóa đơn** trong bảng nhân viên
hoặc quản lý để mở dialog, chờ mục **Vùng bằng chứng · OCR cục bộ, thử nghiệm**.
Khung không nằm trên ảnh preview ở form upload; ảnh mở bằng tab mới không có overlay.
Hồ sơ đã đóng chỉ còn trong Audit có thể mở ảnh bằng link thường; để xem lại trong viewer,
dùng một lượt synthetic mới trong bảng kết quả, không Undo chỉ để xem ảnh.

## Kiểm tra source và ranh giới tài liệu

- Release: 458/458, 0 skip, LocalDB/OCR/PowerShell integration đều bật; frontend 18/18.
- Release build: 0 warning/error; EF không có pending model changes.
- Hai canary synthetic ở `phan_bien/` và `phanbien/` được Git ignore và không lọt gói
  `dotnet publish`; đã dọn đúng canary sau phép thử. Docker ignore đã thêm cả hai tên;
  chưa build/run Docker trong lượt kiểm này.
- Folder cá nhân thực tế nằm ngoài repository; không có file cá nhân nào được stage.
- Không tìm thấy file trùng SHA-256 trong docs/submission/folder cá nhân lúc rà soát.
  Các định dạng MD/DOCX/PDF cùng tên và báo cáo khác ngày được giữ để truy vết.
- Bản nộp thi không bị sửa số liệu; docs hiện hành và addendum ghi checkpoint mới.

## Định danh gói screenshot đã đối chiếu

| File | SHA-256 |
| --- | --- |
| `Audit_log_R3_01_06_13.png` | `0af14414b4583712f60bbcf35b0d97ac887a7e62b161c1c1a4ad0b52fa1ed11c` |
| `Check_img_1.png` | `d99fab42f57a46c3bf26070ec7be328816018b5b9019aa7b2b6bc51619eaa292` |
| `Check_img_2.png` | `20ae4e07278c11f93c5a3c19284cacb58018ee96a5715bc4a7078fb1259d0993` |
| `R3_01_1.png` | `2f540d7853fbd557c0c640d3610e0d5f6c10a368acbe43beae4b6e42321b9323` |
| `R3_01_2.png` | `14c1698c9f81a809b6f0e75aba798452541faac589d90ebcc7c0ee9f4609f65f` |
| `R3_06_1.png` | `cd1d22eaa613a21e210ac45f41dfee92d2854d576f6959a98cf93539c1732ee6` |
| `R3_06_2.png` | `794fe34f997121ad740bfd2aba805132172fedd46d6d127a5f7b3747e3bee347` |
| `R3_06_3.png` | `484769b956a7f6a5940d8abccd94dde391be3cc806aece59ad81d7cb4ab9c8a9` |
| `R3_06_4_Undo.png` | `f7589597ceb4db1179b546bc985dc17f30959b8c36f8291e718d2f4c8e55216c` |
| `R3_06_And_R3_13.png` | `2e8f01dd64c659013b9bb48238bc2fc55fd2f14e84dcbab6ce51c7ace17d75b2` |
| `R3_13_1.png` | `f495747191ba1ad73421ab39982ccf253963246ca2d8daeaad5370b72b4b4b12` |
| `R3_13_2.png` | `fd37da2217932e65b11deddf9cd3079d5aa181eeb74d75004774aad66065f3c0` |

## Gate còn mở

- P3 chưa có ≥30 ảnh mới và annotation độc lập đã khóa; không suy ra accuracy từ bốn ca này.
- Chưa publish PR #1, chưa có smoke hosting mới hoặc restore hosting được diễn tập.
- Giữ Production synthetic-only, OCR/Drive/Verify/retention cleanup tắt và migration flag false.
- Kiểm lại trên master sau merge; quyết định publish trước 18/11, trial hết hạn 20/11.

Đối chiếu [kế hoạch](EVIDENCE_LOCALIZATION_PLAN.md),
[checklist thủ công](MANUAL_FEATURE_TEST_CHECKLIST.md) và [deploy](DEPLOYMENT.md).
