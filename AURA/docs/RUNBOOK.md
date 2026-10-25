# AURA Runbook

## 1. Phụ thuộc

- .NET 8 SDK; bản 8.0.425 được khuyến nghị cho baseline hiện tại
- SQL Server LocalDB trên Windows hoặc SQL Server/Azure SQL có thể truy cập
- Local `dotnet-ef` 8.0.31 qua tool manifest
- OpenRouter API key có quyền gọi model cấu hình trong `OpenRouter:Model`

OpenRouter là provider chính cho cấu hình demo hiện tại. Cấu hình Ollama local tùy chọn nằm tại [`docs/LOCAL_OLLAMA.md`](LOCAL_OLLAMA.md); không cần cài Ollama để chạy baseline của BGK và không bật auto-fallback khi benchmark.

## 2. Clone và cấu hình local

```powershell
git clone https://github.com/BondPhuPhamzZ/AURA.git
cd AURA/AURA
dotnet tool restore
dotnet restore
dotnet user-secrets set "Vision:Provider" "OpenRouter"
dotnet user-secrets set "OpenRouter:ApiKey" "YOUR_OPENROUTER_KEY"
dotnet user-secrets set "OpenRouter:Model" "qwen/qwen3.8-flash"
dotnet user-secrets set "OpenRouter:ReasoningEffort" "none"
dotnet user-secrets set "Vision:FallbackEnabled" "false"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\mssqllocaldb;Database=AuraDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Connect Timeout=5;ConnectRetryCount=0"
dotnet ef database update
```

Không dùng `dotnet user-secrets list` khi quay video/chia sẻ màn hình vì lệnh có thể hiển thị secret.

## 3. Build, test và chạy

```powershell
dotnet build ../AURA.sln --no-restore
dotnet test tests/AURA.Tests/AURA.Tests.csproj --no-restore
dotnet run
```

Không thêm `--no-build` vào lệnh test trừ khi test project vừa được build đúng configuration; nếu không, CLI có thể chạy assembly cũ và báo sai số lượng test.

Trước khi mở AURA, kiểm tra cấu hình mà chưa gọi API AI:

```powershell
$evidenceRoot = Join-Path $env:TEMP "AURA-demo-evidence"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Test-DemoReadiness.ps1" -StartLocalDb -SkipHttp -DiagnoseLocalDb -OutputPath (Join-Path $evidenceRoot "00_preflight\preflight-before-app.txt")
```

Sau khi `dotnet run` đã lắng nghe ở cổng 5000, mở PowerShell thứ hai và chạy cổng kiểm tra đầy đủ:

```powershell
$evidenceRoot = Join-Path $env:TEMP "AURA-demo-evidence"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Test-DemoReadiness.ps1" -DiagnoseLocalDb -OutputPath (Join-Path $evidenceRoot "00_preflight\preflight-running-app.txt")
```

`-OutputPath` tự tạo thư mục cha và transcript; `$evidenceRoot` ở trên chỉ định vị trí lưu kết quả. Không cần `Tee-Object`, tránh PowerShell hiển thị error record đỏ chỉ vì stderr của native tool. Kết quả chạy app hợp lệ là `READY`, đúng database `AuraDb`, health `ok`, `databaseAvailable=true`, `databaseUpToDate=true`, `pendingMigrationCount=0` và fallback tắt. Script không in API key hoặc toàn bộ connection string.

Nếu cần ghi bằng chứng LocalDB hoặc lỗi xuất hiện không ổn định, chạy lệnh read-only sau trong đúng Windows account dùng để demo:

```powershell
$evidenceRoot = Join-Path $env:TEMP "AURA-demo-evidence"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Test-DemoReadiness.ps1" -StartLocalDb -SkipHttp -DiagnoseLocalDb -OutputPath (Join-Path $evidenceRoot "00_preflight\localdb-diagnostics.txt")
```

`sqllocaldb` đôi khi trả exit code 0 nhưng nội dung vẫn báo `registry configuration error`; readiness kiểm cả code và text nhưng xem lỗi inspect này là **warning chẩn đoán**, không phải bằng chứng dữ liệu hỏng. Bài kiểm tra có thẩm quyền khi app đang chạy là `/healthz`: nếu `databaseAvailable=true`, `databaseUpToDate=true`, `pendingMigrationCount=0` thì LocalDB đang phục vụ AURA dù lệnh inspect registry cảnh báo. Nếu health không xác nhận được database, đó mới là blocker. Không xóa instance, registry key hoặc file `.mdf` khi chưa backup và chưa xác nhận đường dẫn database.

Nếu test LocalDB từ một execution sandbox bị `error 50` / `Cannot create an automatic instance`, hãy chạy `sqllocaldb info MSSQLLocalDB` và `sqllocaldb start MSSQLLocalDB` trong PowerShell của **cùng Windows user đang chạy AURA**, ngoài sandbox bị giới hạn, rồi xác nhận `State: Running` trước khi chạy integration tests. Lỗi này có thể là quyền truy cập LocalDB registry của execution context, không tự nó chứng minh database hỏng. Không xóa registry, instance hoặc MDF để xử lý lỗi này.

