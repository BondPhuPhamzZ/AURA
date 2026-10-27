# Deploy AURA và xác minh Live URL

> **Mốc hosting:** Chủ dự án xác nhận SmartASP trial hết hạn ngày **20/11/2026**. Sau mốc này, nếu site không còn hoạt động, người thử dùng hướng dẫn clone/local trong README và liên hệ chủ dự án qua kênh riêng để nhận API key. Chưa ghi nhận bản source hiện tại đã publish trên live URL.

> **Giới hạn gói SmartASP hiện tại:** Ảnh control panel ngày 27/10 cho thấy MSSQL 1/1 và 1000/1000 MB quota đã phân bổ. Nút `+ Database` vẫn mở form nhưng không chứng minh còn suất DB thứ hai. Giữ DB thi cũ; không áp migration candidate vào đó chỉ để thay thế staging. [Phương án Azure Free được đối chiếu](FREE_HOSTING_DECISION_2026-10-27.md) là một dự án triển khai riêng, chưa được smoke.

## Phương án giữ SmartASP với một DB — cần làm trước khi publish mới

Site hiện có vẫn dùng được trong thời hạn gói hosting. Không cần mua thêm DB chỉ để
đổi CSS/đưa thêm báo cáo Excel; bản thay đổi này **không thêm migration**. Tuy
nhiên không thể giả định DB live đã có đủ schema của source hiện hành. Các đoạn
hướng dẫn staging DB riêng bên dưới là phương án ưu tiên nếu sau này có quota;
với gói 1/1, áp dụng trình tự kiểm soát tại chỗ này:

1. Trên local, chạy Release build, toàn bộ test, `dotnet ef migrations has-pending-model-changes`
   và một smoke synthetic trên DB cô lập. Ghi lại commit và số migration của source.
2. Trong SmartASP **Websites → đúng site AURA → File Manager**, tải bản website
   hiện tại và thư mục ảnh receipts private về nơi backup ngoài Git. Trong
   **Databases → MSSQL → đúng DB AURA → Backup**, tải `.bak`. Kiểm có thể
   đọc/restore bản sao trên SQL Server tương thích ở môi trường cô lập. Chụp
   lại tên các biến môi trường, nhưng không đưa giá trị key/connection string
   vào Git hoặc ảnh công khai. Nếu chưa chứng minh restore được, chưa thay live.
3. Đọc `__EFMigrationsHistory` trên DB live theo cách read-only và đối chiếu
   với migration trong source. Nếu còn thiếu, tạo script EF **idempotent** từ
   đúng commit, rà soát SQL và thực hiện trong cửa sổ bảo trì sau backup.
   Nếu không có migration thiếu thì **không chạy migration**. Giữ
   `Database__ApplyMigrationsOnStartup=false`; tuyệt đối không dùng recycle để
   tự chạy migration thử trên DB cũ.
