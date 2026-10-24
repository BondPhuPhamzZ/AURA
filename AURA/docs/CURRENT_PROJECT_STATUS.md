# Trạng thái hiện hành AURA ngày 24/10/2026

> **Manual upload idempotency 24/10 — commit `daab7d4`:** Endpoint nhận
> `Idempotency-Key` tùy chọn theo UUID chuẩn; UI giữ tạm một khóa theo SHA-256
> trong `sessionStorage` đến khi server xác nhận, tái sử dụng qua reload cùng
> tab và tối đa ba lần thử khi mất kết nối, HTTP 429 hoặc 5xx. LocalDB
> integration chạy hai intake instance/DbContext với cùng khóa manual và xác
> nhận đúng một row, audit và file.
> Cùng khóa/cùng SHA-256 trả lại hồ sơ đã lưu; cùng khóa/ảnh khác bị từ chối.
> Thêm test kiểm khóa, retry, xung đột nội dung, SQL unique-conflict, và hợp
> đồng UI. Full Debug/Release pass **311/311** mỗi cấu hình với
> `AURA_RUN_LOCALDB_INTEGRATION=1`; migration không đổi; `git diff --check`
> sạch. Thay đổi đã push lên `codex/drive-pipeline-hardening`; khóa chưa xác nhận
> sẽ mất khi browser session kết thúc.

> **Queue monitoring 24/10:** Snapshot mỗi phút ghi aggregate queue counts,
> tuổi pending lâu nhất và tổng byte được SQL ghi nhận; log Warning theo trạng
> thái khi queue đạt 80% configured capacity, pending age vượt ngưỡng (mặc định
> 300 giây) hoặc có failed mới kể từ khi monitor khởi động. Unit tests xác nhận
> ngưỡng capacity/age; LocalDB test xác nhận projection và recorded bytes.
> Đây là structured application logging, chưa phải alert pipeline. Byte sum
> không bao gồm orphan/temp objects và không thay cho số liệu bucket thực;
> cost vẫn chỉ ghi theo response, chưa có budget/cumulative alert.

> **Cập nhật tiếp nối 24/10 — source intake write xác nhận bền vững:** Khi
> xử lý `DbUpdateException` sau khi tạo receipt nguồn, `ReceiptIntakeService`
> nay dùng `GetPersistedRequestByIdAsync` (`AsNoTracking`) để xác nhận job đã
> thật sự có trong SQL; không dùng `FindAsync`, vốn có thể trả entity `Added`
> còn trong change tracker dù transaction vừa rollback. Nếu không có row bền
> vững hoặc hash không khớp, lỗi được ném lại; object nguồn vẫn được giữ để
> retry/reconciliation. Regression unit test mô phỏng đúng entity còn tracked
> nhưng chưa persist. SQL integration xác nhận transaction rollback fail-safe
> và hai intake instance đọc cùng source key song song chỉ tạo một request,
> một audit và một private file. Focused suite 29/29; full Debug và Release mỗi
> lượt 300/300; Release build 0 warning/error, EF không pending model change và
> `git diff --check` pass. SQL-trigger integration test đã chạy trên LocalDB
> thật: audit-trigger làm transaction rollback, intake ném lỗi thay vì trả
> phantom success; retry bằng scope mới tạo đúng một request và một audit.
> Full Debug/Release chạy với `AURA_RUN_LOCALDB_INTEGRATION=1`, toàn bộ 18
> integration cases được bật (300/300, không skip). Sandbox process ban đầu
> không truy cập được automatic instance; sau khi xác nhận đúng instance
> `MSSQLLocalDB` và chạy test dưới user sở hữu instance, integration pass.
> Chỉ database test tên ngẫu nhiên được tạo/xóa; không chạm DB ứng dụng.

> **Privacy request invariant 24/10:** Nếu `OpenRouter__RequireZeroDataRetention=true`, AURA luôn gửi `provider.data_collection="deny"` kể cả khi biến deny riêng false; `/healthz` báo cùng giá trị hiệu lực thay vì chỉ phản ánh biến riêng. Regression test xác nhận payload dưới cấu hình mâu thuẫn. Full Debug/Release **300/300** với 18 LocalDB integration cases bật, Release build 0 warning/error, EF không pending model change. Đây chỉ là tính nhất quán của yêu cầu routing AURA; không chứng minh model/provider hỗ trợ ZDR.

> **Controlled live-provider smoke 24/10:** Một fixture synthetic `TC-01` chạy qua manual intake, durable queue, OpenRouter `qwen/qwen3.8-flash`, semantic validation và policy trên LocalDB tạm mới. HTTP 202; provider HTTP 200; `COMPLETED`, `AUTO_APPROVE` đúng nhãn; số khai báo và tổng in 295.199 VND khớp; 6,659 ms (6.659 giây); không fallback/provider error; semantic repair không cần kích hoạt. `/healthz=ok`, schema up-to-date. Smoke dùng `data_collection=deny`, nhưng `strictZeroDataRetention=false`, `sqlDistributedAiGateEnabled=false`; ảnh và DB thử nghiệm đã được dọn. Facts có mã vận chuyển nhưng privacy triage là `NONE`, nên bộ phân loại sau inference không phải detector đầy đủ cho định danh có thể liên kết. Đây chỉ là một ca synthetic, không chứng minh accuracy/repeatability hay quyền gửi chứng từ thật. Chi tiết: [controlled model smoke evidence](MODEL_SMOKE_2026-10-24.md).