Nếu `/healthz` treo hoặc `databaseAvailable=false` và log ứng dụng đồng thời có `SQL Network Interfaces, error: 50`, `0x89C50118` hoặc `Cannot create an automatic instance`, đây **không còn là warning**: runtime registration của LocalDB đang chặn kết nối. Quy trình phục hồi an toàn là đóng AURA; sao lưu cả `AuraDb.mdf` và `AuraDb_log.ldf`; export `HKCU\Software\Microsoft\Microsoft SQL Server\UserInstances`; đối chiếu từng `InstanceProcessId` với process thực tế và chỉ xóa entry runtime đã stale; chạy `sqllocaldb start MSSQLLocalDB`; rồi attach lại hai file `AuraDb` nếu instance mới báo `Cannot open database "AuraDb"`. Không xóa toàn bộ instance/folder, không xóa MDF và không dùng GUID registry lấy từ máy khác. Sau đó khởi động đúng `dotnet run --launch-profile AURA` và chỉ coi phục hồi hoàn tất khi `/healthz` trả `status=ok`, database true và pending migration bằng 0.

Nếu warning chỉ xuất hiện trong execution context bị giới hạn nhưng full preflight vẫn có `0 failures` và health xác nhận database, lưu cả warning lẫn health làm bằng chứng minh bạch; không cố sửa registry sát giờ demo. Xem ma trận test tại `docs/TEST_CASES.md` và lịch freeze tại `docs/SUBMISSION_CHECKLIST.md`.

### Sao lưu và diễn tập khôi phục local (SQL + ảnh)

Integration test `Synthetic_database_and_receipt_backup_restore_preserves_pending_job_and_original_retention_deadline` xác nhận SQL backup có checksum, `RESTORE VERIFYONLY`, restore DB LocalDB và ảnh synthetic khớp trạng thái `PENDING`, audit marker, kích thước và SHA-256. Deadline giữ nguyên sau restore; purger chỉ xóa hồ sơ đã chốt quá hạn và giữ job/ảnh còn chờ. Đây là diễn tập cơ chế trên dữ liệu synthetic; chưa phải backup/restore tập dữ liệu thật của máy hay của hosting.

1. Chỉ chạy trên một host local; dừng AURA và mọi worker/instance sau khi chờ queue rỗng hoặc ghi lại job còn chờ. Đọc tên database từ `ConnectionStrings:DefaultConnection` và đường dẫn ảnh từ `ReceiptStorage:Directory`. Không đặt backup bên trong repo, `publish`, `wwwroot` hay thư mục ứng dụng được deploy; ưu tiên ổ ngoài đã mã hóa bằng BitLocker (hoặc tương đương) và giới hạn quyền đọc.
2. Trong PowerShell, tạo thư mục backup riêng; dùng cùng `$backupRoot` cho cả `.bak` và ảnh:

   ```powershell
   $backupRoot = Join-Path $env:USERPROFILE (Join-Path 'AURA-Backups' (Get-Date -Format 'yyyyMMdd_HHmmss'))
   New-Item -ItemType Directory -Force -Path $backupRoot | Out-Null
   $databaseBackupPath = Join-Path $backupRoot 'AURA.bak'
   $databaseBackupPath
   ```

   Mở SSMS và kết nối `(localdb)\MSSQLLocalDB` bằng Windows Authentication. Chọn đúng database ở connection string → **Tasks → Back Up…** → **Backup type: Full** → dán giá trị vừa in của `$databaseBackupPath`. Trong **Options**, bật **Verify backup when finished** và **Perform checksum before writing to media**; đợi kết quả thành công.
3. Sao chép toàn bộ thư mục receipt đã cấu hình sang thư mục backup cùng đợt. Với đường dẫn mặc định:

   ```powershell
   $receiptSource = (Resolve-Path '.\App_Data\receipts').Path
   $receiptBackup = Join-Path $backupRoot 'receipts'
   if (-not (Test-Path -LiteralPath $receiptSource -PathType Container)) { throw 'Configured receipt directory not found; verify ReceiptStorage:Directory first.' }
   New-Item -ItemType Directory -Force -Path $receiptBackup | Out-Null
   robocopy $receiptSource $receiptBackup /E /COPY:DAT /DCOPY:DAT /R:2 /W:1 /XJ
   if ($LASTEXITCODE -ge 8) { throw "Copy failed: robocopy exit $LASTEXITCODE" }
   function Get-ReceiptTreeManifest([string]$Root) {
       $base = [IO.Path]::GetFullPath($Root).TrimEnd('\') + '\'
       @(Get-ChildItem -LiteralPath $Root -File -Recurse | Sort-Object FullName | ForEach-Object {
           '{0}|{1}|{2}' -f $_.FullName.Substring($base.Length), $_.Length,
               (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
       })
   }
   $sourceManifest = Get-ReceiptTreeManifest $receiptSource
   $backupManifest = Get-ReceiptTreeManifest $receiptBackup
   if (Compare-Object $sourceManifest $backupManifest) { throw 'Receipt copy size/hash check failed.' }
   'Receipt copy size/hash check passed.'
   ```

   Nếu cấu hình `ReceiptStorage:Directory` khác mặc định, dùng đúng đường dẫn đó. Giữ cả `.bak` và ảnh trong cùng thư mục backup, giới hạn quyền đọc, và áp dụng chính sách lưu/xóa đã được đơn vị sở hữu dữ liệu duyệt; backup cũng chứa dữ liệu nhạy cảm.
