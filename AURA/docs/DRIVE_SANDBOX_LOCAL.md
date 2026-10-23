# Drive synthetic sandbox cho AURA — chỉ Development local

## Quyết định và giới hạn

- Folder ID là nguồn dữ liệu; service account có quyền Viewer trên đúng folder
  test gọi Drive API read-only. Người dùng thả JPG/PNG vào folder, không cần
  email. Model không đăng nhập Drive; connector tải ảnh qua file gate rồi tạo
  từng SQL job.
- Connector mặc định tắt. Chỉ chạy khi ASPNETCORE_ENVIRONMENT=Development,
  DriveSandbox:Enabled=true, DriveSandbox:RequireMalwareScanner=false và có
  JSON key ngoài repository, đồng thời web server bind rõ một địa chỉ loopback
  như http://127.0.0.1:5097. Điều kiện cuối chỉ chấp nhận cho ảnh synthetic
  trong folder test riêng; chưa có malware scanner nên không dùng hóa đơn thật.
- Folder phải để Restricted, không bật Anyone with the link. Chỉ share cho
  service account Viewer và những người test thật sự cần upload. Nếu nhiều
  người cùng Editor, họ có thể nhìn/sửa file của nhau; đây không phải thiết kế
  intake cho người dùng doanh nghiệp.
- Mỗi ảnh tối đa 5 MB (hoặc ReceiptStorage:MaxFileSizeMb), đúng extension,
  MIME, magic bytes; PDF/ZIP/Google Docs/shortcut bị từ chối. Connector đối
  chiếu MD5, version, parent folder trước khi lưu riêng tư, SHA-256, tạo SQL
  PENDING + audit AI_QUEUED. Một **file ID + MD5 nội dung** = một request ID
  tất định; đổi tên/metadata, poll lại và restart không tạo job mới. Đổi bytes
  tạo checksum/job mới; đừng sửa ảnh đang ingest. Sau thay đổi thuật toán
  idempotency ngày 22/10/2026, dùng **DB local mới** khi bật connector; không
  trỏ bản mới vào DB test Drive cũ nếu chưa reconcile vì có thể nhập lại file.
- Intake hiện ghi vào file tạm trong thư mục riêng rồi chuyển tên khi đã đủ
  bytes. Nếu SQL trả kết quả không chắc chắn sau bước lưu file, lần poll sau
  có thể nhận lại file hoàn chỉnh theo source ID/size/SHA-256; file đích thiếu
  hoặc khác byte bị giữ nguyên và từ chối để kiểm tra thủ công, không ghi đè.
  Mất điện có thể để lại file `.tmp`; chưa có dọn dẹp tự động hay kiểm chứng
  atomic rename trên network share. Đừng xóa ảnh gốc trên Drive theo một
  khoảng thời gian cố định khi SQL job chưa terminal và chưa backup.
- Poll hiện quét chỉ file con trực tiếp của một folder, tối đa 100 entries
  mặc định. Vượt giới hạn thì dừng scan, không nhận một phần. Đây là local
  pilot, chưa chứng minh 1.000 ảnh/đa instance. AI permit vẫn là 1.

## Phần bạn cần thao tác, không gửi bí mật cho Codex

1. Giữ folder test riêng và ảnh tổng hợp; lấy folder ID từ URL Drive.
   Không đổi General access khỏi Restricted. Không gửi ảnh cá nhân thật.