> **Strict-ZDR route check 24/10:** OpenRouter model endpoint API hiện liệt kê 2 endpoint Alibaba cho `qwen/qwen3.8-flash` nhưng không endpoint nào trong danh sách ZDR. Cùng một synthetic smoke với `qwen/qwen3.8-27b`, `RequireZeroDataRetention=true` và cờ deny riêng false chạy 2 lần, cả hai HTTP 200, health `deny=true`/`strictZDR=true`, `AUTO_APPROVE` đúng nhãn (7.685 và 7.631 ms), không fallback/lỗi. Sau khi telemetry an toàn được bổ sung, response metadata ghi host thực tế DekaLLM (không phải Alibaba). Đây chỉ là hai kết quả cùng một fixture, không benchmark accuracy hay đổi model mặc định. Kiểm tra endpoint/model Alibaba cho các model Qwen đa phương thức có ZDR không tìm thấy Alibaba-hosted ZDR endpoint; xem [metadata ZDR hiện hành](OPENROUTER_ZDR_ROUTE_CHECK_2026-10-24.md) và [evidence smoke](MODEL_SMOKE_2026-10-24.md).

> **Qwen Strict-ZDR candidate v3.1 regression 24/10:** `qwen/qwen3.8-27b` chạy ba lần đủ 15 ca synthetic, cùng manifest hash, trên LocalDB tạm mới mỗi lượt: decision **15/15, 14/15, 15/15**; tổng 44/45; 0 missed escalation, 1/15 routine over-escalated ở một lượt, 0 lỗi hệ thống; facts exact pooled 405/438 (92,47%). R3-04 dao động do OCR “Bìa hồ sơ” thành “Bia hò sơ”; policy đã được cập nhật ngoại lệ hẹp cho đúng biến thể sai dấu và có regression test, trong khi `bia chai` vẫn escalation. Benchmark 44/45 là trước sửa và chưa chạy lại model live. R3-06 cố ý che tổng cuối; Semantic không thể khôi phục chứng cứ in và giữ `ESCALATE_FACT` cả ba lượt (fail-safe). P50/P95 E2E theo lượt lần lượt 7.224/16.951s, 5.177/31.465s, 5.670/14.396s. ZDR và data-collection deny bật; SQL distributed gate tắt; chỉ endpoint synthetic, không scale/host/ảnh thật. Giữ candidate khỏi model mặc định; chi tiết [comparison](MODEL_ROUTE_COMPARISON_2026-10-24.md). Raw cases ở local ignored `TestResults`, không commit.

> **Storage hash admission check:** LocalFileSystem và GCS adapter nay tự xác minh SHA-256 của bytes trước khi persist/upload; sai hoặc malformed hash bị từ chối trước I/O. Unit tests xác nhận không tạo file local hay gọi GCS client khi hash không khớp. Debug/Release full suite hiện pass **307/307** mỗi bản với LocalDB integration bật.

> **GCS staging smoke guard:** `/healthz` báo provider lưu trữ đã cấu hình; `Invoke-PrivateReceiptSmoke.ps1` có thể bắt buộc Google Cloud Storage và Strict ZDR, đồng thời luôn kiểm tra local loopback, health/schema, deny hiệu lực, fallback off và concurrency=1 trước khi upload. Đây là guard cấu hình cho smoke synthetic, chưa có bucket thật nên chưa xác nhận GCS/IAM.

> **Policy OCR regression correction:** Thêm biến thể stationery chính xác `bia hò sơ` sau phân tích R3-04; unit tests xác nhận sai dấu không tạo policy false positive nhưng cụm `bia chai` vẫn bị escalation. Debug/Release đều pass **307/307** với `AURA_RUN_LOCALDB_INTEGRATION=1` trong môi trường hỗ trợ LocalDB; SQL integration không skip. Benchmark model trước sửa vẫn 44/45 và chưa được chạy lại live sau thay đổi policy. Chi tiết và giới hạn: [candidate regression report](MODEL_ROUTE_COMPARISON_2026-10-24.md).

> **SQL intake backpressure:** Thêm trần chung cấu hình được cho số receipt `PENDING` + `PROCESSING`, mặc định 1.000 và giới hạn cấu hình 1–10.000. SQL application lock transaction-owned đặt quanh phép đếm và ghi job/audit để các instance cùng dùng một SQL database không vượt trần; HTTP upload nhận 503/`Retry-After` khi đầy. Manual upload bị từ chối sẽ dọn object mới tạo; Drive source-key object được giữ để lần poll sau retry. Test race hai repository độc lập chứng minh ceiling=1 không bị vượt. Đây chỉ giới hạn hàng đợi chưa terminal, không giới hạn tổng ảnh đã lưu, chi phí, tốc độ giữa nhiều instance, hay chống abuse/thiếu đăng nhập.

