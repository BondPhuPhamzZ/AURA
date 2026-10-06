# Regression Test Kit v3

## Vai trò và giới hạn

V3 là một **synthetic post-holdout regression alternative**, không thay Test Kit v2, không thay official real holdout và không tạo claim blind mới. Bộ ảnh được sinh deterministic bằng Pillow; script không đọc ảnh thật và không gọi AI. Mục tiêu là kiểm tra ổn định contract/policy/identifier taxonomy trên các bố cục gần receipt thực tế hơn.

Không commit ảnh hóa đơn thật, thông tin giao dịch thật hoặc bản dẫn xuất pixel từ holdout vào v3. Những bài học từ holdout chỉ được dùng ở mức taxonomy lỗi: mất mực, mã thiết bị bị nhầm thành mã giao dịch, nhãn định danh và provenance tổng cuối.

## Khác biệt so với v2

- V2 và raw benchmark cũ được giữ nguyên để tái lập lịch sử.
- Không còn nhãn ghép `Số hóa đơn/biên nhận`.
- `invoiceNumber`, `receiptNumber`, `transactionReference`, `documentNumber`, `posNumber`, `invoiceSerial` và `taxAuthorityCode` xuất hiện với nhãn riêng.
- `R3-05` là mực suy giảm nhưng tổng vẫn đọc trực tiếp được, expected `AUTO_APPROVE`.
- `R3-06` phá hủy vật lý giá trị giảm giá và tổng cuối, ngăn cả đọc trực tiếp lẫn tái dựng số học; expected `ESCALATE_FACT`, `totalAmount=null`, `totalAmountSource=NOT_VISIBLE`.
- `R3-07` chỉ có Shop ID/POS No/TID nên không được coi là mã truy vết.
- `R3-08` có số biên nhận bị mất ký tự và POS No vẫn không được dùng thay thế.

## Coverage đã khóa

| Nhánh | Số ca | Nội dung chính |
|---|---:|---|
| AUTO_APPROVE | 5 | VAT invoice number, PTT, RRN, Bill No, readable thermal fade |
| ESCALATE_FACT | 4 | destroyed total, device IDs only, destroyed receipt number, amount mismatch |
| ESCALATE_POLICY | 3 | alcohol, personal-care wipes, cinema |
| ESCALATE_AUTHORITY | 3 | ba mức tổng trên 1.000.000 VND |

`test_kit/v3/judge-manifest.json` là ground truth thực thi. Mỗi case có SHA-256, claimed amount, expected decision, expected facts và rationale. `SHA256SUMS.txt` khóa cả hai manifest và 15 ảnh.

## Quy trình tái tạo và review

```powershell
python tools/generate_regression_receipts_v3.py --as-of-date 2026-10-06
dotnet test .\tests\AURA.Tests\AURA.Tests.csproj -c Release
```

Sau khi sinh lại:

1. Xem `test_kit/v3/contact-sheet.jpg`, rồi mở riêng `R3-05`, `R3-06`, `R3-07` và `R3-08` ở kích thước gốc.
2. Xác nhận `R3-05` đọc được toàn bộ tổng; `R3-06` không thể xác định tổng; `R3-08` không thể phục hồi đầy đủ số biên nhận.
3. Không sửa expected theo output model. Nếu human review bất đồng, loại case khỏi gate hoặc tạo version mới.
4. Commit ảnh, manifest và hash trước lần request đầu tiên.

## Chạy regression thật

Mặc định runner vẫn dùng v2. Muốn chạy v3 phải truyền đường dẫn tường minh:

```powershell
$out = 'D:\aura\demo_evidence\19_regression_test_kit_v3_2026-10-06\01_live_openrouter'
.\tools\Invoke-ExtendedDatasetEvaluation.ps1 `
  -BaseUrl 'https://bondphupham-001-site1.ltempurl.com' `
  -ManifestPath '.\test_kit\v3\judge-manifest.json' `
  -ImagesDirectory '.\test_kit\v3\images' `
  -MaxCases 15 `
  -InterCaseDelaySeconds 4 `
  -OutputDirectory $out
```

Runner xác minh toàn bộ 15 SHA-256 trước khi lấy antiforgery token hoặc upload. Evidence gồm `metadata.json`, `results.json`, `results.csv`, `summary.json`, commit, health, manifest hash, image hashes, provider, decision, facts và latency.

## Cổng quyết định

- **Không ảnh hưởng baseline:** Verify v2 và 122+ automated tests vẫn pass; production config/fallback không đổi.
- **Đạt regression v3:** 15 case hoàn tất, 0 system error, 0 missed escalation; over-escalation và field mismatch phải được phân tích theo raw evidence, không sửa nhãn hậu nghiệm.
- **Không đạt:** giữ v2/OpenRouter baseline, lưu raw v3 làm evidence, phân loại lỗi fixture/model/contract và sửa trong version mới.
- Chỉ một tập hóa đơn thật mới, chưa từng được xem hoặc dùng để phát triển, mới có thể tạo claim blind tiếp theo.

## Raw v3 và revision v3.1

Raw v3 ngày 06/10 đạt 11/15 decision, 142/145 field, 0 system error và lộ hai safety gap cùng một fixture precondition sai. Không sửa raw/expected/hash v3. Xem [`REGRESSION_TEST_KIT_V3_LIVE_RESULT_2026-10-06.md`](REGRESSION_TEST_KIT_V3_LIVE_RESULT_2026-10-06.md).

V3.1 chỉ thay R3-12 bằng ảnh/hash mới có cả `Số biên nhận` và `Số chứng từ`; 14 ca còn lại giữ cùng mục đích kiểm thử. Sinh revision bằng:

```powershell
python tools/generate_regression_receipts_v3.py --as-of-date 2026-10-06 --revision 3.1
```

Chỉ chạy live v3.1 sau khi commit hardening, publish đúng commit, recycle pool và health trả `ok`. Không chạy lại v3 để thay raw 11/15.

`contact-sheet.jpg` chỉ là mục lục thumbnail nên chữ cố ý nhỏ. Runner phải upload từng ảnh
trong `test_kit/v3_1/images`, không upload contact sheet. Xem ảnh riêng ở 100% zoom và quy
trình review/hash tại [`../test_kit/v3_1/README.md`](../test_kit/v3_1/README.md).

Ca R3-12 hiện còn được thực thi trực tiếp qua `PolicyDecisionEngine` trong automated test:
ground truth `Vé xem phim` phải cho `ESCALATE_POLICY`. Như vậy manifest không thể tuyên bố
một expected decision mà policy C# hiện tại không tạo ra.

Live v3.1 đã chạy đúng một batch ngày 07/10: 15/15 completed, 13/15 exact decision,
0 missed escalation, 0 system error và field UTF-8-safe 140/146. Không chạy lại. Xem
[`LIVE_REGRESSION_V31_RESULT_2026-10-07.md`](LIVE_REGRESSION_V31_RESULT_2026-10-07.md).