2. Mở [Google Cloud Console](https://console.cloud.google.com/), tạo project
   thử nghiệm riêng; bật Google Drive API. Vào IAM & Admin → Service Accounts
   → Create service account (không cần Domain-wide delegation). Tạo JSON key
   cho local test khi thật cần; key này có quyền lâu dài nên đặt ở ví dụ
   %APPDATA%\AURA\secrets\drive-service-account.json, ngoài repository,
   không gửi qua chat/Git. Bảo vệ Windows account và thu hồi key sau thử
   nghiệm. Production nên dùng secret vault/workload identity thay vì file
   JSON lâu dài.
3. Mở JSON chỉ trên máy của bạn để lấy client_email (không gửi private_key).
   Trong Drive folder → Share → thêm email service account ở quyền Viewer.
   Nếu Google chặn chia sẻ, kiểm tra chính sách tài khoản/Workspace; không
   chuyển sang public link để lách.
4. Trong PowerShell, từ project root, chạy cấu hình nhưng vẫn giữ connector
   tắt (thay URL và đường dẫn key thực tế của bạn):

       .\tools\configure_drive_sandbox.ps1 -FolderUrlOrId 'https://drive.google.com/drive/folders/FOLDER_ID' -ServiceAccountJsonPath "$env:APPDATA\AURA\secrets\drive-service-account.json"

   Sau khi folder đã share đúng Viewer, chỉ có ảnh synthetic và SQL local
   riêng, chạy lại cùng lệnh với -EnableSyntheticOnly. Script chỉ ghi folder
   ID/đường dẫn key vào .NET user-secrets; không in private key.

## Chạy và kiểm thử local

Không trỏ localhost vào SQL hosting. Trong một cửa sổ PowerShell mới:

    sqllocaldb start MSSQLLocalDB
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ConnectionStrings__DefaultConnection = 'Server=(localdb)\mssqllocaldb;Database=AuraDriveSandbox_20261022;Trusted_Connection=True;MultipleActiveResultSets=true;Connect Timeout=5;ConnectRetryCount=0'
    dotnet build .\AURA.csproj -c Release
    dotnet ef database update --configuration Release --no-build
    dotnet run --no-build --no-launch-profile -c Release --urls http://127.0.0.1:5097

Kiểm http://127.0.0.1:5097/healthz: phải có status=ok,
databaseUpToDate=true, pendingMigrationCount=0.
driveSandboxConfigured=true chỉ là gate cấu hình, không chứng minh service
account đã đọc được Drive hay AI đã hoàn tất. Kiểm log “Drive sandbox scan
hoàn tất”, SQL có request mới và audit AI_QUEUED; sau đó poll trạng thái đến
terminal. Không gọi healthz là inference test.

Thử theo thứ tự: 1 PNG synthetic → 1 job; khởi động lại → vẫn 1 job; JPG khác
→ job thứ hai; PDF/giả MIME/ảnh >5 MB → không vào queue và có mã từ chối trong
App_Data/DriveSandbox/rejections.jsonl; file ở folder khác → không thấy.
Batch 5, 20, 100 đã được chạy trên bộ có 30 nội dung khác nhau. Xem
[báo cáo MVP 22/10/2026](DRIVE_MVP_VALIDATION_2026-10-22.md) để biết số
terminal, latency, provider error và lỗi nhân job đã được sửa. Không vượt
100 entries trước khi thiết kế lại giới hạn/pagination; không dùng bộ bản sao
để tuyên bố accuracy trên 100 hóa đơn độc lập.

## Tắt và thu hồi

    dotnet user-secrets set 'DriveSandbox:Enabled' 'false' --project .\AURA.csproj
    dotnet user-secrets set 'DriveSandbox:RequireMalwareScanner' 'true' --project .\AURA.csproj

Restart app để áp dụng. Gỡ quyền Viewer của service account khỏi folder;
nếu kết thúc thử nghiệm thì xóa/disable JSON key trong Google Cloud Console
và xóa file key cục bộ theo chính sách của bạn. Không publish connector lên
hosting, không commit key, không dùng bản này với hóa đơn thật.

## Gate còn mở trước production

Malware scanning/quarantine và decoder ảnh an toàn; danh tính người nộp và
RBAC; endpoint ảnh hiện chưa có authorization; consent, retention/backup,
provider privacy/Strict ZDR cần xác nhận độc lập; token vault hoặc identity
không dùng JSON key; cursor/idempotency và limiter phân tán; load test,
monitoring và restore drill. Folder Viewer không tự giải quyết các gate đó.

Nguồn Google: [service-account authentication](https://developers.google.com/identity/protocols/oauth2/service-account),
[tạo credential và chia sẻ tài nguyên cụ thể](https://developers.google.com/workspace/guides/create-credentials),
[Drive files.list theo parent](https://developers.google.com/workspace/drive/api/guides/search-files),
[download blob](https://developers.google.com/workspace/drive/api/guides/manage-downloads).