> **Earlier isolated app-smoke evidence 24/10:** Full Debug and Release suites
> at that checkpoint passed **297/297 with all 16 SQL LocalDB integration cases enabled**
> (`AURA_RUN_LOCALDB_INTEGRATION=1`, isolated temporary databases); Release
> build has 0 warnings/errors; EF
> reports no pending model changes; `git diff --check` is clean. New coverage
> connects actual intake + SQL persistence to a worker with a second storage
> adapter over one shared fake GCS client. The first LocalDB attempt failed
> because the user-owned instance was stopped and sandbox registry access was
> restricted; inspecting outside the sandbox confirmed it was `Stopped`, then
> starting the existing instance allowed both full suites to pass. Localhost
> `http://127.0.0.1:5099/healthz` then returned HTTP 200 / `status=ok` against
> a newly migrated, empty smoke DB; DB/schema/storage were available, queue
> empty, Drive/Verify off, concurrency 1, SQL AI gate off. No AI request or
> receipt upload occurred; `strictZeroDataRetention=false`. The app was stopped
> and only that isolated DB/temp folder was removed. Port 5097 remains stopped.
> This is fake-GCS integration evidence, not a real bucket or separate-machine test.

> **Stale upload staging cleanup 24/10:** LocalFileSystem now periodically
> removes only filenames matching AURA's generated `.32hex.(jpg|jpeg|png).32hex.tmp`
> pattern when older than the default 24 hours. It first opens each candidate
> exclusively, so active writers are skipped; normal receipt files, recent temp
> files, and unrelated `.tmp` files are untouched. Cleanup is bounded to 500
> candidates per pass (every 60 minutes by default) and logs aggregate counts only. It does not delete uploaded
> receipts or change their retention; GCS object cleanup is not implemented.
> Isolated runtime smoke produced `deleted=1; skipped=0`, removed one synthetic
> stale staging file, and returned HTTP 200 / `status=ok`; queue was empty and
> the test database/folder were removed afterward.

> **GCS credential mode hardening 24/10:** GCS storage configuration can now
> explicitly select `ApplicationDefault` or `ServiceAccountJson`; JSON mode
> remains the default for backward compatibility. ADC mode requires an empty
> explicit key path; an explicit `GOOGLE_APPLICATION_CREDENTIALS` file, when
> set, must exist outside app content root. JSON mode also requires an existing
> absolute key file outside app root, and no credential is required while
> LocalFileSystem is selected. This validates configuration policy only; ADC identity discovery,
> impersonation, actual IAM and bucket access have not been exercised. Explicit
> `GOOGLE_APPLICATION_CREDENTIALS` files are also rejected when missing or inside
> app root. Full Debug/Release suites: **297/297 each**, all 16 LocalDB cases enabled;
> Release build 0 warnings/errors; EF no pending model changes.

> **Shared receipt storage adapter 24/10 (candidate, not live):** Receipt intake,
> worker inference and receipt serving now depend on `IReceiptFileStore`, with
> local-file default and opt-in private GCS adapter. Cloud creation uses a
> generation-match-zero precondition; retries reuse only exact size/SHA matches;
> reads are generation-pinned and verified against SQL before inference. The
> inference API now accepts verified bytes, so cloud images do not need a
> temporary local copy. Health checks probe storage. No GCS bucket, IAM grant,
> key or hosting setting has been provisioned/changed; GCS integration has not
> been exercised against a real bucket. Keep `LocalFileSystem` until isolated
> authenticated integration, two-host, backup/restore and failure tests pass.
> Retention cleanup is deliberately not implemented: never apply one-day
> lifecycle deletion while jobs may be pending or retryable. Local fake-client
> GCS tests cover create-only writes, matching/conflicting retries, generation-
> pinned reads, pre-download size rejection and a download exceeding its own
> advertised size. Verification after this code change: Debug and Release
> **297/297**, opt-in LocalDB integration
> latest LocalDB run **16/16**, Release build **0 warnings/errors**,
> EF reports no pending model changes, and NuGet online audit reports no
> vulnerable packages in app or tests. These are local/unit + LocalDB results,
> not authenticated real-GCS or multi-host evidence. See
> [SHARED_RECEIPT_STORAGE.md](SHARED_RECEIPT_STORAGE.md).

> **Stable-source object cleanup race 24/10:** Nếu Drive intake đã ghi object
> nhưng database lookup lỗi trước khi bắt đầu insert, source-key object nay
> được giữ lại để retry/reconciliation; không xóa bytes mà poller khác có thể
> đang tái sử dụng. Manual upload có random ID mới vẫn cleanup object vừa tạo
> khi SQL chưa bắt đầu. Fault-injection test xác nhận retry hoàn tất cùng object;
> Debug/Release **291/291**, LocalDB opt-in **15/15**, Release build và EF checks
> vẫn sạch. Đây không thay thế orphan sweeper; cleanup theo retention cần dựa
> trên tuổi object và đối soát SQL, chưa được bật.