4. Tạo restore drill vào **database tên mới** và thư mục ảnh test mới; không overwrite database hiện hành:

   ```powershell
   $restoreName = 'AuraRestoreCheck_' + (Get-Date -Format 'yyyyMMdd_HHmmss')
   $restoreReceiptDirectory = Join-Path $env:TEMP $restoreName
   New-Item -ItemType Directory -Force -Path $restoreReceiptDirectory | Out-Null
   ```

   Trong SSMS dùng **Tasks → Restore → Database…**, chọn backup, đặt **Destination → Database** thành `$restoreName`, rồi ở **Files** đặt file data/log vào đường dẫn mới. Chép ảnh backup sang thư mục restore và đối chiếu hash:

   ```powershell
   robocopy $receiptBackup $restoreReceiptDirectory /E /COPY:DAT /DCOPY:DAT /R:2 /W:1 /XJ
   if ($LASTEXITCODE -ge 8) { throw "Restore copy failed: robocopy exit $LASTEXITCODE" }
   $restoredManifest = Get-ReceiptTreeManifest $restoreReceiptDirectory
   if (Compare-Object $backupManifest $restoredManifest) { throw 'Restored receipt hash check failed.' }
   $env:ConnectionStrings__DefaultConnection = "Server=(localdb)\MSSQLLocalDB;Database=$restoreName;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Connect Timeout=5;ConnectRetryCount=0"
   $env:ReceiptStorage__Directory = $restoreReceiptDirectory
   dotnet run
   ```

   Hai biến môi trường chỉ tồn tại trong cửa sổ PowerShell hiện tại; sau khi dừng AURA, đóng cửa sổ này để trở về cấu hình thường. Không ghi đè User Secrets hay connection string đang dùng.
5. Chỉ xác nhận drill đạt khi `/healthz` trả `ok`, migration bằng 0, queue/status/audit khớp, và ảnh đọc qua route chứng từ khớp kích thước/hash SQL. Ghi thời gian backup/restore và kết quả kiểm tra. Nếu khôi phục thật, đóng app, sao lưu trạng thái hiện tại trước, rồi mới đổi cấu hình trở lại; không xóa bản nguồn cho tới khi xác minh xong.

### Retention local 7 ngày (opt-in, chưa bật mặc định)

Để giữ nguyên DB đi thi, chạy sandbox riêng bằng script dưới đây. Mặc định script
dùng LocalDB tên `AURA_RetentionSandbox_yyyyMMdd` và ảnh riêng trong
`.tmp/retention-sandbox/<database>/receipts`; `.tmp` được Git/publish exclude.
Nó override connection string/retention chỉ trong process đang chạy, khôi phục
environment khi dừng và không sửa User Secrets. Chỉ dùng ảnh synthetic: model/API
key vẫn lấy từ cấu hình hiện có, script không tự kiểm chứng hay thay đổi route ZDR.

```powershell
.\tools\Start-AuraRetentionSandbox.ps1 -PrepareOnly
.\tools\Start-AuraRetentionSandbox.ps1
```

Lệnh đầu build Release và áp dụng migration vào DB sandbox. Lệnh thứ hai mở app
ở `http://127.0.0.1:5098`; nhấn Ctrl+C để dừng. Kiểm `/healthz`: `status=ok`,
`pendingMigrationCount=0`, `retentionCleanupEnabled=true`, `retentionDays=7`,
`receiptStorageProvider=LocalFileSystem`, fallback/Drive/Verify tắt và concurrency
1. DB cũ vẫn cần migration riêng nếu chạy qua lệnh `dotnet run` thường; không dùng
lệnh đó để thử retention trước khi chốt chuyển đổi dữ liệu lịch sử.

Regression script UTC được chạy riêng với cả Windows PowerShell 5.1 và PowerShell
7, bao gồm manifest V1 cũ, metadata không có ảnh, offset +07:00, phần giây lẻ,
report-only và biên hết hạn. Trên máy có cả hai shell và LocalDB, bật integration:

```powershell
$env:AURA_RUN_LOCALDB_INTEGRATION = '1'
$env:AURA_RUN_POWERSHELL_INTEGRATION = '1'
dotnet test tests/AURA.Tests/AURA.Tests.csproj -c Release --no-restore
```

Retention chỉ purge hồ sơ đã có quyết định cuối: `AUTO_APPROVE` hoặc trạng thái quản lý đã chốt. Hồ sơ đang chờ, đang xử lý, retryable và chờ người duyệt không bị xóa. Khi bật `DataRetention:Enabled=true`, app dọn toàn bộ backlog đủ điều kiện qua nhiều batch trước khi nhận request, rồi theo chu kỳ `DataRetention:SweepIntervalMinutes` (mặc định 5 phút). SQL giữ khóa update trên hồ sơ được chọn trong suốt bước xóa ảnh và commit, tránh thao tác cũ mở lại hồ sơ giữa chừng. Đến hạn thì Undo bị từ chối. Nếu startup cleanup lỗi hoặc không lấy được khóa cleanup, app không khởi động; lỗi định kỳ được log và thử lại ở chu kỳ sau. Đây là best-effort local: sweep có độ trễ theo chu kỳ, lỗi I/O/SQL có thể trì hoãn xóa, và khi máy tắt phải chờ app chạy lại; không bảo đảm xóa đúng giây.