4. Trong **Advanced Tools → Pool Manager → Environment Variables**, kiểm đúng
   application pool/site, rồi đặt hoặc cập nhật: `ASPNETCORE_ENVIRONMENT=Production`,
   `ConnectionStrings__DefaultConnection` trỏ **DB AURA hiện tại**,
   `ReceiptStorage__Provider=LocalFileSystem`, `ReceiptStorage__Directory` trỏ
   đường dẫn receipts private/persistent **đang chứa ảnh cũ**, `Vision__Provider=OpenRouter`,
   `Vision__MaxConcurrentRequests=1`, `Vision__FallbackEnabled=false`,
   `OpenRouter__ApiKey` mới, `OpenRouter__Model=qwen/qwen3.8-27b`,
   `OpenRouter__RequireZeroDataRetention=true`,
   `OpenRouter__DenyProviderDataCollection=true`,
   `ExternalImagePrivacy__Mode=ApprovedImagesOnly`,
   `ExternalImagePrivacy__ApprovedHashesFile` trỏ manifest synthetic private
   có thể đọc được, `DriveSandbox__Enabled=false`, `Verify__Enabled=false`,
   `DataRetention__Enabled=false`, `Database__ApplyMigrationsOnStartup=false`
   và `AllowedHosts` là hostname live. Chỉ một worker process; xác minh model
   còn endpoint nhận `zdr=true` bằng smoke synthetic. Health chỉ báo **cấu hình**
   Strict ZDR, không chứng minh hợp đồng lưu trữ dữ liệu của provider.

   **Profile biến cho bản synthetic trên một SmartASP app pool** (giá trị trong ảnh control panel đều bị che, không thể suy ra giá trị đang lưu):

   | Biến hiện có | Giá trị mục tiêu |
   | --- | --- |
   | `ASPNETCORE_ENVIRONMENT` | `Production` |
   | `Vision__Provider` / `Vision__FallbackProvider` | `OpenRouter` / `Ollama` (fallback vẫn tắt) |
   | `Vision__CircuitBreakFailureThreshold` / `Vision__CircuitBreakSeconds` | `3` / `60` |
   | `Vision__MaxConcurrentRequests` / `Vision__ProviderCooldownSeconds` / `Vision__FallbackEnabled` | `1` / `120` / `false` |
   | `OpenRouter__BaseUrl` / `OpenRouter__ChatCompletionsPath` | `https://openrouter.ai/api/v1/` / `chat/completions` |
   | `OpenRouter__Model` / `OpenRouter__ReasoningEffort` | `qwen/qwen3.8-27b` / `none` |
   | `OpenRouter__PolicyPath` / `OpenRouter__TimeoutSeconds` / `OpenRouter__MaxOutputTokens` | `BUSINESS_RULES.md` / `90` / `4096` |
   | `OpenRouter__DenyProviderDataCollection` / `OpenRouter__RequireZeroDataRetention` | `true` / `true` |
   | `ReceiptStorage__MaxFileSizeMb` | `5` |
   | `DecisionPolicy__EscalateDuplicateReceipts` | `false` để giữ baseline regression synthetic; chưa tuyên bố chống thanh toán trùng ở production |
   | `ReceiptProcessing__PollIntervalMs` / `ReceiptProcessing__LeaseSeconds` / `ReceiptProcessing__FailureBackoffMaxSeconds` | `5000` / `600` / `60` |
   | `Verify__Enabled` / `Verify__InterCaseDelayMs` | `false` / `4000` (giá trị delay không dùng khi Verify tắt) |
   | `Database__ApplyMigrationsOnStartup` | `false` |

   Các biến hiện có **không thể điền thay từ ảnh đã che**:

   | Biến | Cách đặt an toàn |
   | --- | --- |
   | `OpenRouter__ApiKey` | Key mới còn hiệu lực, nhập trực tiếp vào pool; không dùng User Secrets local và không gửi qua chat/Git. |
   | `ConnectionStrings__DefaultConnection` | Chuỗi kết nối SQL của **DB AURA hiện có**, không phải LocalDB; giữ đúng tài khoản/quyền và kiểm schema trước publish. |
   | `ReceiptStorage__Directory` | Giữ đường dẫn thư mục ảnh private/persistent **đang chứa ảnh cũ**. Nếu trước đó dùng mặc định `App_Data/receipts`, xác minh thư mục tương ứng trên host; không trỏ vào `wwwroot`. |

   **Thêm/kiểm tra các biến còn thiếu:** `ReceiptStorage__Provider=LocalFileSystem`,
   `Vision__UseSqlDistributedGate=false`, `ExternalImagePrivacy__Mode=ApprovedImagesOnly`,
   `ExternalImagePrivacy__ApprovedHashesFile=<đường dẫn tuyệt đối tới manifest synthetic trên host, ngoài wwwroot>`,
   `DriveSandbox__Enabled=false`, `DataRetention__Enabled=false`, và
   `AllowedHosts=bondphupham-001-site1.ltempurl.com` **nếu đây vẫn là hostname site AURA**.
   Manifest phải được chuyển riêng, đọc được bởi app pool và chứa hash của đúng ảnh synthetic sẽ thử; thiếu hoặc sai file này sẽ chặn khởi động.
   `ReceiptProcessing__MaxOutstandingReceipts=1000` đang có trong `appsettings.json`; chỉ thêm biến khi cần ghi đè rõ ràng.

   **Dọn cấu hình mailbox đã gỡ khỏi source:** `MailboxSandbox__ApiKey`,
   `MailboxSandbox__Provider`, `MailboxSandbox__MailboxId`, `MailboxSandbox__AllowedFolder`,
   `MailboxSandbox__AllowedSenders__0`, `MailboxSandbox__RequireMalwareScanner`,
   `MailboxSandbox__MaxAttachmentsPerMessage`, `MailboxSandbox__Enabled` không còn được đọc.
   Xóa các biến cũ sau khi lưu bản ghi cấu hình riêng an toàn; thu hồi key cũ nếu không dùng ở nơi khác. Không thay bằng biến Gmail/Drive trên Production.

5. Publish package Release vào đúng web root, **giữ nguyên** thư mục receipts,
   `App_Data` và file private; tắt tùy chọn Web Deploy xóa file ngoài package.
   Không upload khóa Drive, manifest vào `wwwroot`, ảnh thật, hoặc dữ liệu test
   local. Recycle pool một lần, kiểm `/healthz` = `ok`, schema up to date,
   Strict ZDR/data-collection flags, storage, provider/model đúng; sau đó thử
   một ảnh synthetic được duyệt, xem SQL/audit, Excel/CSV, Forward/Undo,
   rồi recycle và kiểm ảnh/job còn. Nếu lỗi, dừng tiếp nhận mới và dùng bản
   website/DB/receipts backup theo kế hoạch restore đã diễn tập; không restore
   đè DB đang có ghi mới.