> **SQL AI permit có fencing 24/10:** Mỗi lần process nhận SQL application
> lock, SQL tăng token fencing đơn điệu. Trước khi trả facts từ provider,
> extractor kiểm tra lại session lock và token trên chính session đó; watchdog
> cũng giám sát cả hai. Integration LocalDB mô phỏng session bị `KILL`, xác
> nhận permit ở coordinator thứ hai nhận token mới lớn hơn và owner cũ không
> thể xác nhận quyền. Migration `20261023182852_AddVisionInvocationFence`
> tạo dòng singleton ban đầu. Đây bảo vệ việc chấp nhận kết quả đã cũ, nhưng
> không thể buộc provider bên ngoài dừng request/billing ngay khi session mất;
> gate mặc định vẫn false cho tới khi có shared private storage và chaos/load
> test trên hai host thật.

> **Cooldown provider dùng chung qua SQL 24/10:** Khi
> `Vision__UseSqlDistributedGate=true`, cooldown sau rate-limit được ghi/đọc
> trong bảng `VisionProviderCooldowns`; thời hạn mới chỉ được kéo dài, không
> rút ngắn. Các process dùng chung database vì vậy không gọi nối tiếp khi một
> host vừa nhận 429. SQL `SYSUTCDATETIME()` là nguồn thời gian chuẩn cho
> cooldown phân tán khi ghi lẫn đọc; test LocalDB truyền giờ host lệch ±nhiều
> năm xác nhận không bỏ qua hoặc kéo dài cooldown sai. Test hai coordinator
> cũng xác nhận read/extend/clear. Hai migration `20261023181204_AddSharedVisionProviderCooldown`
> và `20261023182852_AddVisionInvocationFence` phải được backup, áp có kiểm
> soát trước khi bật SQL gate. Không tự bật
> startup migration và chưa chạy migration trên hosting. Cả hai migration đã
> được áp trên LocalDB mới; cooldown ghi/đọc được, fencing row được seed,
> không còn pending migration và database tạm đã xóa. Debug/Release
> **285/285**, LocalDB integration **15/15**, Release build 0 warning/error,
> EF không pending model change. Đây là kiểm thử LocalDB cục bộ, không thay
> thế xác nhận clock/HA của SQL hosting.
> Đây chưa là kiểm thử 429 từ provider thật hoặc hai host trên mạng; gate mặc
> định vẫn false.

> **SQL UTC cho job lease/heartbeat 24/10:** Claim/reclaim, heartbeat, kiểm tra
> lease trước terminal write và timestamp audit xử lý dùng `SYSUTCDATETIME()`
> đọc từ SQL thay vì đồng hồ process. LocalDB xác nhận helper lấy giờ SQL,
> lease ban đầu còn hạn và heartbeat gia hạn lease qua extraction dài. Sau phần
> này Debug/Release **285/285**, LocalDB worker integration **15/15**; Release
> build **0 warning/error**, EF không pending model change. Lượt `dotnet test`
> báo `NU1900` do NuGet advisory feed không truy cập được; đây không phải
> vulnerability audit thành công. Chưa kiểm tra clock skew trên hai SQL host,
> vì đây chỉ là LocalDB.

> **Dependency vulnerability audit 24/10:** Chạy riêng `dotnet list package
> --vulnerable --include-transitive` qua NuGet advisory feed cho cả `AURA.csproj`
> và `tests/AURA.Tests/AURA.Tests.csproj`; cả hai báo không có package dễ tổn
> thương theo nguồn hiện tại. Đây không thay cho kiểm thử runtime, license
> review hoặc kiểm tra lại khi package/feed thay đổi. Các lần `--no-restore`
> vẫn có thể hiện `NU1900` vì invocation đó không tải được advisory metadata.

> **Idempotency khi hai poller insert đồng thời 24/10:** SQL primary key tiếp
> tục là trọng tài cho source-derived request ID. Nếu một poller thua insert
> vì row cùng ID vừa được commit, intake đọc lại row, so SHA-256 rồi trả cùng
> job và phát tín hiệu queue; nội dung hash khác vẫn bị từ chối. Test fault
> injection xác nhận chỉ có một request/file. Debug/Release **282/282**,
> LocalDB worker integration được khởi chạy lại sau thay đổi, Release build
> 0 warning/error, EF không pending model change. Điều này kiểm chứng xử lý
> phía intake cho conflict SQL đã commit, chưa phải kiểm thử hai Drive poller
> trên hai máy thật hoặc race network/share filesystem.

> **SQL AI gate mất session 24/10:** Với SQL gate bật, lease permit nay theo
> dõi `APPLOCK_MODE` trên đúng session mỗi 5 giây. Mất session/lock sẽ hủy
> cancellation token của provider; hồ sơ nhận `AI_GATE_LOST`, không được ghi
> như quyết định AI hoàn tất và được giữ cho retry theo worker policy.
> LocalDB integration chủ động `KILL` session xác nhận watchdog phát hiện và
> dispose báo lỗi; Debug/Release **281/281**, LocalDB opt-in đã gồm test này,
> Release build 0 warning/error, EF không pending model change. Đây vẫn là
> phát hiện theo chu kỳ, provider có thể đã nhận request trước khi hủy; chưa
> chứng minh provider ngừng xử lý tức thì hay không phát sinh billing/request
> trùng. Chưa có test hai máy chủ thật hoặc outage/chaos trên hosting.