Kết quả Verify không sở hữu ảnh riêng trong receipt store: `StoredFileName` rỗng
thì purger chỉ xóa facts/audit/record đã chốt, không gọi file store với key rỗng.
Backup vẫn phải tính deadline của các record này. Manifest mới thêm
`metadataOnlyRecordCount`/`metadataOnlyExpiriesUtc`; remover vẫn đọc manifest V1
cũ. Timestamp được giữ nguyên UTC/offset và độ chính xác khi đọc JSON trên cả
hai phiên bản PowerShell; không stringify `DateTime` rồi parse lại theo múi giờ máy.

1. Trước khi bật trên máy đang dùng, kiểm tra migration `20261024130944_AddReceiptRetentionDeadline` đã áp dụng, chạy test integration và diễn tập trên DB/thư mục synthetic riêng. Tạo backup đầy đủ và restore thử trước khi đổi `DataRetention:Enabled`; tuyệt đối không dùng script backup để xóa bản backup chưa đăng ký.
2. Tạo một backup set bên ngoài repo, ví dụ:

   ```powershell
   $backupRoot = Join-Path $env:USERPROFILE (Join-Path 'AURA-Backups' ('AURA_BACKUP_' + (Get-Date -Format 'yyyyMMdd_HHmmss') + '_01'))
   New-Item -ItemType Directory -Path $backupRoot | Out-Null
   $databaseBackupPath = Join-Path $backupRoot 'AURA.bak'
   $receiptBackup = Join-Path $backupRoot 'receipts'
   New-Item -ItemType Directory -Path $receiptBackup | Out-Null
   ```

   Tạo Full `.bak` bằng SSMS như phần trên; copy receipt vào `$receiptBackup` và xác minh hash. Dừng AURA cùng mọi writer. Trong cùng PowerShell, đăng ký catalog (lệnh chỉ đọc SQL, so sánh file với SQL; không tạo backup):

   ```powershell
   .\tools\Register-AuraLocalBackupRetention.ps1 `
     -BackupSetDirectory $backupRoot `
     -DatabaseBackupFile $databaseBackupPath `
     -ReceiptSourceDirectory (Resolve-Path '.\App_Data\receipts').Path `
     -ReceiptBackupDirectory $receiptBackup `
     -ConfirmApplicationStopped
   ```

   Script lấy connection string từ User Secrets hoặc `AURA_BACKUP_CONNECTION_STRING` nhưng không in giá trị; không truyền secret trên dòng lệnh. Manifest ghi size/SHA-256 và hạn purge. Hạn là sớm hơn giữa ngày tạo backup + 7 ngày và deadline sớm nhất của dữ liệu đã xử lý có trong snapshot. Vì vậy backup được tạo sau thời điểm hạn sẽ bị từ chối nếu SQL còn giữ hồ sơ quá hạn. Chỉ backup set theo mẫu `AURA_BACKUP_*` có manifest hợp lệ mới được remover quản lý; bản backup cũ/vô danh được giữ nguyên để tránh xóa nhầm.
3. Remover chạy report-only nếu thiếu `-Apply`; nó chỉ xóa backup set đã hết hạn sau khi kiểm tra tên, manifest, danh sách file, size và hash:

   ```powershell
   .\tools\Remove-ExpiredAuraLocalBackups.ps1 -BackupRoot (Join-Path $env:USERPROFILE 'AURA-Backups')
   .\tools\Remove-ExpiredAuraLocalBackups.ps1 -BackupRoot (Join-Path $env:USERPROFILE 'AURA-Backups') -Apply
   ```

   Lệnh thứ hai là xóa thực tế, chỉ chạy sau khi đọc kết quả report-only. Để tự động trên máy local, dùng Windows Task Scheduler chạy lặp mỗi **5 phút** và khi đăng nhập lại, bằng đúng Windows user có quyền xóa backup. Với máy chạy nền liên tục có thể thêm trigger **At startup** khi principal đã được cấu hình chạy trước đăng nhập. Bật **Run task as soon as possible after a scheduled start is missed** và chọn **Do not start a new instance** nếu task trước chưa xong. Action chạy `powershell.exe` với đối số `-NoProfile -File "<đường dẫn tuyệt đối tới Remove-ExpiredAuraLocalBackups.ps1>" -BackupRoot "<đường dẫn backup root>" -Apply`. Không dùng lịch chỉ một lần/ngày vì có thể giữ backup quá hạn gần một ngày khi máy vẫn bật. Kiểm tra lịch sử task và thư mục sau mỗi lần chạy; task hiện chưa được đăng ký tự động bởi AURA. App startup chỉ dọn DB/ảnh, không tự chạy script backup.

Script đăng ký backup giả định app và mọi writer đã dừng, `.bak` là snapshot mới nhất của đúng DB hiện tại, và source/backup receipt khớp mọi hồ sơ SQL. Nó kiểm hash receipt nhưng không thể chứng minh nội dung `.bak` là backup SQL hợp lệ; bật **Verify backup when finished** và **Perform checksum** trong SSMS, rồi restore drill riêng. SQL + file system không tạo snapshot nguyên tử; đây vẫn là rủi ro cần chấp nhận/đóng bằng quiesce và restore test trên đúng máy.

SQL và filesystem không có một transaction/snapshot nguyên tử chung. Dừng tất cả writer trước khi copy ảnh và backup DB để giảm cửa sổ lệch; nếu cần RPO/RTO được cam kết hoặc snapshot nhất quán khi app đang chạy, phải thiết kế backup quiesce/coordination riêng và kiểm thử đúng host. Đừng xem một lần LocalDB test là bằng chứng hosting backup, retention, hay recovery time.

Mở URL được in trong terminal. Không truy cập `/Verify` để tìm trang riêng; nút Verify nằm ngay trên trang chủ. `/Applicant` và `/Verify` chủ động chuyển về `/`.

## 4. Tái lập Verify

> Từ source 21/10/2026, Verify chỉ chạy khi `ASPNETCORE_ENVIRONMENT=Development` **và** `Verify__Enabled=true`; Production luôn chặn và không render nút, kể cả env còn bật. Tái lập trong Development cô lập, không trên Live URL. Xem [gate hardening](POST_CONTEST_HARDENING_2026-10-21.md).

1. Bấm **Chạy Verify Harness (90s)** một lần.
2. Chờ tối đa năm request Qwen chạy tuần tự qua OpenRouter.
3. Xác nhận đúng 5 dòng, có timestamp và latency.
4. Kỳ vọng fixture v2: TC-01..03 `AUTO_APPROVE`, TC-04 `ESCALATE_FACT`, TC-05 `ESCALATE_POLICY`.
5. Với `5xx`, chỉ chờ retry giới hạn của adapter. Với `429`, hệ thống không retry: dừng batch và kết quả phải là `ESCALATE_SYSTEM_ERROR`, tuyệt đối không PASS giả.

Fixture có thể tái tạo bằng Python/Pillow qua `tools/generate_verify_receipts.py --as-of-date 2026-09-21`. Script đồng thời sinh Test Kit v2 gồm 30 ca nhưng chỉ 5 ca đại diện được Verify gọi. Không đổi `as-of-date`, fixture hoặc expected sau khi chốt mà không cập nhật manifest, tài liệu và commit.

Test Kit v3/v3.1 là regression namespace riêng, không thay Verify/v2. Tái tạo bằng `tools/generate_regression_receipts_v3.py --as-of-date 2026-10-06 --revision 3.0|3.1`; runner phải nhận manifest và images directory tường minh. Raw v3 đã khóa/chạy một lần; chỉ chạy v3.1 sau publish đúng candidate, recycle và health `ok`.

Để bảo toàn credit: build + ít nhất 196 automated test offline trước, deploy, chạy health/security smoke không ảnh; Verify 5 ảnh chỉ chạy trong Development cô lập khi thực sự cần. Bộ 15/30 ca chỉ chạy trong phiên đánh giá riêng bằng runner ngoài UI sau khi xác nhận quota và quyền gửi ảnh.

Sau thay đổi contract giảm giá hoặc sửa lỗi live, làm theo cổng build/test/health trong `docs/CURRENT_PROJECT_STATUS.md` và `docs/SUBMISSION_CHECKLIST.md`. Không reset database và không chạy migration nếu facts vẫn nằm trong JSON hiện có; restart queue phải dùng chính DB cũ để chứng minh độ bền.

Sau official holdout 06/10, extraction dùng evidence contract v2. Dữ liệu JSON cũ vẫn đọc được nhưng không được chạy lại để viết đè official evidence. Mailbox sandbox thử nghiệm đã được gỡ và model hiện không có pending migration. Trước publish source 21/10 phải đạt Release build sạch, ít nhất 196/196 test và `dotnet ef migrations has-pending-model-changes` không có thay đổi. Thứ tự SmarterASP an toàn: backup DB/receipts và kiểm biến môi trường; giữ `Database__ApplyMigrationsOnStartup=false`, `Verify__Enabled=false`, `OpenRouter__DenyProviderDataCollection=true`, `OpenRouter__RequireZeroDataRetention=false` cho tới khi ZDR route được probe; publish Release đúng SHA; recycle; `/healthz` phải HTTP 200, `status=ok`, database/storage true, migration 0 và `verifyHarnessEnabled=false`. Sau đó chạy một synthetic smoke, kiểm dashboard/CSV và audit; không bật migration startup hoặc fallback.

Live gate hậu contract v2 phải dùng request mới, không gọi lại 15 ảnh là “blind”: (1) receipt có giờ `HH:mm:ss` + bia phải `ESCALATE_POLICY`; (2) receipt rõ, dòng tổng in đủ phải giữ `PRINTED_FINAL_TOTAL` và AUTO khi các điều kiện khác đạt; (3) ảnh có giá trị Total bị cắt phải FACT dù subtotal/items cộng được; (4) `04-10-26` phải canonicalize thành 2026-10-04; (5) Winmart có PTT + Mã CQT phải tách receipt/tax-authority; (6) money-role CoopSmile không được dùng cash/change/VAT làm total/discount. Lưu raw status/facts/audit và timestamp. Nếu model/provider chưa tuân schema v2, kết quả phải `ESCALATE_SYSTEM_ERROR`/`ESCALATE_FACT`, không rollback gate để lấy AUTO.

Live gate candidate 12/10 đã đạt: health đúng Qwen3.8 Flash/reasoning none, Verify 5/5 trong 65,004 giây, 0 system/provider error; năm client cùng bấm chỉ tạo 1 HTTP 200 + 4 HTTP 409. Không chạy lại Verify để săn số đẹp. Trước video chỉ rehearsal luồng trình bày trên commit freeze; nếu cần inference thật, dùng đúng một operator và không chạy upload song song Verify.

Delay giữa các ca Verify được cấu hình bằng `Verify:InterCaseDelayMs`, mặc định 4000 ms để bảo vệ quota. Không đổi giá trị trong cùng một benchmark; luôn ghi giá trị này vào metadata phép đo.

### Đánh giá mở rộng 15/30 ca, không thêm nút UI

Giữ dashboard gọn và giữ Official Verify đúng 5 ca. Khi cần chạy gói 15 ca:

```powershell
.\tools\Invoke-ExtendedDatasetEvaluation.ps1 -BaseUrl "http://localhost:5000" -ManifestPath ".\test_kit\judge-manifest.json" -ImagesDirectory ".\test_kit\images" -MaxCases 15 -InterCaseDelaySeconds 4
```

Runner gọi đúng upload production, poll trạng thái và ghi `metadata.json`, `results.csv`, `results.json`, `summary.json` vào `test_kit/results/<timestamp>`. Gói 30 ca dùng `manifest.json` và `-MaxCases 30`. Xem `docs/DATASET_EVALUATION_GUIDE.md` trước khi dùng ảnh thực tế hoặc công bố số liệu.

Trong demo, `DecisionPolicy:EscalateDuplicateReceipts=false` cho phép chạy lại cùng ảnh nhưng vẫn ghi nhận trùng trong audit. Trước production, đổi thành `true`. Thay đổi cấu hình này không cần sửa code.

Nếu một ca dừng gần đúng thời gian `OpenRouter:TimeoutSeconds`, đó là `AI_TIMEOUT`, không phải model “học kém đi”. Harness dừng gọi AI cho các ca còn lại sau timeout, rate limit, lỗi xác thực/credit/model hoặc lỗi contract JSON mang tính hệ thống để bảo vệ chi phí. HTTP 429 không được tự động retry.

### Model OpenRouter cho demo

- Mặc định demo: `qwen/qwen3.8-flash` qua endpoint Alibaba, reasoning `none`, hỗ trợ vision và structured output bằng JSON Schema. Candidate ngày 12/10 đạt 15/15 post-holdout regression và năm upload P95 41,079 giây; xem `EVALUATION_EVIDENCE.md`.
- Không bỏ `ReasoningEffort=none`: reasoning mặc định đã dùng hết output cap 4096 và tạo `AI_RESPONSE_TRUNCATED`. Không tăng output cap để che căn nguyên.
- `qwen/qwen3-vl-8b-instruct` được giữ làm rollback/baseline lịch sử; endpoint Parasail của model này đã phát sinh 429 trong các lượt live ngày 12/10.
- Qwen3-VL-4B-Instruct là ứng viên self-host tiếp theo, nhưng không được khai báo như route OpenRouter khi catalog chưa cung cấp endpoint đó.
- HTTP 404 / `AI_MODEL_UNAVAILABLE`: slug model sai, đã bị gỡ hoặc hiện không có endpoint; đây không phải quota.
- HTTP 401/403 / `AI_AUTH_ERROR`: key sai hoặc thiếu quyền.
- HTTP 402 / `AI_CREDITS_REQUIRED`: tài khoản không đủ credit hoặc key không được phép dùng model.
- HTTP 429 / `AI_RATE_LIMIT`: rate limit của OpenRouter/provider; ứng dụng không tự retry để tránh phát sinh thêm chi phí.
- Đổi model bằng `OpenRouter:Model`; luôn xác nhận model nhận input ảnh trên catalog trước khi đổi.

### Provider Ollama local tùy chọn

- Chọn bằng `Vision:Provider=Ollama`; quay lại baseline bằng `Vision:Provider=OpenRouter`.
- Model mặc định local là `qwen3-vl:4b-instruct`; một request tại một thời điểm. Contract hiện hành cần context 16384, output 4096 và timeout benchmark 600 giây: batch 8K ngày 12/10 có 10/15 response truncated, còn 16K hoàn tất 15/15 nhưng P95 processing 213,429 giây.
- Fallback mặc định tắt để benchmark không trộn provider. Khi demo local cần đường dự phòng, bật `Vision:FallbackEnabled=true`, giữ `Vision:Provider=OpenRouter` và đặt `Vision:FallbackProvider=Ollama` sau khi smoke Ollama. Không bật cấu hình loopback này trên SmarterASP.
- Fallback chỉ chạy một lần cho lỗi hạ tầng đủ điều kiện. Lỗi schema, semantic, truncated response hoặc invalid request vẫn chuyển kiểm tra thủ công.
- Sau JSON Schema, backend kiểm tra ngữ nghĩa tiền VND, loại chứng từ và các identifier. Dữ kiện mâu thuẫn được đọc lại đúng một lần; vẫn sai thì chuyển `ESCALATE_FACT`, không tự đoán hoặc tự nhân số tiền.
- Hóa đơn giấy phải có ít nhất một hàng hóa/dịch vụ đọc được mới có thể tự duyệt. Nếu vùng món hàng bị che, cắt, mờ hoặc model trả `lineItems=[]`, backend repair đúng một lần rồi fail-safe `ESCALATE_FACT`; không được suy ra “không có hạng mục cấm”.
- `/healthz` chỉ đạt `ok` khi policy, cấu hình AI, database, migration và thư mục receipt sẵn sàng; endpoint không gọi provider nên không thay cho một ảnh smoke test.
- Baseline năm fixture ngày 27/09/2026 từng đạt 25/25 bằng Ollama 4B. Lượt post-policy/contract v2 profile 8K ngày 06/10 đạt 13/15. Benchmark digest khóa với 16K ngày 12/10 đạt 15/15 decision, 69/75 field và 0 missed/over/system error; semantic repair xử lý được TK-02/TK-12. Tuy vậy P50/P95 processing 101,833/213,429 giây và chưa có capacity evidence trên endpoint GPU, nên chỉ coi là fallback candidate và không bật auto-fallback toàn cục.
- Benchmark GPU BTC dùng đúng `qwen3-vl:8b-instruct-q4_K_M`, `ContextTokens=16384`, `MaxOutputTokens=4096`, `Think=false` trên database test và synthetic data. Không dùng alias `qwen3-vl:8b`, vì alias có thể trỏ tới biến thể thinking và làm JSON hết output budget. Đây là benchmark mới; không được suy ra PASS từ cấu hình trước khi chạy. Runbook phiên mượn máy được giữ ngoài repository công khai.
- CPU/GPU off-load, context và output cap là ba biến khác nhau. AURA gửi nguyên byte ảnh,
  không resize; cách đo token/processor và điều kiện so sánh nằm tại
  [`LOCAL_OLLAMA.md`](LOCAL_OLLAMA.md).
- Xem hướng dẫn cài, giới hạn RAM và rollback tại `docs/LOCAL_OLLAMA.md`.

### Khi OpenRouter/Qwen trả lỗi

1. Mở **OpenRouter → Activity/Logs** và đối chiếu HTTP status, model, provider, token và chi phí.
2. Với `401/403`, xác nhận secret thuộc đúng tài khoản đã nạp credit và key chưa bị thu hồi/giới hạn bởi guardrail.
3. Với `402`, kiểm tra số dư và giới hạn chi tiêu riêng của API key. `Key limit` là trần chi tiêu, không phải số dư.
4. Với `429`, ghi provider/request ID/`Retry-After` từ server log, chờ hết cooldown rồi thử đúng **một ảnh**; không bấm Verify liên tục. Public baseline khóa upstream 120 giây nếu response không có `Retry-After`; local contingency có fallback đã gate dùng 300 giây để tránh quay lại Parasail quá sớm.
5. Với `5xx/timeout`, kiểm tra trang trạng thái OpenRouter/provider. Hệ thống phải giữ `ESCALATE_SYSTEM_ERROR`, không dùng kết quả giả hoặc cache cũ như một lần gọi AI mới.
6. Chỉ đổi model sau khi chạy lại ma trận 5 ca và xác nhận input ảnh, JSON Schema, latency và câu hỏi chuyển tiếp.

### Pipeline xử lý nền và fallback

1. Luôn chạy `dotnet ef database update` sau khi pull commit có migration mới.
2. Upload hợp lệ trả `202 Accepted` sau khi lưu file và record `PENDING`; model không còn giữ request HTTP mở.
3. Worker claim job bằng lease; UI gọi `GET /Applicant/Status/{id}` đến khi `COMPLETED` hoặc `FAILED`.
4. Refresh trong cùng tab không làm mất theo dõi vì status URL nằm trong `sessionStorage`. Nếu đóng tab, kết quả vẫn nằm trong database và audit.
5. Audit `AI_QUEUED` chứng minh request đã nhận; audit `AI_PROCESSED_*` ghi provider chính, provider phục vụ, fallback flag, error code và latency AI.
6. Circuit breaker mặc định mở sau ba lỗi hạ tầng liên tiếp, dùng fallback 60 giây rồi cho provider chính một lượt probe.
7. `VisionInvocationCoordinator` dùng chung cho Verify và worker, mặc định `Vision:MaxConcurrentRequests=1`. Khi 429, worker dừng drain queue; các job chưa claim vẫn nằm trong SQL.

### Khi có 2–5 người cùng thao tác

1. Chỉ một người được bấm **Verify Harness**. `WorkflowOperationGate` trả HTTP 409 cho Verify thứ hai trên cùng instance.
2. Candidate từ 12/10 dùng chung AI capacity gate cho Verify và worker nên không còn gọi provider song song trong cùng process. Vẫn không chạy Verify khi đo upload vì Verify giữ gate lâu và làm tăng latency FIFO.
3. Hai đến năm upload hợp lệ có thể được nhận 202 và giữ trong SQL; worker xử lý FIFO một hồ sơ tại một thời điểm. Nói rõ đây là durability/queue evidence, không phải AI concurrency SLA.
   `tools/Invoke-ConcurrentUploadSmoke.ps1` chỉ cho phép loopback mặc định, giới hạn 1–10 upload, dừng poll sau tối đa 900 giây (có thể cấu hình), từ chối URL có credentials/query/fragment, không ghi đường dẫn ảnh hoặc nội dung lỗi server vào report. Chỉ thêm `-AllowNonLoopback` khi đã duyệt rõ một mục tiêu staging synthetic; không dùng ảnh cá nhân hoặc coi smoke tải nhỏ là load test production.
4. Nếu một hồ sơ trả `AI_RATE_LIMIT`, dừng các lượt AI còn lại. Không recycle, retry dồn hoặc bật fallback chưa benchmark để che lỗi.
5. Trước demo kiểm OpenRouter Activity/Credits/Key limit và chạy đúng một ảnh smoke. Với 429, dùng video/raw evidence và trình bày fail-safe/human review.
6. App pool demo phải đặt **Maximum Worker Processes = 1**; gate hiện tại không phân tán giữa nhiều IIS process.
7. Giữ `Verify:Enabled=false` trên public URL. Source 21/10 chặn Verify trong Production ngay cả khi cấu hình nhầm `true`; không bật lại trên public Production.

Để bật fallback cho demo trên máy cá nhân:

```powershell
ollama list
Invoke-RestMethod 'http://127.0.0.1:11434/api/tags'
dotnet user-secrets set "Vision:Provider" "OpenRouter"
dotnet user-secrets set "Vision:FallbackEnabled" "true"
dotnet user-secrets set "Vision:FallbackProvider" "Ollama"
```

Khi benchmark từng provider, bắt buộc đặt `Vision:FallbackEnabled=false`; nếu không, một kết quả thành công có thể đến từ fallback và làm sai phép so sánh.

## 5. Cấu hình deploy

Các biến môi trường bắt buộc:

```text
OpenRouter__ApiKey=<secret>
OpenRouter__Model=qwen/qwen3.8-flash
OpenRouter__ReasoningEffort=none
Vision__Provider=OpenRouter
ConnectionStrings__DefaultConnection=<SQL Server connection string>
ReceiptStorage__Directory=<persistent volume path>
Database__ApplyMigrationsOnStartup=true
```

Quy trình release:

```powershell
dotnet test tests/AURA.Tests/AURA.Tests.csproj -c Release
dotnet publish AURA.csproj -c Release -o publish
dotnet ef database update --connection "<DEPLOYMENT_CONNECTION>"
```

Khi deploy public, ứng dụng phải chạy sau HTTPS/reverse proxy. Container Linux lắng nghe cổng `8080`; health endpoint là `/healthz`. Persistent volume phải tồn tại qua lần restart/redeploy; kiểm tra bằng cách upload một ảnh, restart instance, rồi mở lại link **Xem hóa đơn** trong Audit. Hướng dẫn SmarterASP.NET và các giới hạn Render nằm tại `docs/DEPLOYMENT.md`.

## 6. Checklist trước nộp

- Trang chủ public trả `200`, không yêu cầu tài khoản.
- POST Verify thiếu CSRF trả `400`; nút UI có token và chạy được.
- `/BUSINESS_RULES.md` trả `404` vì policy không được public từ static root.
- Upload giả MIME bị từ chối; JPG/PNG hợp lệ được lưu và mở lại qua route chứng từ.
- Ảnh lớn hơn 5 MB bị chặn với thông báo dễ hiểu, không xuất hiện lỗi `Unexpected end of JSON input`. Giới hạn multipart có phần đệm cho antiforgery/boundary nhưng controller vẫn khóa riêng file ở 5 MB.
- Verify đạt 5/5 trong thời gian demo cho phép. Evidence live candidate hiện hành là 65,004 giây E2E. Timeout cấu hình là 90 giây cho OpenRouter và 600 giây cho Ollama 4B 16K; đây là chốt lỗi kỹ thuật, không phải SLA production.

Chiến lược tách public/local, cấu hình lease cho inference chậm và giới hạn của provider failover nằm tại [`DEPLOYMENT.md`](DEPLOYMENT.md), [`LOCAL_OLLAMA.md`](LOCAL_OLLAMA.md) và [`CURRENT_PROJECT_STATUS.md`](CURRENT_PROJECT_STATUS.md).
- Audit hiển thị input, action, timestamp, reason; chuyển tiếp/Đồng ý/Từ chối/undo hoạt động.
- Ca `ESCALATE_*` xuất hiện ở cửa sổ nhân viên trước; bấm **Chuyển tiếp** rồi mới xuất hiện ở cửa sổ quản lý.
- Nhân viên có thể chuyển từng hồ sơ hoặc **Chuyển tiếp tất cả**; mỗi hồ sơ phải có audit `EMPLOYEE_FORWARDED_TO_MANAGER`.
- Quản lý bấm **Đồng ý duyệt** hoặc **Từ chối duyệt**; kiểm tra toast và audit `MANAGER_YES`/`MANAGER_NO` chứa câu hỏi, câu trả lời và outcome.
- Trạng thái quyết định và audit được lưu cùng một lần EF Core `SaveChanges`, tránh trạng thái đổi nhưng thiếu nhật ký.
- Ảnh POS có MID/TID, ShopID, POS/register hoặc pager/table không được dùng thay mã riêng của giao dịch. Chỉ `VAT_INVOICE` bắt buộc seller tax ID; retail/restaurant/POS receipt có `invoiceNumber`, `receiptNumber` hoặc `transactionReference` hợp lệ có thể không có MST. Ảnh hóa đơn nháp/chưa phát hành phải chuyển FACT.
- Ảnh e-commerce được phép thiếu MST/invoice number nếu có order/booking/tracking/receipt ID cùng trạng thái hoàn tất, ngày giao dịch/thanh toán, merchant, total, currency và line items đáng tin cậy. Ngày giao hàng không tự thay ngày giao dịch; mã vận chuyển không được gọi là MST hay hóa đơn thuế.
- README có hướng dẫn localhost tái lập; Live URL chỉ cần điền vào form/video nếu nhóm tiếp tục công bố môi trường hosting tùy chọn.
- API key/connection string không xuất hiện trong Git hoặc video.
