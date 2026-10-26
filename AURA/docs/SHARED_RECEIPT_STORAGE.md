# Lưu ảnh hóa đơn dùng chung, riêng tư

## Tình trạng hiện tại

AURA mặc định lưu ảnh cục bộ (`LocalFileSystem`); Google Cloud Storage (GCS) là tùy chọn để nhiều máy chủ cùng đọc ảnh. GCS **chưa được kiểm thử với bucket và quyền truy cập thật**. Khi chưa bật GCS, ảnh vẫn nằm trên máy chủ hiện tại.

Giai đoạn đã chốt vẫn dùng một host local. Các bước bucket/IAM dưới đây là hướng
tương lai khi có lựa chọn triển khai/ngân sách riêng, không phải yêu cầu phải
mở cloud để chạy supervised synthetic MVP.

Ảnh được đặt tên bằng mã hồ sơ do AURA sinh. GCS chỉ tạo đối tượng lưu trữ mới, không ghi đè ảnh khác; nếu đã có ảnh cùng tên, AURA chỉ dùng lại khi kích thước và SHA-256 khớp. Khi đọc, AURA cố định phiên bản ảnh rồi đối chiếu kích thước/hash với SQL trước khi xử lý. Ứng dụng không công khai địa chỉ bucket hoặc tạo liên kết truy cập tạm thời (signed URL).

Với `LocalFileSystem`, staging sweeper dọn file tạm quá 24 giờ. Bộ retention riêng đã được triển khai opt-in và kiểm thử synthetic: xóa ảnh/SQL/facts/audit của hồ sơ đã chốt sau 7 ngày, giữ pending và escalation chưa chốt. Startup catch-up và cleanup là best-effort khi app chạy; DB cũ vẫn chưa bật theo lựa chọn người dùng. Xem [runbook](RUNBOOK.md).

## 1. Tạo bucket và quyền truy cập

