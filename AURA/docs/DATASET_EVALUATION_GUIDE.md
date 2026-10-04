# Hướng dẫn bộ dữ liệu và đánh giá mở rộng

Cập nhật ngày 01/10/2026. Official Verify Harness vẫn giữ nguyên đúng 5 ca mà BTC đã biết. Bộ 15/30 ca là phép đánh giá nội bộ chạy ngoài giao diện, không tạo thêm nút và không thay đổi expected result của Verify.

## 1. Chiến lược dữ liệu

AURA dùng ba lớp dữ liệu tách biệt:

| Lớp | Mục đích | Nguồn và quy tắc |
|---|---|---|
| Official Verify 5 ca | Smoke test trong demo | Fixture tổng hợp đã khóa, không thay đổi nếu chưa báo BTC |
| Synthetic 15/30 ca | Phủ nhánh routine, FACT, POLICY và AUTHORITY | Sinh tất định bằng Python/Pillow, có seed, manifest và ground truth |
| Holdout thực tế | Đánh giá khả năng tổng quát | Ảnh được phép dùng, đã ẩn danh, không đưa vào Git và không dùng để chỉnh prompt trước khi chấm |

Không dùng ảnh hóa đơn do generative AI tạo làm nguồn ground truth chính. Model tạo ảnh thường làm sai chữ, số tiền, mã số thuế hoặc cấu trúc dòng hàng, khiến lỗi của công cụ sinh ảnh bị tính nhầm thành lỗi của AURA. Có thể dùng công cụ tạo ảnh cho nền/chất liệu phụ, nhưng nội dung chữ và nhãn phải được dựng bằng code và review thủ công.

Không clone nguyên logo, mã số thuế, số hóa đơn hoặc dữ liệu cá nhân của một hóa đơn thật. Hóa đơn thật chỉ dùng làm tham chiếu bố cục khi đã được phép và ẩn danh. Các merchant, identifier và nội dung trong Test Kit phải là dữ liệu tổng hợp.

## 2. Bộ dữ liệu hiện có

- `test_kit/manifest.json`: 30 ca tổng hợp đầy đủ expected status và expected facts.
- `test_kit/judge-manifest.json`: 15 ca tuyển chọn, gồm 5 routine, 4 FACT, 3 POLICY và 3 AUTHORITY.
- `test_kit/images/`: ảnh được sinh bằng `tools/generate_verify_receipts.py`.
- `test_kit/local_real/`: holdout thực tế cục bộ; ảnh bị Git ignore.

Ba mươi ca hiện tại bao phủ nhiều bố cục như mobile commerce, POS, giấy in nhiệt, VAT, nhà hàng và ride receipt; đồng thời có blur, crop, thiếu identifier, lệch số tiền, ngày cũ/tương lai/cuối tuần, draft/refund, hàng hóa bị cấm và vượt thẩm quyền.

## 3. Vì sao không thêm nút 15/30 ca vào UI

Một ca đánh giá cần ảnh, `claimedAmount`, expected status và ground truth riêng. Thả 15–30 ảnh vào form một hóa đơn với một ô số tiền sẽ làm mất mapping và tạo kết quả không thể kiểm toán. Một nút batch mới cũng làm dashboard demo rối và có thể tiêu 15–30 request ngoài ý muốn.

Runner ngoài UI gọi đúng endpoint production `/Applicant/UploadReceipt`, nhận HTTP 202, poll `/Applicant/Status/{id}` và lưu bằng chứng. Vì vậy phép đo vẫn đi qua validation, storage, queue, provider, semantic validator, policy, database và audit giống upload thủ công.

Khi manifest tuyển chọn có `sourceManifest`, runner tự đối chiếu case theo `id` để lấy `expected_facts` từ manifest nguồn. Nhờ đó `field exact match` của gói 15 ca không còn bị để trống chỉ vì file tuyển chọn cố ý giữ cấu trúc ngắn. Metadata lưu cả SHA-256 của manifest tuyển chọn và manifest nguồn.

