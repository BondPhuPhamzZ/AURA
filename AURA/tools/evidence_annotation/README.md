# Công cụ đánh dấu vùng bằng chứng (giai đoạn 3)

Công cụ offline để **một người khác** (không phải người phát triển bộ định vị) đánh dấu vị trí
đúng của 4 trường trên ảnh hóa đơn. File `annotations.json` xuất ra là "đáp án" để đo khung OCR
của AURA. Ảnh chỉ mở trong tab trình duyệt và không được tải lên đâu cả. Trang có CSP chặn mọi
kết nối mạng.

## Chạy

```powershell
node .\tools\evidence_annotation\serve.mjs
```

Mở `http://127.0.0.1:8766/`. Server chỉ phục vụ 4 file của công cụ trên loopback, để trình duyệt
có secure context tính SHA-256; ảnh không đi qua server.

## Quy ước đánh dấu (áp dụng cho mọi ảnh)

| Trường | Vẽ khung bao | Chọn "Không có / không đọc được" khi |
| --- | --- | --- |
| Đơn vị bán | Cả dòng tên đơn vị (hai dòng nếu tên xuống dòng) | Không in tên, hoặc tên bị che/mờ không đọc được |
| Mã định danh | Nhãn và mã trên cùng dòng, ví dụ `Số hóa đơn: 000142` | Không có mã giao dịch, hoặc mã bị che/hỏng |
| Ngày in | Nhãn và ngày, ví dụ `Ngày hóa đơn: 05/10/2026` | Không in ngày, hoặc ngày không đọc được |
| Tổng thanh toán | Cả hàng: nhãn tổng **cuối cùng** và số tiền phải trả, không phải tạm tính | Số tổng cuối bị che/rách/mờ, kể cả khi vẫn còn nhãn |

Nhập giá trị **đúng như in** trên ảnh:
- tên đơn vị, mã và loại mã;
- ngày in nguyên văn;
- tổng tiền (dạng số) và dòng tổng nguyên văn.

Với trường không đọc được, vẫn nhập giá trị bạn biết từ nguồn khác (ví dụ tổng tiền theo
manifest) để kiểm tra bộ định vị **không** vẽ nhầm sang chỗ khác.

## Khóa trước khi chạy

1. Đánh dấu đủ mọi ảnh. Danh sách bên trái hiện ✓ khi một ảnh đã đủ thông tin.
2. Bấm **Xuất annotations.json**. Thanh trạng thái hiện SHA-256 của file; ghi lại mã này,
   hoặc commit file trước khi chạy đo.
3. Đặt `annotations.json` và đúng các ảnh đó vào cùng một thư mục, rồi chạy bộ đo:

```powershell
$env:AURA_RUN_LOCALIZATION_EVAL = '1'
$env:AURA_LOCALIZATION_SET = '<thư mục chứa annotations.json và ảnh>'
$env:AURA_TESSDATA_PATH = (Resolve-Path .\.tmp\tessdata).Path
dotnet test .\tests\AURA.Tests\AURA.Tests.csproj -c Release --filter "FullyQualifiedName~Locked_annotation_set"
```

Bộ đo kiểm tra từng ảnh: SHA-256 phải khớp và ảnh không có EXIF xoay. Sau đó nó chạy OCR thật
và ghi `report.md`/`report.json` vào `test_kit/results/localization-eval-*` (đã bị Git-ignore).
Báo cáo ghi lại SHA-256 của annotation. Test **fail khi gate đề xuất chưa đạt**:
- ít nhất 30 ảnh và 5 ca đối chứng âm;
- không khung nào đặt nhầm ở tổng tiền;
- tỉ lệ khoanh nhầm ≤ 2% và recall ≥ 80% cho mỗi trường.

Muốn chỉ xem báo cáo, đặt `$env:AURA_LOCALIZATION_GATE = 'report-only'`.

Muốn tiếp tục một bộ đang làm dở, bấm **Mở annotations.json** rồi chọn lại đúng các file ảnh.
Công cụ ghép theo tên file và SHA-256. Trường chưa trả lời được xuất là `unset`, và bộ đo sẽ
từ chối file cho tới khi đủ.