**Giới hạn ảnh thật:** Upload đã nhận và lưu ảnh trước khi gate hash chặn gửi
ra model. Không đổi `ExternalImagePrivacy__Mode` sang `LegacyConfigured` trên
site công khai chỉ vì uploader xác nhận consent. AURA chưa xác thực/phân quyền
người xem và chưa có pre-inference redaction; retention trên DB cũ đang tắt.
Trước pilot dữ liệu thật cần giới hạn truy cập site, chính sách lưu/xóa và
backup, kiểm quyền/consent, đánh giá provider và thử nghiệm phục hồi. Luật bảo
vệ dữ liệu cá nhân cũng đặt trách nhiệm ở bên kiểm soát/xử lý, không chuyển
toàn bộ cho người upload. [Văn bản luật](https://datafiles.chinhphu.vn/cpp/files/vbpq/2025/7/91qh.signed.pdf).

Folder [AURA_INVOICE Restricted](https://drive.google.com/drive/folders/1CozQh8rmg4rz_tJenwhbI1snZSKyYhZU?usp=drive_link)
chỉ là điểm xin quyền demo synthetic. Chủ folder phải **accept/chia sẻ quyền**
cho từng Google account; để upload vào folder chung cần Editor, vốn cũng có thể
sửa/xóa nội dung trong folder, nên chỉ cấp cho người thử đáng tin cậy. Link
không tự cấp quyền. Connector Drive hiện không
chạy trên SmartASP Production.

> **Đối chiếu 27/10:** Evidence mới chứng minh thêm ba fixture Drive local có kết quả/hành động và ba ảnh thật trên web bị chặn trước inference. Chưa có smoke SmartASP hoặc phép đo latency đầu cuối; gate staging riêng và `DriveSandbox__Enabled=false` trên Production vẫn giữ nguyên. [Chi tiết](USER_DRIVE_PRIVACY_CHECK_2026-10-27.md).

> **Mốc lịch sử 25/10/2026, không phải release gate mới:** candidate trên nhánh
> `master` chỉ được chốt cho synthetic local, giữ nguyên
> DB thi cũ. Chưa publish hoặc bật retention trên hosting/DB cũ. Không chạy các
> bước deploy/migration bên dưới cho candidate mới khi chưa có quyết định riêng.
> Xem [checklist hiện hành](CURRENT_ACTION_CHECKLIST.md); các bước/số liệu tiếp
> theo là hướng dẫn và evidence của baseline lịch sử.

## Gate hiện hành trước SmartASP staging — 26/10

Đồng bộ `master` cập nhật source và README, không publish website, migrate SQL
hoặc thay biến môi trường hosting. Bản mặc định cũ được giữ bằng tag
`archive/master-before-hardening-20261026`; tag không phải backup database/ảnh.

Health live read-only ngày 26/10 trả `ok`, Flash, Strict ZDR false, Verify true;
không có commit ID được xác minh. Đây là cấu hình của bản đang chạy, không chứng
minh source mới đã live. Source hiện có 9 migration; `pendingMigrationCount=0`
trên package cũ không chứng minh DB sẵn sàng cho package mới. DB local cũ có 6
migration và vẫn được giữ nguyên theo lựa chọn người dùng.

**Chưa GO cho Drive trên Production:** connector cố ý chỉ chạy Development và
loopback. Không đổi public website sang Development để bật Drive. SmartASP
Windows/IIS hiện là mục tiêu `dotnet publish`; Docker là hướng triển khai riêng,
chưa có container-runtime/SQL evidence và không tự áp dụng trên gói shared này.
Xem [IIS/platform của SmartASP](https://www.smarterasp.net/developers/websites)
và [quy trình IIS của Microsoft](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/?view=aspnetcore-8.0#application-initialization-module-and-idle-timeout).

Để chuẩn bị **manual synthetic staging** mà không đụng DB cũ:

1. Tạo hoặc xác nhận một database test và receipt directory riêng, quyền đọc/ghi
   đúng app pool, ngoài `wwwroot`. Giữ backup SQL + ảnh của site cũ và restore
   thử trên môi trường tách biệt. Nếu không có quota DB/site tách biệt, làm theo
   phương án nâng cấp tại chỗ ở đầu tài liệu; DB cũ không phải staging.
2. Apply migration có kiểm soát vào đúng DB test mới; kiểm đủ schema. Không bật
   startup migration trên live để thử. Package phải có `BUSINESS_RULES.md`,
   `AURA.dll`, `web.config`, `wwwroot`; không có `App_Data`, `TestResults`, `.tmp`,
   `test_kit`, JSON key hoặc secret. Publish probe hiện hành đã kiểm được boundary
   này; probe đầu có 9 raw TestResults JSON nên không dùng artifact đầu.
3. Đặt hash allowlist synthetic ở một đường dẫn private riêng trên host, ngoài
   `wwwroot`; `.tmp` local và service-account key không được ship trong package.
   File UTF-8 array 49 hash có thể tạo bằng launcher local rồi chuyển riêng theo
   runbook. Không thêm hash ảnh thật để vượt gate.
4. Chỉ khi ba bước trên xong, nhập giá trị private trực tiếp trên hosting:

   | Biến | Giá trị cho staging synthetic |
   | --- | --- |
   | `ASPNETCORE_ENVIRONMENT` | `Production` |
   | `ConnectionStrings__DefaultConnection` | Connection string DB test mới; không gửi qua chat/log |
   | `ReceiptStorage__Provider` / `ReceiptStorage__Directory` | `LocalFileSystem` / thư mục private, persistent, riêng cho DB test |
   | `Database__ApplyMigrationsOnStartup` | `false` |
   | `DriveSandbox__Enabled` / `Verify__Enabled` | `false` / `false` |
   | `Vision__Provider` / `Vision__MaxConcurrentRequests` | `OpenRouter` / `1` |
   | `Vision__FallbackEnabled` / `Vision__UseSqlDistributedGate` | `false` / `false`; chỉ một instance |
   | `OpenRouter__ApiKey` | Key hiện hành qua hosting secret UI, không đưa vào package |
   | `OpenRouter__Model` | `qwen/qwen3.8-27b` — route đã được kiểm local |
   | `OpenRouter__RequireZeroDataRetention` / `OpenRouter__DenyProviderDataCollection` | `true` / `true`; đây là request settings |
   | `ExternalImagePrivacy__Mode` | `ApprovedImagesOnly` |
   | `ExternalImagePrivacy__ApprovedHashesFile` | Đường dẫn tuyệt đối tới allowlist private trên host |
   | `AllowedHosts` | Hostname staging thực tế, không copy `127.0.0.1;localhost` lên hosting |
   | `DataRetention__Enabled` | Giữ `false` đến khi rehearsal cleanup/backup trên host được duyệt; không bật trên DB cũ |

   Profile regression đang tắt duplicate escalation để lặp fixture. Khi test
   nghiệp vụ chống chi trùng, chọn rõ `DecisionPolicy__EscalateDuplicateReceipts=true`
   và khóa expected cho ca ảnh trùng trước; không dùng nhãn regression cũ cho ca
   trùng. Không thay flag rồi gọi mọi sai khác route là model lỗi.
5. Hạn chế truy cập staging bằng IP allowlist hoặc authentication của hosting đã
   được xác nhận; AURA chưa có RBAC. Site Guard là WAF, không thay xác thực hay
   phân quyền ([tài liệu hãng](https://www.smarterasp.net/support/kb/a2131/site-guard.aspx)).
   Sau publish/recycle: health + 1 synthetic inference,
   ảnh vẫn đọc được, Forward/manager/Undo/audit, dashboard/CSV/Excel cùng kỳ, invalid
   upload bị chặn và file/job còn nguyên sau recycle. Đo hành vi worker khi idle
   và restart; chưa có test này thì không hứa chạy Drive/worker 24/7. Không chạy
   Verify trên Production và không dùng ảnh thật cho smoke.

UX fragment hiện đã qua Release 377/377, frontend 8/8 và browser/SQL fault checks;
Production local chạy được, không có migration mới. Người dùng chọn chuẩn bị
artifact/hướng dẫn trước. Chưa có thay đổi environment/SQL/publish trên SmartASP;
quyền dữ liệu thật và full Drive Production vẫn là các gate riêng.

## Cập nhật site SmartASP hiện có khi người dùng sẵn sàng

Staging là website/DB/ảnh tách biệt để chạy thử, không phải tên dataset. Có thể
giữ URL cũ khi thay package của cùng website; việc này không tự sinh URL mới.

1. Mở [Customer Login](https://www.smarterasp.net/login), chọn hosting plan rồi
   **Trang web / Websites** → đúng site AURA hiện có. Chưa bấm Publish/Rebuild.
2. Trong **File Manager**, vào đúng web root (ảnh control panel trước đây là
   `/aura`). Backup toàn bộ bản website cũ và thư mục ảnh persistent đang dùng,
   kể cả `App_Data/receipts` nếu đúng cấu hình. Tải về nơi private ngoài Git.
   Trong **Cơ sở dữ liệu / Databases → MSSQL**, chọn DB cũ → **Backup** và tải
   `.bak`; xác minh bản backup/restore riêng trước khi thay live. Không xóa ảnh.
3. Kiểm quota, tạo **DB MSSQL demo mới** để giữ nguyên DB đi thi. Tên do hosting
   cấp có thể có prefix. Mở SQL Studio/SSMS vào đúng DB mới và chạy script
   idempotent của cùng commit trong gói bàn giao; kiểm đủ 9 migration. Không chạy
   script thử trên DB cũ. Nếu không còn quota DB, tiếp tục local và chưa publish.
4. Tạo receipt directory riêng cho DB mới, ngoài `wwwroot`; giữ allowlist 49 hash
   ở đường dẫn tuyệt đối private riêng. File Manager có thể hiển thị đường dẫn ảo,
   nên lấy physical path từ control panel/support thay vì đoán đường dẫn Windows.
   App pool cần đọc allowlist và đọc/ghi receipt directory. Không ship JSON key Drive.
5. Vào **Environment Variables** của đúng website. Dùng bảng cấu hình ở trên,
   nhập API key/connection string trực tiếp vào hosting, giữ `Production`,
   `Database__ApplyMigrationsOnStartup=false`, Drive/Verify off và một worker.
   Thay `AllowedHosts` bằng hostname thực tế. Chưa bật retention trên host.
6. Gói bàn giao có `web.zip` cùng script SQL/allowlist/manifest nằm bên ngoài.
   **Chỉ giải nén nội dung web.zip vào web root**; không upload cả bundle hay để
   SQL/manifest chứa đường dẫn private thành URL công khai. Không xóa folder
   runtime để làm sạch destination. Nếu dùng Visual Studio/Web Deploy, tắt
   **Remove additional files at destination** và không tự publish database.
7. Publish trong cửa sổ bảo trì rồi **Recycle application pool** đúng site.
   Hard refresh một lần để nhận JS/CSS mới. Kiểm health: `ok`, model đúng route,
   Strict-ZDR request true, `ApprovedImagesOnly`, 49 hash, Drive/Verify false,
   pending migration 0. `aiConfigured=true` chỉ chứng minh có cấu hình key.
8. Chạy một synthetic inference được duyệt, rồi Forward/quản lý/Undo, audit,
   dashboard/CSV cùng kỳ và một ảnh không được duyệt (dừng trước inference).
   Kiểm riêng GET tới private paths không trả file. Recycle và kiểm lại job/ảnh.
   Thử hai tab/lỗi mạng bằng DB demo; không mang fault proxy lên hosting.
9. Kiểm chính sách idle/recycle/Always Running của gói thực tế; không suy từ local
   rằng worker sẽ chạy 24/7. Nếu lỗi, giữ evidence, đưa lại package + environment
   của bản cũ và trỏ lại DB/receipt directory cũ; không restore đè DB đang ghi.

**Demo Drive:** upload ảnh synthetic đã duyệt trực tiếp vào folder Restricted đã
cấu hình, chạy launcher local `-EnableSyntheticDrive` theo runbook và xem Audit/
Dashboard tự cập nhật. Service account chỉ cần Viewer. Người khác chỉ upload được
khi được chủ folder chia sẻ quyền phù hợp cho đúng email; một link Restricted
không cấp quyền. Không đổi sang Anyone-with-link để vượt setup; README chỉ
công bố link xin quyền theo quyết định của chủ folder. Luồng Drive tới live SmartASP cần gate triển khai
connector riêng; bản UX này không mở gate đó.

> Source 21/10/2026: Verify Harness mặc định tắt và **không thể chạy trên Production**, kể cả khi hosting còn `Verify__Enabled=true`. Cần ít nhất **196/196 tests** trước publish. Bản hardening chưa được smoke live; xem [báo cáo gate](POST_CONTEST_HARDENING_2026-10-21.md). Các số live cũ bên dưới là evidence lịch sử.

Live URL đã dùng để smoke Sprint 1: `https://bondphupham-001-site1.ltempurl.com/`. BTC xác nhận phần trình diễn Chung kết chạy trên laptop cá nhân, nhưng brief vẫn yêu cầu đường truy cập công khai cho bản bàn giao. Vì vậy localhost là đường demo chính; Live URL là cổng bàn giao riêng và phải được smoke lại sau mỗi bản hardening. Hosting trial có thể chậm hoặc gián đoạn nên không dùng nó làm đường trình diễn duy nhất. Baseline và ranh giới vận hành hiện hành nằm tại [`CURRENT_PROJECT_STATUS.md`](CURRENT_PROJECT_STATUS.md).

## Trang index mặc định của SmarterASP.NET

Nếu cùng một URL lúc hiện AURA, lúc hiện trang `Dear customer`, website root vẫn còn `index.html` mặc định hoặc trình duyệt đang giữ bản cache của file đó. File này không thuộc AURA. Cách an toàn là đổi tên thành `index.smarterasp-backup.html`, recycle pool, kiểm tra bằng cửa sổ ẩn danh rồi mới xóa hẳn. Không xóa `web.config`, `AURA.dll`, `wwwroot` hoặc `App_Data`.

Sau khi publish, website root phải chứa trực tiếp `web.config`, `AURA.dll`, `AURA.deps.json`, `AURA.runtimeconfig.json` và `wwwroot/`. Không để các file này nằm thêm một cấp trong thư mục `publish/`.

Luôn publish vào một thư mục mới nằm ngoài project. Không dùng `-o .\publish`: output cũ nằm trong project có thể bị MSBuild thu lại làm input và tạo `publish\publish` hoặc đường dẫn stale. Ví dụ PowerShell an toàn:

```powershell
$shortCommit = (git rev-parse --short HEAD).Trim()
$publishRoot = Join-Path ([System.IO.Path]::GetTempPath()) "AURA-$shortCommit"
if (Test-Path -LiteralPath $publishRoot) { throw "Artifact đã tồn tại: $publishRoot. Hãy dùng tên mới; không ghi đè artifact đã kiểm thử." }
New-Item -ItemType Directory -Path $publishRoot | Out-Null
dotnet publish .\AURA.csproj -c Release -o $publishRoot --no-restore
```

Artifact production không được có thư mục `publish`, `.tmp`, `test_kit` hoặc `appsettings.Development.json`. Verify Harness vẫn lấy đúng năm fixture tổng hợp trong `wwwroot/test_data`.

## Thứ tự smoke test production

1. Mở `/healthz` trong cửa sổ ẩn danh và xác nhận HTTP 200, `status: ok`, `aiConfigured: true`, `policyAvailable: true`, `databaseAvailable: true`, `databaseUpToDate: true`, `pendingMigrationCount: 0` và `storageAvailable: true`.
2. Mở `/`, chuyển qua các tab Nhân viên, Quản lý và Lịch sử; xác nhận không còn trang `index.html` mặc định.
3. Upload đúng một ảnh tổng hợp nhỏ, kiểm preview, facts AI, quyết định và một request tương ứng trong OpenRouter Activity.
4. Với một hồ sơ `ESCALATE_*`, bấm chuyển tiếp, mở tab Quản lý, ra quyết định và xác nhận Audit Log chỉ hiển thị một hồ sơ với timeline nhất quán.
5. Recycle pool, mở lại hồ sơ/ảnh vừa tạo để kiểm tra SQL và thư mục ảnh không mất dữ liệu.
6. Xác nhận Verify Harness **không hiển thị**, `/healthz.verifyHarnessEnabled=false`. Nếu cần đo lại fixture, chạy trên Development cô lập, không trên public Production. Mốc dưới 90 giây của bản cũ không phải SLA.
7. Kiểm tra lại trên điện thoại hoặc mạng 4G, sau đó mới chia sẻ Live URL.

Cập nhật: 29/09/2026. Phương án ngắn hạn đang dùng là **SmarterASP.NET 60-day trial**, vì AURA dùng ASP.NET Core 8 + SQL Server và cần lưu ảnh hóa đơn bền. Render Free chỉ nên dùng làm preview stateless.

## 1. Chuẩn bị local

1. Bảo đảm nhánh nộp đã push lên GitHub và không có secret trong Git.
2. Chạy:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Test-DemoReadiness.ps1" -StartLocalDb -SkipHttp -DiagnoseLocalDb -OutputPath ".\deployment_evidence\preflight-before-app.txt"
dotnet build --no-restore
dotnet test tests/AURA.Tests/AURA.Tests.csproj --no-restore
```

Kỳ vọng source 21/10: preflight trả `READY: 0 failures`, Release build 0 warning/error, ít nhất 196/196 test pass và `dotnet ef migrations has-pending-model-changes --no-build` không có model change. Kiểm dependency vulnerability cho app/test project, rồi smoke `/healthz` và một fixture synthetic sau deploy. Offline gate không gọi AI và không chứng minh route Strict ZDR hay khả năng phục vụ người dùng thật.

3. Trong Visual Studio, mở `AURA.csproj` và chọn **Publish → Folder** hoặc **Publish → Web Deploy**. Target framework là `net8.0`, cấu hình `Release`.

## 2. Tạo trial SmarterASP.NET (hướng dẫn lịch sử cho tài khoản mới)

Tài khoản AURA hiện có đã dùng hết 1/1 suất MSSQL. Bỏ qua bước tạo DB mới
bên dưới khi cập nhật website hiện tại; dùng checklist tại đầu tài liệu.

1. Đăng ký tại `https://www.smarterasp.net/free_trial` bằng tài khoản của nhóm.
2. Từ màn hình **Các gói hosting**, tại thẻ `bondphupham-001` / plan `W60-US`, bấm nút xanh **Quản lý**. Không cần bấm `+ Đơn hàng mới`, `Kế hoạch nâng cấp`, `Tên miền`, `VPN` hoặc `MCP/API`.
3. Trong Hosting Control Panel vừa mở, vào **Websites**. Chọn website `aura`/temporary site rồi bấm **Manage Website**. Ghi lại Temporary URL và đặt website type/version là ASP.NET Core tương thích .NET 8.
4. Vào **Databases → MSSQL → + Add Database**, chọn SQL Server version còn được gói trial hỗ trợ, đặt tên database và mật khẩu riêng rồi copy connection string. Không chụp/commit mật khẩu.
5. Kiểm tra hosting plan đã bật ASP.NET Core 8 và application pool 64-bit. Nếu runtime không có, mở ticket support trước khi deploy.
6. Trong Pool Manager, đặt **Maximum Worker Processes = 1** cho baseline demo. Limiter/cooldown hiện nằm trong memory từng process.

## 3. Cấu hình biến môi trường (baseline lịch sử)

Bảng bên dưới ghi lại cấu hình ban đầu; với source hiện tại, dùng bảng cấu
hình trong **Phương án giữ SmartASP với một DB** ở đầu tài liệu, nhất là model
và Strict ZDR. Không thay các biến mới bằng giá trị lịch sử bên dưới.

Trong SmarterASP Control Panel V10, mở **Advanced Tools → Pool Manager → Actions → Environment Variables**. Biến được đặt ở application-pool level; dùng tên duy nhất nếu pool chứa nhiều site.

Thêm:

```text
ASPNETCORE_ENVIRONMENT=Production
Vision__Provider=OpenRouter
Vision__FallbackEnabled=false
Vision__FallbackProvider=Ollama
Vision__MaxConcurrentRequests=1
Vision__ProviderCooldownSeconds=120
Verify__Enabled=false
OpenRouter__ApiKey=<OPENROUTER_KEY_MỚI>
OpenRouter__Model=qwen/qwen3.8-flash
OpenRouter__ReasoningEffort=none
ConnectionStrings__DefaultConnection=<SQL_SERVER_CONNECTION_STRING>
Database__ApplyMigrationsOnStartup=false
ReceiptStorage__Provider=LocalFileSystem
ReceiptStorage__Directory=App_Data/receipts
ReceiptStorage__MaxFileSizeMb=5
```

Giữ `Database__ApplyMigrationsOnStartup=false` mặc định. Chỉ bật trong một cửa sổ migration có backup đã xác minh, kế hoạch rollback và người vận hành theo dõi; recycle pool sau khi migration hoàn tất. Không đặt key vào `appsettings.json`, publish profile hoặc ảnh/video.

### Shared GCS storage (not yet approved for live use)

Source có adapter tùy chọn, nhưng giữ `ReceiptStorage__Provider=LocalFileSystem` cho tới khi bucket test được tạo private, bật uniform bucket-level access và public access prevention, cấp identity riêng quyền tối thiểu ở bucket, rồi hoàn thành [runbook kiểm thử shared storage](SHARED_RECEIPT_STORAGE.md). Không dùng Google Drive folder làm object storage hay tái dùng Drive read-only service account. Chưa có bucket key/environment variables cần thêm vào SmarterASP ở thời điểm này; không chuyển live host sang GCS chỉ bằng cách đổi provider.

Khi các gate staging đã qua, các khóa cấu hình (không chứa credential value) là:

```text
ReceiptStorage__Provider=GoogleCloudStorage
ReceiptStorage__GoogleCloudStorage__Bucket=<private bucket name>
ReceiptStorage__GoogleCloudStorage__Prefix=aura/receipts
ReceiptStorage__GoogleCloudStorage__ServiceAccountJsonPath=<absolute external credential path>
```

## 4. Publish bằng Web Deploy

1. Trong SmarterASP control panel, mở thông tin **Web Deploy** và tải/copy profile.
2. Visual Studio → **Publish → Import Profile**, chọn profile vừa tải.
3. Chọn **Settings**:
   - Configuration: `Release`;
   - Target Framework: `net8.0`;
   - Deployment mode: Framework-dependent;
   - không xóa file trong `App_Data/receipts` khi publish lại.
4. Bấm **Validate Connection**, sau đó **Publish**.
5. Nếu dùng Folder publish thay vì Web Deploy, upload toàn bộ nội dung thư mục publish bằng File Manager/FTP vào web root, không upload source tree.

## 5. Xác minh ngay sau deploy

Thực hiện theo thứ tự để không tốn quota vô ích:

1. Mở `https://<live-url>/healthz`, kỳ vọng HTTP 200, JSON `status: ok`, model `qwen/qwen3.8-flash`, `reasoningEffort=none`, `databaseAvailable=true`, `databaseUpToDate=true`, `pendingMigrationCount=0` và `storageAvailable=true`. Endpoint không gọi AI provider nên vẫn cần ảnh smoke test riêng.
2. Mở trang chủ ở cửa sổ ẩn danh, kiểm CSS/JS/ba tab.
3. Mở tab quản lý và lịch sử để xác nhận SQL Server/migration hoạt động.
4. Upload một ảnh smoke tổng hợp; xác nhận HTTP `202`, audit `AI_QUEUED`, rồi trạng thái `COMPLETED/FAILED`. Refresh giữa lúc xử lý không được làm mất job.
5. Mở lại ảnh từ lịch sử, recycle app pool rồi mở lại lần nữa để xác minh file tồn tại.
6. Xác nhận `/healthz.verifyHarnessEnabled=false` và UI không có nút Verify. Lượt live 12/10 đạt 5/5 trong 65,004 giây trên bản cũ; Production mới không cho chạy lại Verify. Nếu cần benchmark, dùng Development cô lập và ghi rõ thời điểm/cost.
7. Đổi migration flag về `false`, recycle pool và kiểm `/healthz` lần cuối.

## 6. Xử lý lỗi thường gặp

| Hiện tượng | Kiểm tra |
|---|---|
| 500.30/502.5 | Runtime .NET 8, startup log tạm thời, connection string và environment variables |
| Database login failed | server/database/user/password, firewall/provider connection string |
| `AI_NOT_CONFIGURED`/401 | `OpenRouter__ApiKey` ở đúng app pool, recycle sau khi sửa |
| `POLICY_NOT_FOUND` hoặc `policyAvailable: false` | Publish lại bản mới có `BUSINESS_RULES.md` cạnh `AURA.dll`; không chỉ upload riêng DLL |
| 404 ảnh sau recycle | quyền ghi `App_Data/receipts` và publish không xóa folder |
| 429/provider capacity | mở OpenRouter **Upstream Requests**, ghi provider/status/request ID; chờ cooldown; không tạo key mới để né limit |
| App chạy nhưng redirect HTTPS lỗi | giữ URL HTTPS do host cấp và kiểm proxy/header; không hard-code domain |

## Thay API key OpenRouter đã hết hạn

Key `btc_key_test` hết hạn không thể được “gia hạn” bằng cách đổi tên. Trong [OpenRouter Keys](https://openrouter.ai/settings/keys), tạo **key riêng mới** cho AURA, đặt tên có môi trường, giới hạn chi tiêu và ngày hết hạn phù hợp. Lưu giá trị bí mật ngay lúc tạo vào trình quản lý bí mật của bạn; không gửi qua chat, ảnh chụp, Git, tài liệu, `appsettings.json` hoặc script. Không dùng `Default key` dùng chung để né giới hạn của key demo. Nếu cần sao chép cấu hình thì chỉ sao chép model/guardrails, không sao chép key cũ.

1. **Local Development:** trong thư mục `AURA\AURA`, chạy `dotnet user-secrets set "OpenRouter:ApiKey" "<NEW_PRIVATE_KEY>"` tại PowerShell riêng không ghi hình; đóng terminal sau thao tác. Chỉ đặt secret, không sửa source. Tránh `dotnet user-secrets list` khi chia sẻ màn hình vì có thể in giá trị.
2. **Hosting:** trong biến môi trường của đúng website/app pool, **thay giá trị** `OpenRouter__ApiKey` bằng key mới; giữ nguyên `OpenRouter__Model`, `ReasoningEffort`, `Vision__MaxConcurrentRequests=1`, `Vision__FallbackEnabled=false`, `Verify__Enabled=false`, `DriveSandbox__Enabled=false` và các biến database/storage đang hợp lệ. Lưu rồi recycle pool. Không cần publish lại DLL chỉ để đổi key; nếu có publish source khác, kiểm đúng commit và migration riêng.
3. **Xác minh:** kiểm `/healthz` cho database/storage/migration, nhưng `aiConfigured=true` chỉ chứng minh key có giá trị cấu hình. Dùng **một hóa đơn tổng hợp** tại localhost hoặc host được cho phép, chờ trạng thái cuối, kiểm không có `AI_AUTH_FAILED`/401/403/429, audit và model route. Không dùng ảnh thật để thử key. Nếu thất bại, giữ các job đã lưu và dừng gửi thêm; kiểm hạn mức, quyền, model, route và log đã loại secret.
4. **Thu hồi:** sau khi key mới chạy thành công ở từng môi trường, xóa/revoke key đã hết hạn hoặc không dùng trong dashboard và ghi ngày xoay vòng trong hồ sơ vận hành riêng. Không tạo nhiều key để vượt provider rate limit. Nếu key từng bị lộ, revoke ngay thay vì chờ hết hạn.

`data_collection=deny` và `OpenRouter__RequireZeroDataRetention=false` là hai điều kiện khác nhau. Không bật Strict ZDR chỉ từ trạng thái `/healthz`; cần xác minh route/model Alibaba còn endpoint hợp lệ với `zdr=true`, kiểm một synthetic request và có rollback. Key mới tự nó không chứng minh quyền riêng tư hoặc sửa các gate production.

Chỉ bật stdout startup log khi cần chẩn đoán, tải log về rồi tắt lại để tránh đầy storage hoặc lộ cấu hình.

Hosted deployment không thể gọi Ollama trên laptop qua `127.0.0.1`; loopback khi đó là chính server hosting. Chỉ bật fallback Ollama nếu model chạy trên cùng server hoặc một endpoint HTTPS riêng đã được bảo vệ. Bản demo trên máy cá nhân có thể bật fallback vì ASP.NET và Ollama cùng máy.

Profile local đã pass synthetic gate dùng timeout 600 giây, context 16384, output 4096, keep-alive 30 phút và `Think=false`. Vì đường fallback có thể chạy sau timeout primary rồi còn semantic repair, đặt riêng `ReceiptProcessing__LeaseSeconds=1800` khi dùng local contingency; không thay lease public baseline nếu không cần. Chi tiết cấu hình và rollback xem [`LOCAL_OLLAMA.md`](LOCAL_OLLAMA.md) và [`RUNBOOK.md`](RUNBOOK.md).

Implementation hiện tại cho phép `Ollama:BaseUrl` là HTTPS từ xa nhưng chưa có cấu hình bearer/API-key riêng cho Ollama. Vì vậy **không public trực tiếp cổng 11434** và không trỏ production AURA tới một endpoint Ollama Internet chưa có lớp bảo vệ. POC GPU phải dùng private network/VPN hoặc reverse proxy có TLS, xác thực, rate limit và log đã che dữ liệu; nếu proxy bắt bearer token thì cần bổ sung hỗ trợ header trong app rồi regression lại trước khi dùng.

Drive synthetic sandbox ngày 22/10 chỉ chạy trong Development, mặc định tắt;
không đưa service-account JSON key lên hosting và không bật DriveSandbox__Enabled
trên Production. Chi tiết setup local ở [DRIVE_SANDBOX_LOCAL.md](DRIVE_SANDBOX_LOCAL.md).
Khi chạy EF với `--no-build`, luôn chỉ rõ `--configuration Release` nếu vừa build
Release: Debug assembly cũ trên máy phát triển từng chứa migration mailbox đã gỡ.

## 7. Vì sao không chọn Render Free cho bản nộp

Render chạy được .NET qua Dockerfile và cấp URL `onrender.com`, nhưng Free Web Service:

- spin down sau 15 phút không hoạt động và có thể mất khoảng một phút để wake;
- dùng filesystem tạm, mất ảnh upload khi restart/redeploy/spin-down;
- không gắn persistent disk ở gói Free;
- Render Postgres không dùng trực tiếp với provider EF SQL Server hiện tại và free database hết hạn sau 30 ngày.

Muốn dùng Render ổn định phải trả phí Web Service + persistent disk và dùng SQL Server bên ngoài, hoặc đổi code sang PostgreSQL/object storage. Đây là thay đổi kiến trúc không nên làm sát deadline. Tài liệu chính thức: `https://render.com/docs/free`, `https://render.com/docs/docker`, `https://render.com/docs/disks`.

## 8. Điều kiện bản nộp lịch sử (đọc cùng gate hiện hành)

- Live URL HTTPS mở được ở cửa sổ ẩn danh, không cần login.
- `/healthz` trả 200.
- SQL migration hoàn tất và `pendingMigrationCount=0`; ba tab đọc được dữ liệu.
- Ảnh vẫn mở sau recycle/redeploy kiểm soát.
- Không có secret trong Git, log, slide hoặc video.
- Một ảnh smoke hoạt động; Verify chỉ kiểm ở Development cô lập, Production phải bị khóa. Lỗi provider phải fail-safe chứ không giả PASS.
- URL phải được điền đồng nhất vào README, video description và form nộp sau khi bản hardening được smoke lại; không dùng trạng thái Sprint 1 cũ để suy ra bản hiện tại đã sẵn sàng.