> **Lease heartbeat 24/10:** Trong khi worker giữ một job `PROCESSING`,
> heartbeat SQL riêng gia hạn lease theo `Id + ProcessingAttemptCount` trước
> khi hết hạn, kể cả lúc chờ AI permit. Mất ownership hoặc lỗi SQL tại nhịp
> gia hạn sẽ hủy lượt trích xuất; trước khi lưu terminal, worker dừng heartbeat
> và đọc lại rowversion của đúng attempt/lease còn hiệu lực. LocalDB test
> xác nhận lượt AI dài được gia hạn và chỉ có một audit, đổi ownership sẽ hủy
> lượt đang chờ mà không ghi audit. Debug/Release **280/280**, LocalDB opt-in
> **11/11**, build Release 0 warning/error, EF không pending model change.
> Lượt Release test chạy song song với build ban đầu gặp khóa file `obj`;
> chạy tuần tự sau đó 280/280 pass. Vẫn chưa kiểm chứng hai máy chủ thật,
> mất SQL session trong khi provider đang chạy, hoặc load/chaos; **NO-GO**
> cho nhiều worker/live production.

> **Intake crash-retry 24/10:** Ảnh được ghi vào file tạm cùng thư mục rồi
> chuyển tên trước khi tạo SQL job. Nếu một lần ghi SQL có kết quả không chắc
> chắn, file hoàn chỉnh được giữ lại; lần quét Drive sau chỉ tái sử dụng khi
> size và SHA-256 khớp. File nguồn thiếu/sai byte và source key bị dùng lại
> với nội dung khác đều bị từ chối, không ghi đè. Drive intake **25/25**;
> toàn bộ Debug/Release **278/278**, LocalDB opt-in **9/9**, Release build
> 0 warning/error, EF không pending model change. Đây là kiểm thử fault
> injection local, **không** phải kiểm chứng mất điện, SMB/object storage,
> dọn file tạm hay giao dịch nguyên tử SQL + filesystem. Không đổi default
> connector hoặc deploy live; gate nhiều host vẫn đóng.

> **Telemetry chi phí OpenRouter 24/10:** Khi response có `usage.cost` là số
> không âm, adapter ghi chi phí USD cùng token/provider-kind mà không ghi
> nội dung ảnh hay JSON model. Thiếu hoặc không hợp lệ thì ghi `null`, không
> xem là $0; test chặn chuỗi có dạng PII từ trường cost lọt vào log.
> Release/Debug **275/275**, adapter **16/16**, LocalDB opt-in **9/9**,
> Release build 0 warning/error, EF không pending model change. Lượt LocalDB
> đầu tiên thất bại vì instance dừng và môi trường kiểm thử không truy cập
> được registry; sau khi khởi động instance hiện có và chạy bên ngoài sandbox,
> 9/9 pass. `NU1900` chỉ cho biết không tải được nguồn vulnerability advisory,
> **không** xác nhận dependency sạch. Telemetry cost chưa phải billing
> reconciliation, chưa có tổng chi phí/alert đa host hay load test; vẫn NO-GO
> cho scale/pilot production.

> **Smoke localhost 24/10:** Truy vấn chỉ đếm trên `AuraDb` cho thấy không có
> job `PENDING/PROCESSING` trước khi khởi chạy; `DriveSandbox__Enabled=false`.
> `/healthz` trong Development trả `status=ok`, `aiConfigured=true`,
> `databaseUpToDate=true`, `pendingMigrationCount=0`, `storageAvailable=true`,
> `sqlDistributedAiGateEnabled=false` và `strictZeroDataRetention=false`.
> Process thử đã dừng. Đây chỉ xác nhận startup/cấu hình, **không** gọi AI,
> không chứng minh accuracy, route ZDR hay readiness của nhiều host.

> **Đối chứng model local mới nhất (23/10): vẫn NO-GO publish/scale.** Trên cùng
> 15 ca synthetic đã biết và code quyết định mới, Qwen3.6 Flash đạt 15/15
> quyết định, 0 system error; Qwen3.8 Flash đạt 13/15, một schema mismatch
> và một over-escalation. Trước khi bổ sung gate mô tả hàng hóa chung chung,
> Qwen3.6 đã bỏ lọt một ca FACT do dòng tổng bị mờ nhưng model dựng evidence
> hợp lý từ số dòng hàng. Gate mới chuyển ca đó cho người kiểm tra, **không**
> giải quyết tổng quát việc model tưởng tượng pixel. Adapter đã chặn response
> provider quá 2 MiB. Worker kiểm tra size/SHA-256 file trước AI.
> Release/Debug 274/274 và LocalDB opt-in 9/9 sau test reclaim lease,
> SQL application lock, file bị sửa, backup/restore synthetic, monitor queue
> và hai host giả lập dùng chung thư mục ảnh;
> build sạch và EF không đổi sau guard.
> Không đổi default model hay
> SmartASP; chưa có holdout mù/ảnh thật được phép dùng với model thay thế.
> Xem [đối chứng route và giới hạn](MODEL_ROUTE_COMPARISON_2026-10-23.md).