1. Tạo vùng lưu trữ (bucket) GCS riêng cho AURA; thư mục Drive không thay thế được bucket.
2. Bật [Uniform bucket-level access](https://cloud.google.com/storage/docs/uniform-bucket-level-access) và [Public access prevention](https://cloud.google.com/storage/docs/public-access-prevention). Không đặt quyền `Anyone` hoặc tạo public URL.
3. Tạo tài khoản dịch vụ (service account) riêng cho AURA và cấp quyền [`Storage Object User`](https://cloud.google.com/storage/docs/access-control/iam-roles) **chỉ trên bucket này**. Không dùng tài khoản chỉ đọc Drive.

**Vì sao:** bucket riêng, không công khai và quyền giới hạn ở đúng bucket làm giảm khả năng người lạ hoặc ứng dụng khác đọc, sửa hay xóa ảnh.

## 2. Chọn cách xác thực

**Ưu tiên ADC (Application Default Credentials):**

- Trên Google Cloud, gắn tài khoản dịch vụ AURA vào workload (tiến trình ứng dụng). Không cần phát tán khóa riêng.
- Khi kiểm thử trên máy cá nhân, ưu tiên impersonation (mượn danh tính tài khoản dịch vụ trong thời gian ngắn):

```powershell
gcloud auth application-default login --impersonate-service-account=<email-service-account>
```

Tài khoản lập trình viên cần quyền `roles/iam.serviceAccountTokenCreator` trên tài khoản dịch vụ đích. Xem [tổng quan ADC](https://cloud.google.com/docs/authentication/application-default-credentials) và [thiết lập ADC trên máy cá nhân](https://cloud.google.com/docs/authentication/set-up-adc-local-dev-environment).

**Chỉ dùng JSON key khi máy chủ không hỗ trợ ADC:** giữ key ngoài repo, gói publish và thư mục ứng dụng; giới hạn quyền đọc file. Nếu máy chủ không bảo vệ được key, không bật GCS trên máy đó — hãy chạy worker ở hạ tầng hỗ trợ workload identity (danh tính gắn trực tiếp với máy chạy ứng dụng).

**Vì sao:** ADC/impersonation tránh lưu private key dài hạn. JSON key bị lộ có thể bị dùng để truy cập bucket cho tới khi bị thu hồi.

## 3. Cấu hình ứng dụng

Chỉ cấu hình sau khi bucket và quyền IAM đã sẵn sàng. Đặt bằng biến môi trường hoặc .NET User Secrets; **không ghi bí mật vào repo**.

```text
ReceiptStorage__Provider=GoogleCloudStorage
ReceiptStorage__GoogleCloudStorage__Bucket=<ten-bucket-rieng>
ReceiptStorage__GoogleCloudStorage__Prefix=aura/receipts
ReceiptStorage__GoogleCloudStorage__CredentialMode=ApplicationDefault
ReceiptStorage__MaxFileSizeMb=5
```

Nếu bắt buộc dùng JSON key, thay chế độ xác thực và thêm đường dẫn tuyệt đối nằm ngoài repo/thư mục ứng dụng:

```text
ReceiptStorage__GoogleCloudStorage__CredentialMode=ServiceAccountJson
ReceiptStorage__GoogleCloudStorage__ServiceAccountJsonPath=<duong-dan-tuyet-doi-den-file-json-ben-ngoai-app>
```

Hai chế độ xác thực loại trừ nhau. ADC có thể đọc `GOOGLE_APPLICATION_CREDENTIALS`; nếu biến này được đặt, AURA yêu cầu file xác thực nằm ngoài thư mục ứng dụng. Với SmartASP hoặc máy chủ không thuộc Google Cloud, **đừng mặc định là đã có danh tính ADC**: chỉ bật GCS nếu máy chủ hỗ trợ danh tính an toàn hoặc bảo vệ được key bên ngoài thư mục website.

**Vì sao:** cấu hình tách riêng provider, bucket và identity; giới hạn kích thước giảm tải và tránh nhận ảnh vượt chính sách.

## 4. Kiểm thử một ảnh tổng hợp

Chạy AURA trên môi trường phát triển/kiểm thử với GCS đã cấu hình, rồi gửi đúng một ảnh tổng hợp qua địa chỉ máy cục bộ `127.0.0.1`:

```powershell
.\tools\Invoke-PrivateReceiptSmoke.ps1 `
  -ImagesDirectory .\test_kit\v3_1\images `
  -BaseUrl http://127.0.0.1:5110 `
  -MaxCases 1 `
  -ConfirmExternalProviderSend `
  -RequireGoogleCloudStorage `
  -RequireStrictZeroDataRetention
```

Script kiểm tra tình trạng ứng dụng, SQL/migration, nơi lưu ảnh, chế độ từ chối thu thập dữ liệu, dự phòng đã tắt và chỉ một lượt gọi AI **trước khi tải ảnh lên**. Chỉ dùng `-RequireStrictZeroDataRetention` khi `/healthz` xác nhận Strict ZDR đang hiệu lực. Ảnh tổng hợp vẫn được gửi tới dịch vụ AI đã cấu hình; kết quả lưu cục bộ trong `test_kit/results/` (thư mục này đã loại khỏi Git). Bài smoke này không đo độ chính xác hay chứng minh khả năng nhiều máy chủ cùng đọc ảnh.

**Vì sao:** một ảnh tổng hợp giúp xác nhận cấu hình và đường đi dữ liệu trước khi thử nhiều ảnh. Chạy trên máy cục bộ và kiểm tra trước khi tải lên giúp tránh gửi nhầm ảnh hoặc gọi AI ngoài dự kiến.

Khi kiểm tra máy chủ thử nghiệm qua đường hầm tới `127.0.0.1`, có thể thêm `-RequireSqlDistributedAiGate`. Cờ này chỉ xác nhận giới hạn AI dùng chung qua SQL đã bật; nó **không chứng minh hai máy chủ thực sự tranh cùng một lượt gọi đúng cách**.

## 5. Trình tự mở rộng an toàn

1. Giữ `LocalFileSystem` cho tới khi xác nhận bucket là riêng tư và quyền đã cấp đúng.
2. Trên cơ sở dữ liệu phát triển/kiểm thử riêng, kiểm tra tình trạng ứng dụng, tải ảnh lên/đọc lại, kích thước/hash, worker và thử lại. Nếu thiếu ảnh hoặc hash sai, xử lý phải dừng an toàn, không được duyệt.
3. Không bật quy tắc tự xóa ảnh khi hồ sơ còn `PENDING`, `PROCESSING` hoặc có thể cần thử lại. Chưa áp dụng xóa sau một ngày; ảnh phải được giữ cho tới khi xử lý xong và hết thời hạn lưu đã được duyệt.
4. Chạy hai máy chủ ứng dụng cùng dùng SQL và bucket: xác nhận máy thứ hai đọc được ảnh; thử mất lease, dịch vụ AI giới hạn tốc độ, và khởi động lại/thử lại.
5. Sao lưu và khôi phục cả SQL lẫn ảnh; kiểm tra phiên bản ảnh/hash và ghi nhận RPO/RTO (mức mất dữ liệu và thời gian khôi phục tối đa chấp nhận được).
6. Chỉ bật GCS trên máy chủ thật sau các bước trên. Giữ giới hạn AI dùng chung qua SQL tắt cho tới khi kiểm thử tranh chấp, lỗi và tải nhiều máy chủ đạt.

**Vì sao:** phải giữ ảnh cho hồ sơ đang xử lý hoặc cần thử lại. Kiểm thử hai máy chủ và diễn tập khôi phục giúp xác nhận dữ liệu không phụ thuộc một máy. Chỉ tăng số worker sau khi đã kiểm tra lỗi và tải.

## Còn thiếu trước pilot/production

Các điều kiện còn thiếu gồm: GCS/IAM thật; kiểm thử hai máy chủ; diễn tập khôi phục cloud và RPO/RTO; xác thực/phân quyền người dùng; cách ly file nghi nhiễm mã độc; thời hạn lưu trữ được duyệt; giám sát/cảnh báo; cam kết quyền riêng tư của dịch vụ AI; và kiểm thử tải. Chỉ có bộ kết nối GCS **không làm AURA sẵn sàng cho production**.

GCS là nơi lưu ảnh riêng tư, không phải nguồn Drive. Adapter đã có `DeleteAsync` để cleanup qua interface; retention, versions/lifecycle và backup trên bucket thật **chưa được kiểm chứng**, không bật dựa trên test local. Không có thao tác xóa ảnh gốc trên Drive.