## 4. Chạy gói 15 ca

Khởi động AURA trước, giữ fallback tắt khi đo riêng một provider. Sau đó chạy từng lệnh trên một dòng:

Nếu source vừa thay đổi, phải chạy `dotnet build` rồi mới dùng `dotnet run --no-build`. Không dùng một assembly cũ để benchmark source mới; kiểm tra `/healthz` phải có `databaseAvailable=true` và `storageAvailable=true` trước khi bắt đầu.

```powershell
cd D:\aura\AURA\AURA
```

```powershell
.\tools\Invoke-ExtendedDatasetEvaluation.ps1 -BaseUrl "http://localhost:5000" -ManifestPath ".\test_kit\judge-manifest.json" -ImagesDirectory ".\test_kit\images" -MaxCases 15 -InterCaseDelaySeconds 4
```

## 5. Chạy đủ 30 ca

Chỉ chạy khi đã xác nhận quota và chi phí:

```powershell
.\tools\Invoke-ExtendedDatasetEvaluation.ps1 -BaseUrl "http://localhost:5000" -ManifestPath ".\test_kit\manifest.json" -ImagesDirectory ".\test_kit\images" -MaxCases 30 -InterCaseDelaySeconds 4
```

Mỗi lượt tạo một thư mục bị Git ignore trong `test_kit/results/<timestamp>` gồm:

- `metadata.json`: commit, manifest SHA-256, cấu hình health và thời gian chạy;
- `results.csv`: một dòng cho mỗi ca;
- `results.json`: dữ liệu đầy đủ, expected/actual facts;
- `summary.json`: decision accuracy, field exact match, missed escalation, over escalation, system error, fallback và P50/P95.

Không xóa ca lỗi khỏi kết quả. Lỗi provider phải giữ `ESCALATE_SYSTEM_ERROR`; không chạy lại chỉ để thay kết quả fail.

## 6. Holdout thực tế

Mức tối thiểu trước demo là 10–15 ảnh được phép dùng và chưa từng dùng để sửa prompt/policy. Mỗi ảnh cần hai người review ground truth khi có thể. File nhãn cục bộ nên có:

- mã ca và tên file ẩn danh;
- số tiền khai báo;
- expected status;
- document type, merchant, subtotal, receipt-level discount, final payable amount, date và identifier;
- lý do escalation;
- người gán nhãn và bất đồng nếu có.

Kết quả 15 ca chỉ là bằng chứng định hướng cho demo. Không tuyên bố accuracy production từ mẫu nhỏ. Mục tiêu 100 ảnh trong `MEASUREMENT_PLAN.md` vẫn là cổng cho đánh giá đáng tin cậy hơn sau hackathon.

Dùng `test_kit/holdout-manifest.template.json` làm cấu trúc tham khảo rồi sao chép ra thư mục private ngoài Git. Manifest chính thức phải điền SHA-256 64 ký tự cho từng ảnh. Runner xác minh toàn bộ hash **trước request đầu tiên**; chỉ cần một file lệch hash thì lượt chạy dừng và không gửi ảnh nào tới provider. Results và metadata cũng ghi lại hash thực tế để nối ground truth với đúng input.

Ảnh đã được dùng để sửa prompt/policy hoặc phân tích lỗi, chẳng hạn development receipt, không còn là blind holdout. Giữ chúng ở regression set và thay bằng ảnh unseen. Không đưa manifest thật, consent hoặc ảnh gốc vào Git.

## 7. Cổng chấp nhận

- Không missed escalation trong các ca rủi ro khóa.
- Không có PASS giả cho `ESCALATE_SYSTEM_ERROR`.
- Official Verify vẫn 5/5 và không thay fixture.
- File bằng chứng ghi đúng commit, provider, model và fallback state.
- Nếu benchmark một provider, `Vision:FallbackEnabled=false`.
- Nếu đánh giá fallback, báo riêng primary failure, served provider và recovery; không trộn vào benchmark model.