> **Gate strict-schema trước đối chứng model (23/10): NO-GO publish/scale.** Parser v2 đã bắt buộc đủ
> shape; log/audit Semantic không còn ghi nguyên văn issue có thể chứa dữ
> liệu hóa đơn. Release và Debug 253/253 test pass; LocalDB opt-in 2/2 pass
> trên DB tạm; Release build 0 warning/error, EF không có pending model change.
> Hai lượt 5 ảnh synthetic khớp nhãn 3/5 và 4/5. Lượt 15 ảnh thật đã dùng
> trước chỉ 13/15 completed, hai phản hồi provider HTTP 200 nhưng không là
> JSON object sau retry. Không có benchmark accuracy mới; chưa publish.
> Xem [evidence strict-schema](STRICT_SCHEMA_REGRESSION_2026-10-23.md).

> **Chẩn đoán tiếp theo (23/10):** đã bổ sung mã lý do schema an toàn,
> không ghi phản hồi model thô hoặc giá trị hóa đơn. Ca 13 của bộ 15 ảnh cũ
> chạy lại riêng 3/3 completed nên chưa tái hiện được lỗi trước đó; không
> được xem là cải thiện accuracy. Default suite 248/248 pass (hai ca LocalDB
> integration opt-in không nằm trong lượt này). Vẫn **NO-GO** cho publish/
> scale. Đã xác định thêm khoảng trống code: với version v2 hiện diện,
> parser chưa bắt buộc đủ mọi trường required; cần fix và chạy regression
> trước khi tiếp tục. Xem [evidence chẩn đoán](SCHEMA_FAILURE_DIAGNOSTIC_2026-10-23.md).

Đánh giá về mức supervised MVP, yêu cầu Track A, phạm vi nghiệp vụ, UX reload
và điều phối nhiều máy chủ nằm tại
[readiness and scale assessment](READINESS_AND_SCALE_ASSESSMENT_2026-10-23.md).
Các artifact cuộc thi được giữ nguyên; xem [addendum hậu cuộc thi](../submission/AURA_POST_CONTEST_ADDENDUM_2026-10-23.md).

> **Gate mới nhất sau chỉnh prompt, 23/10:** 234/234 Release tests pass;
> smoke local 5 synthetic chỉ 3/5 completed và khớp nhãn, hai ca
> `AI_SCHEMA_MISMATCH`. Lặp lại 15 ảnh thật đã dùng trước: 14/15 completed,
> một `AI_RATE_LIMIT`; 10/15 khớp nhãn khóa và 11/15 quyết định trùng lần
> trước nếu tính ca fail là không khớp. Đây là **NO-GO cho benchmark mới và
> mở rộng worker**. Xem [evidence và giới hạn](EVALUATION_EVIDENCE.md).

> **Candidate mới nhất:** runner ghi checkpoint; retry nhắc schema/v2; 429
> quay về queue tối đa ba lượt. Parser chỉ gắn version v2 khi provider bỏ sót
> đúng trường protocol đó và mọi trường evidence còn lại hiện diện, có cờ
> audit `EvidenceContractVersionInferred`. Suite báo 244/244; hai ca LocalDB
> opt-in chạy riêng 2/2 pass, DB tạm đã dọn. Hai lượt synthetic liên tiếp
> 5/5, nhưng lặp 15 ảnh thật đã dùng vẫn chỉ 14/15 completed (một
> `AI_SCHEMA_MISMATCH`), 10/15 khớp nhãn và 11/15 lặp quyết định cũ.
> Audit: 4/15 Semantic Repair, 0/15 inferred version. **Chưa publish hoặc
> tăng worker**; đây chưa phải bằng chứng production accuracy.

> **Source hiện hành:** **231/231** Release tests pass, Release build 0
> warning/error với `--no-restore`, EF không có pending model changes. Drive
> synthetic sandbox vẫn chỉ chạy Development/loopback và tắt mặc định. Batch
> 5/20/100 đã chạy qua Google Drive API, SQL queue, OpenRouter và policy.
> Bản 100 đầu sinh 104 job từ 100 file do metadata version đổi; sau sửa
> identity theo folder ID + file ID + MD5 nội dung, DB thử mới sinh đúng
> 100 job, 98 completed và 2 fail-safe. Poll và restart không tạo thêm job.
> 15 ảnh thật có consent chạy qua **manual intake local**, 15/15 completed;
> không upload lên Drive và không có ground truth khớp hash để tính accuracy.
> Xem [báo cáo đo MVP](DRIVE_MVP_VALIDATION_2026-10-22.md). Đây không phải
> bằng chứng production-ready hay cho phép publish Drive connector.

> **Hardening 23/10:** storage đặt dưới `wwwroot` bị từ chối lúc startup;
> tên file đã lưu không được chứa path separator. Dashboard trả `no-store`.
> Smoke local trên DB thử riêng đạt `/healthz=ok`; bộ 100 hiện có bị chặn bởi
> gate hash duy nhất vì có 70 bản sao. 15 ảnh thật có 15 hash khác nhau nhưng
> chưa có nhãn accuracy. Xem [evidence và giới hạn](STORAGE_AND_DATASET_GATE_2026-10-23.md).

> **Mốc lịch sử trước batch:** Source 22/10 có Drive sandbox Development-only, tắt mặc định,
> không thêm EF migration. Manual upload và Drive staging cùng dùng
> ReceiptIntakeService; Drive chỉ đọc file con trực tiếp của folder test riêng,
> chống trùng bằng request ID tất định theo file ID + version lúc đó. **E2E local thật**
> đã đọc Google Drive qua service account, tạo SQL job và gọi OpenRouter:
> 3/3 lần xử lý job synthetic hoàn tất (2 fixture khác nhau, một fixture chạy
> lại để xác nhận sửa lỗi), một file `.txt` bị chặn, restart không tạo trùng.
> Fixture thứ hai lộ false positive `TaxId` giống PHONE; backend đã sửa và
> chạy lại bằng file ID mới ra `AUTO_APPROVE`, vẫn giữ audit cũ.
> `/healthz` local `status=ok`, migration 0, dashboard 200. Connector đã tắt
> lại sau test; **bản live chưa publish lại, không nhận dữ liệu thật**.
> Rejection log được hardening để cùng source-version/reason chỉ ghi một lần;
> vượt giới hạn scan 100 entries cũng fail-closed, không intake một phần.
> Tại mốc đó **216/216** Release tests pass, build 0 warning/error, EF không pending
> model changes. Xem [evidence Drive local](DRIVE_SANDBOX_EVIDENCE_TEMPLATE.md),
> [checklist/GO-NO-GO](DRIVE_SANDBOX_CHECKLIST.md) và [runbook](DRIVE_SANDBOX_LOCAL.md).

> Bản source sau cuộc thi có thêm gate Verify chỉ dành cho Development và rate limit HTTP trong một process; 196/196 offline tests, Release build 0 warning và EF model không đổi. **Chưa publish/smoke bản hardening này trên live; chưa sẵn sàng cho dữ liệu người dùng thật.** Xem [báo cáo hardening 21/10](POST_CONTEST_HARDENING_2026-10-21.md). Các số live bên dưới là evidence lịch sử của bản trước, không xác nhận hành vi bản mới.

## Kết luận

Candidate sau feedback tại commit `de101f2` đã được publish và smoke trên live ngày 17/10: health HTTP 200, Verify 5/5, ba upload synthetic đều hoàn tất và workflow Forward → YES → Undo → Audit đi hết luồng. Source sau đó có một UX follow-up: rút gọn intake, cho chọn kỳ dashboard/CSV và tăng tương phản/scroll bảng; phần UX này cần publish lại trước khi gọi là live. Mỗi ảnh vẫn là một hồ sơ độc lập; hệ thống chưa có `InvoiceBatch`/`batchId` phía server. Video cũ chỉ là bằng chứng của baseline trước feedback.

## Cấu hình live

| Thành phần | Giá trị hiện hành |
|---|---|
| Provider gateway | OpenRouter |
| Model | `qwen/qwen3.8-flash` |
| Route | Alibaba |
| Reasoning | `none` |
| Processing | Durable SQL background queue |
| AI concurrency | 1 trong mỗi process |
| Provider cooldown | 120 giây |
| Verify Harness | Bản source mới mặc định tắt; chỉ cho phép trong Development khi bật rõ ràng. Live cũ từng bật, cần publish bản mới để khóa. |
| Live fallback | Tắt; Ollama chỉ là contingency local |
| Lượt chọn nhiều ảnh | Tối đa 10 JPG/PNG; mỗi ảnh tối đa 5 MB và tạo một job/status URL riêng, không có batch transaction phía server |
| Amount | UI không có ô nhập; worker dùng `totalAmount` AI trích xuất sau validation. `USER_PROVIDED` chỉ còn cho test kit/API lịch sử |
| Provider privacy | `data_collection=deny`; strict ZDR mặc định tắt chờ xác nhận route live |
| Privacy route | `ESCALATE_PRIVACY`; triage category, không lặp lại giá trị gốc |

Read-only health ngày 17/10 lúc 12:54 ICT trả HTTP 200, `status=ok`, database/storage sẵn sàng, migration 0, Qwen3.8 Flash, reasoning none, `data_collection=deny`, AI concurrency 1 và fallback tắt. Health không gọi inference nên đã được đối chiếu thêm bằng live smoke phía dưới.

## Gate kỹ thuật đã đạt

- Debug build: 0 warning, 0 error.
- Offline tests sau UX follow-up và multilingual fail-safe: 190/190 pass.
- Mailbox sandbox thử nghiệm cùng migration liên quan đã được gỡ; EF model hiện không có pending model change.
- Live Verify sau publish: 5/5 trong 49,518 giây, 0 system error.
- Duplicate Verify clients: 1 HTTP 200 và 4 HTTP 409 trên một process.
- Năm upload khác nhau: 5/5 hoàn tất, không mất job; E2E P95 93,684 giây do concurrency một.
- Live upload không gửi `claimedAmount`: 3/3 hoàn tất, đều HTTP 202, AI amount provenance đúng, 0 provider/system error; 2/3 khớp manifest cũ, TC-05 chuyển bảo thủ sang `ESCALATE_PRIVACY` thay vì `ESCALATE_POLICY`.
- Live input validation: 2/2 ca file giả magic bytes và file 5 MB + 1 byte bị HTTP 400 trước storage/queue.
- Workflow live: Forward → manager YES → Undo → Audit đều pass trên fixture synthetic.

## Thay đổi sau feedback doanh nghiệp

1. **Nhiều hóa đơn:** UI nhận tối đa 10 ảnh và gọi tuần tự cùng endpoint HTTP 202. Mỗi ảnh vẫn qua kiểm extension/MIME/magic bytes/5 MB, private storage, SHA-256, SQL row, lease và audit độc lập. Danh sách job được lưu sau từng response 202 để lỗi giữa batch không làm mất job đã nhận.
2. **Bỏ nhập tiền khỏi UI:** trình duyệt không còn hiển thị hoặc gửi `claimedAmount`. Worker chỉ dùng `facts.TotalAmount` do model trả về sau JSON Schema và semantic validation; nguồn mới của luồng demo là `AI_EXTRACTED` hoặc `UNAVAILABLE`. Backend giữ `USER_PROVIDED` để tương thích test kit/API lịch sử. Thiếu total vẫn fail-safe về kiểm tra dữ kiện.
3. **Dashboard quản lý:** tab Tổng quan cho chọn tháng/năm; KPI, bảng ngày, bảng đối chiếu năm và CSV dùng cùng kỳ được chọn. Dữ liệu được project theo giờ Việt Nam từ SQL.
4. **Privacy triage:** model chỉ gắn marker category trong `suspiciousSignals`; backend normalize category, không ghi lại giá trị cá nhân, và policy C# chuyển `ESCALATE_PRIVACY`. Request OpenRouter mặc định yêu cầu provider không dùng dữ liệu để thu thập/huấn luyện.
5. **Báo cáo quản lý:** dashboard tải CSV theo kỳ đang chọn từ SQL. File chỉ chứa dữ liệu vận hành cần review, không chứa raw AI facts và có chống CSV formula injection.
6. **Drive folder connector:** Drive synthetic sandbox đã qua E2E local trên fixture tổng hợp, tắt mặc định và chỉ chạy Development loopback. Service account Viewer trên folder test, metadata/MD5/version + file gate và cùng ingestion boundary. Kênh production vẫn cần identity người nộp, consent, malware quarantine, retention, distributed coordination và capacity test.
7. **Dashboard fail-closed và UX:** khi SQL không tải được, UI không hiển thị KPI bằng 0 hoặc cho tải CSV. Bảng có vùng cuộn, header sticky và đường kẻ tương phản hơn; nút refresh dùng icon. “Cần kiểm tra dữ liệu cá nhân” là số hồ sơ có marker privacy cần người xem lại, không phải kết luận pháp lý.

Privacy triage là công cụ hỗ trợ rà soát, không phải kết luận pháp lý hay chứng nhận tuân thủ. Strict zero-data-retention chỉ được bật sau khi route/provider live được xác nhận hỗ trợ; nếu bật mù có thể làm không còn provider đủ điều kiện. Rate limit mới chỉ nằm trong memory từng process và không thay thế SSO/RBAC hoặc edge limiter.

## Evidence đánh giá

| Tập | Kết quả | Ranh giới claim |
|---|---|---|
| Prospective mini-holdout 10 | 8/10, 0 missed, 2 over, 0 system/provider/429, 28/30 field | Một official run; mẫu nhỏ có redaction; không phải production accuracy |
| Regression 12 original bytes | 10/12, 0 missed, 2 over, 35/36 field | Dữ liệu đã biết; stability/concurrency only |
| Sequential repeat 12 | 11/12, 0 missed, 1 over, P95 32,903 giây | Không phải holdout mới |

FAHASA còn over ổn định do validator chưa chấp nhận chắc chắn nhãn `Thanh toán 91.500 đ` là final-payable. Không vá keyword sát ngày thi vì có nguy cơ nhầm tender, tiền khách đưa hoặc tiền thối. Evidence localization vẫn là prototype cô lập và chưa ảnh hưởng pipeline/policy.

## Gate con người còn mở

1. Publish/recycle UX follow-up mới nhất, kiểm lại `/healthz`, bộ chọn kỳ/CSV và một synthetic smoke; không rerun holdout cũ để tạo accuracy claim mới.
2. Xác nhận route Alibaba/OpenRouter vẫn nhận `data_collection=deny`; chỉ cân nhắc bật strict ZDR sau một probe có kiểm soát.
3. Hoàn tất ba phiên user study thật, có consent, quote nguyên văn, một thay đổi truy vết được và một negative effect.
4. Rehearsal có bấm giờ theo demo mới: dashboard → lượt nhiều ảnh không nhập tiền → privacy escalation → manager → audit.

Đối chiếu đầy đủ với tài liệu `recommend_check`, 10 architecture gates và evidence read-only ngày 17/10 nằm trong `AFTER_FEEDBACK_ARCHITECTURE_CHECKLIST.md`.

## Cách đọc ngày trong gói Demo Day

Các gói evidence có timestamp ngoài repository là bằng chứng lịch sử bất biến, không được sửa ngày để tạo cảm giác đây là phép đo mới. Nguồn trạng thái hiện hành là tài liệu này; bản tổng hợp số liệu công khai nằm tại `EVALUATION_EVIDENCE.md`. Tài liệu ôn tập, kịch bản và Q&A cá nhân được giữ ngoài repository; chúng không phải evidence đánh giá hay hồ sơ nộp.
