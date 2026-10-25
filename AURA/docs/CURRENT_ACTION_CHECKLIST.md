# Checklist hiện hành và mốc publish lịch sử

> **25/10 — NO-GO publish/scale bản source mới.** Checklist
> publish phía dưới là mốc lịch sử 17–21/10, không còn là lệnh triển khai
> candidate hiện tại. Xem [trạng thái hiện hành](CURRENT_PROJECT_STATUS.md)
> và [đối chứng model](MODEL_ROUTE_COMPARISON_2026-10-24.md).

Việc cần làm trước khi mở lại gate publish/pilot:

- [x] Đã chốt giữ DB cũ, dùng synthetic retention sandbox trước. Launcher `Start-AuraRetentionSandbox.ps1 -PrepareOnly` build/migrate DB sandbox riêng thành công, receipt root nằm trong `.tmp` bị Git/publish exclude; không sửa User Secrets và đã xác minh khôi phục environment. Inventory dữ liệu thật chỉ read-only và lưu local bị Git ignore.
- [x] Metadata-only/UTC 25/10: record không có ảnh riêng vẫn được purge facts/audit đúng hạn; Verify AUTO mới có deadline; backup tính cả hạn của record không có file. Đã sửa mất UTC/giây lẻ khi PowerShell đọc JSON. Bốn integration cases PS5.1/7 kiểm manifest V1, offset +07, trước hạn/đúng hạn và report-only đều pass. Full Debug/Release cuối **349/349 mỗi bản**, LocalDB và PowerShell integration bật, không skip; EF sạch. TRX nằm local tại `test_kit/results/retention-audit-20261025/`.
- [x] Retention/restore 25/10: drain nhiều batch trước startup, giữ update lock đến khi xóa ảnh + commit SQL, lock contention không được trả giả là "đã sạch"; LocalDB test batch size 2 dọn đủ 5 hồ sơ và giữ hồ sơ chưa chốt. Backup có checksum/VERIFYONLY, restore giữ deadline cũ; dọn hồ sơ quá hạn và giữ pending. Lần full Release đầu 340/341 phát hiện sharing violation trong concurrent intake; thêm retry có giới hạn cho Windows sharing/lock violation, size/hash vẫn bắt buộc. Focused storage/intake 16/16; full Debug/Release sau fix **344/344 mỗi bản**, LocalDB bật, không skip; Release build 0 warning/error, EF sạch. Script backup kiểm Git root thật ở thư mục cha. Chi tiết nằm trong [trạng thái hiện hành](CURRENT_PROJECT_STATUS.md).
- [x] Commit `0568b5a`: privacy prompt phân biệt người mua/người nhận với bên bán/hộ kinh doanh; semantic repair ở cả OpenRouter và Ollama giữ nguyên marker privacy hợp lệ của lần trích xuất đầu. Test trực tiếp regression marker qua repair và classifier đều pass; full Debug/Release pass **333/333** mỗi bản với `AURA_RUN_LOCALDB_INTEGRATION=1`.
- [x] Sau commit, bộ 4 ảnh synthetic privacy chạy 3 lượt: **12/12 quyết định**, **21/21 category**, 0 missed privacy category, 0 over-flag, 0 provider/system error/fallback. Đây là bộ role-control nhỏ, không phải detector recall benchmark tổng quát.
- [x] Sau commit `06346de`, Test Kit v3.1 được chạy lại trên DB LocalDB và thư mục ảnh private riêng: **15/15 quyết định**, 143/146 facts (97,95%), không missed/over/system/provider error/fallback; P50/P95 end-to-end 3.620/7.802 giây. Semantic repair R3-06 không giải quyết được tổng bị che và giữ `ESCALATE_FACT`; ba lệch facts là chuỗi `invoiceDateEvidence` thêm nhãn in ở R3-03/R3-05/R3-07. Health trước/sau `ok`; queue cuối lượt rỗng. Strict ZDR request + provider-data deny bật, concurrency 1, fallback/Drive/Verify tắt. Raw evidence local bị Git ignore. Chi tiết tại [model route comparison](MODEL_ROUTE_COMPARISON_2026-10-24.md).
- [x] Rerun local single-host trên commit hiện hành: Debug/Release **333/333**, backup/restore SQL + ảnh synthetic riêng **1/1**, Release build 0 warning/error, EF không pending model changes. Một ảnh R3-01 chạy qua LocalFileSystem → SQL durable queue → OpenRouter → Semantic/policy và kết thúc `COMPLETED / AUTO_APPROVE` sau 5.928 ms; `/healthz=ok`, Strict ZDR request + provider-data denial bật, fallback off, một AI permit. Chỉ là smoke trên một ảnh synthetic, không phải thêm accuracy sample. Kết quả nằm trong Git-ignored `test_kit/results/local-singlehost-20261024_193823_680018fd/`; DB/receipt test cô lập còn được giữ.
- [x] Runbook backup file local: đoạn Robocopy + so sánh manifest (relative path/bytes/SHA-256) pass trên hai file synthetic trong thư mục tạm, gồm một file lồng thư mục; Robocopy exit 1 là thành công có file mới. Đây chỉ xác minh thao tác copy/hash; không phải backup dữ liệu thật hoặc SQL backup.
- [x] Screen route Ollama local: một R3-01 synthetic qua loopback `127.0.0.1:11434`, không dùng provider ngoài; `qwen3-vl:4b-instruct` hoàn tất fail-safe thành `ESCALATE_FACT` sau 278.164 giây thay vì nhãn `AUTO_APPROVE`. Semantic bắt `bookingId` không nhất quán và chạy một repair. Đây là một ca screen, không phải benchmark; xác nhận thêm trade-off privacy/locality với latency/chất lượng cần benchmark target hardware trước khi cân nhắc đổi route.
- [ ] Privacy gate vẫn mở: triage là post-inference và không che ảnh trước provider; cần pre-inference redaction/local VLM hoặc route được chủ dữ liệu/pháp chế duyệt, threat model và holdout độc lập trước pilot.
- [x] Retention code 7 ngày đã được thêm nhưng **chưa bật trên DB người dùng**: deadline bền vững cho quyết định tự động cuối và quyết định quản lý; startup catch-up + sweep mỗi 5 phút; purge ảnh, facts, audit trong SQL theo batch và khóa Undo sau hạn. Backup local có manifest/hash, remover report-only nếu thiếu `-Apply`. Migration LocalDB cô lập + startup health + script backup tổng hợp pass; Debug/Release **339/339** với LocalDB integration. Giới hạn: best-effort khi máy chạy; nếu máy tắt, dữ liệu dọn ở lần app/Task Scheduler kế tiếp. Còn restore drill đúng host, lịch dọn backup và bật `DataRetention:Enabled=true` có chủ ý.
- [ ] Local backup/restore đã được diễn tập với cặp SQL + ảnh synthetic, nhưng chưa có restore drill trên hosting, snapshot đồng bộ hay RPO/RTO. Hoàn tất đúng môi trường lưu hiện tại trước khi chuyển sang cloud.

- [x] Sửa fail-safe source intake: xác nhận kết quả insert lỗi mơ hồ bằng truy vấn `AsNoTracking`, không nhận nhầm entity chưa commit còn trong EF change tracker thành hồ sơ bền vững. Focused intake tests 29/29 và full Debug/Release 300/300 (18 LocalDB integration cases bật).
- [x] SQL-trigger rollback integration test chạy trên LocalDB thật: transaction rollback không bị hiểu là thành công; retry trong scope mới tạo đúng một job/audit. Race test chạy hai intake/repository/DbContext độc lập cùng đọc trước và cùng insert source key: một request, một audit, một file. Full Debug/Release 300/300 với `AURA_RUN_LOCALDB_INTEGRATION=1`, cả 18 integration cases hoạt động và không skip. Chỉ DB test tên ngẫu nhiên được tạo/xóa; LocalDB user instance được giữ Running.
- [x] Privacy request invariant: bật `OpenRouter__RequireZeroDataRetention` luôn kéo `data_collection` về `deny` dù biến deny riêng false; payload và `/healthz` dùng cùng giá trị hiệu lực. Regression test pass. Đây không phải xác nhận provider/model hỗ trợ ZDR hoặc kiểm thử request thật.
- [x] Controlled model smoke 24/10: một ca synthetic `TC-01` chạy qua manual intake → SQL queue → OpenRouter thật → semantic/policy; HTTP 202/200, `COMPLETED`, `AUTO_APPROVE` đúng nhãn, số tiền đúng, 6,659 ms, không fallback/lỗi provider. DB và ảnh tạm đã dọn. `strictZeroDataRetention=false`, SQL distributed gate off; output có mã vận chuyển nhưng privacy triage `NONE`, cần cải thiện/đánh giá detector trước khi mở dữ liệu thật. Evidence: [MODEL_SMOKE_2026-10-24.md](MODEL_SMOKE_2026-10-24.md).
- [x] ZDR metadata + synthetic route check 24/10: public endpoint catalog shows no ZDR-listed Alibaba endpoint for `qwen/qwen3.8-flash`; `qwen/qwen3.8-27b` has eligible ZDR routes. Two repeats of one synthetic case succeeded with Strict ZDR; the repeat with endpoint telemetry identified DekaLLM, not Alibaba infrastructure. This is not accuracy evidence or legal assurance; model default unchanged. See [route evidence](OPENROUTER_ZDR_ROUTE_CHECK_2026-10-24.md).
- [x] Post-fix Strict-ZDR v3.1 live repeat 24/10: the same locked 15 synthetic receipts ran three times on source commit `2e10e534d082314269341af1114a13b7e19f6922`; Qwen3.8-27B matched decisions **15/15 on each run**, with no missed escalation, over-escalation, provider error or fallback. Pooled exact facts were 432/438 (98.63%). The only recurring fact mismatch was the `invoiceDateEvidence` display string for R3-03/R3-07 (printed label included); normalized date and decision stayed correct. R3-06 Semantic repair was attempted each time, could not resolve the intentionally obscured printed total, and safely stayed `ESCALATE_FACT`. This is repeatability evidence for the locked synthetic set, not general accuracy, privacy coverage, legal, or production evidence. Full Debug/Release tests pass 332/332 each with `AURA_RUN_LOCALDB_INTEGRATION=1`; Release build 0 warnings/errors; EF has no pending model changes. [Detailed report](MODEL_ROUTE_COMPARISON_2026-10-24.md).
- [x] Isolated local runtime at the evaluation checkpoint reported `/healthz=ok`, LocalFileSystem, SQL current, one AI request at a time, Strict ZDR and provider data-collection deny enabled, fallback/Drive/Verify off. A separate six-case runner-instrumentation check recorded 1 Semantic repair attempt, 0 resolved, 1 unresolved (`R3-06`), and 0 privacy-review classifications. The zero privacy count is only a property of those fixtures; privacy detector coverage remains an open gate.
- [x] SQL-shared intake backpressure: transaction-owned application lock serializes capacity check + insert across app instances sharing the same SQL database; counts `PENDING` + `PROCESSING`; default ceiling 1,000 (`ReceiptProcessing__MaxOutstandingReceipts`, range 1–10,000). Full queue returns HTTP 503/`Retry-After`; manual orphan upload bytes are removed, source-key bytes are retained for a later retry. SQL race integration test proves two repositories cannot exceed a ceiling of one. This is not a lifetime storage quota or spend/abuse limit.
- [x] Queue health signals: one-minute aggregate snapshot now logs pending/processing/failed, oldest pending age, and SQL-recorded receipt bytes; emits transition warnings at 80% of configured outstanding limit, pending age ≥ `ReceiptProcessing__QueueWarningAgeSeconds` (default 300), and newly failed count observed since monitor start. Provider failures and per-response cost remain structured logs. The recorded byte sum excludes orphan objects/temp files and is not authoritative provider storage usage; logs are not yet connected to an alerting/metrics backend and cost is not cumulative/budgeted.
- [x] Manual HTTP intake idempotency: optional `Idempotency-Key` accepts canonical UUID only; browser multi-upload keeps one UUID per SHA-256 in session storage until HTTP acknowledgment and reuses it for up to three network/429/5xx attempts, including same-tab reload. Same key + same SHA-256 resolves to the existing case; same key + different bytes is rejected. LocalDB integration now drives two independent intake/repository/DbContext instances against the same manual key and verifies one row/audit/file. Clients omitting the header retain previous behavior; closing the browser session drops an unacknowledged key.
- [x] Workflow action concurrency/retry: LocalDB races send duplicate single/bulk handoff, manager approval, and duplicate `Undo` writes from independently loaded DbContexts. SQL `rowversion` allows exactly one state/audit commit per race and rejects the stale write. LocalDB controller tests simulate lost responses after approval and Undo: a fresh retry returns `alreadyApplied` without another audit. SQL `rowversion` still makes an in-flight stale request return HTTP 409; retrying after reload is safe.

- [x] Tách receipt storage thành `IReceiptFileStore`; giữ local filesystem mặc định và thêm adapter GCS private opt-in. Intake retry tạo object chỉ khi key vắng mặt, không ghi đè hash khác; worker/controller đọc qua abstraction, worker kiểm size/hash trước inference. Ca opt-in nối intake thật + SQL với worker ở host thứ hai dùng chung fake GCS client đã chạy thành công. Cấu hình auth hỗ trợ ADC hoặc external JSON key rõ ràng; validation từ chối mode/path mơ hồ và credential JSON trong app root, kể cả `GOOGLE_APPLICATION_CREDENTIALS`. Full Debug/Release hiện **333/333 mỗi cấu hình với LocalDB integration bật** (`AURA_RUN_LOCALDB_INTEGRATION=1`) trên DB tạm; EF không pending model changes. Các ca race LocalDB cho chuyển tiếp đơn/lô, duyệt và Undo đều chỉ ghi một trạng thái/audit; bản ghi cũ bị từ chối. Đây vẫn là fake GCS; ADC/IAM/bucket thật và two-machine test chưa được thực hiện.
- [ ] Chưa tạo bucket GCS thật, cấp IAM hoặc kiểm credentials thật. Trước đó cần bucket private với uniform bucket-level access + public access prevention và service account riêng; chỉ cấp quyền object ở bucket, không dùng Drive read-only account. Trên GCP ưu tiên ADC với attached identity; local ưu tiên ADC impersonation, không dùng personal ADC rộng quyền.
- [ ] Kiểm tra integration thực trên bucket test: health/read/write/retry/hash/missing-object; sau đó 2 host cùng đọc object + DB chung, mất lease/restart, backup/restore SQL và object đồng bộ. Chưa bật SQL distributed AI gate hoặc tăng workers.
- [x] Chuẩn bị smoke runner cho GCS staging: trước upload yêu cầu loopback, `/healthz=ok`, DB/schema cập nhật, GCS provider (nếu được yêu cầu), data-collection deny hiệu lực, fallback off, đúng một AI permit và Strict ZDR tùy chọn. Có thể bắt buộc `sqlDistributedAiGateEnabled=true` khi probe staging qua loopback tunnel; đây chỉ là config guard trước một ca synthetic, không thay thế race/lost-session test hai host. Kiểm thử bổ sung với HTTP server giả trên loopback xác nhận pending job dừng ở `PROCESSING_TIMEOUT` sau cấu hình 1 giây (đo 1.114 giây), không gọi model; report không chứa đường dẫn ảnh local.
- [x] Retention candidate đã được kiểm thử local trên dữ liệu tổng hợp: migration + startup health, purger SQL/file/audit, giữ lại hồ sơ chưa chốt, retry khi file đã vắng, khóa Undo sau hạn và script backup manifest/remover. Full Debug/Release **339/339** với LocalDB integration bật. Tóm tắt evidence nằm ở [trạng thái hiện hành](CURRENT_PROJECT_STATUS.md); thao tác ở [runbook](RUNBOOK.md).
- [ ] Chưa bật retention trên DB người dùng theo lựa chọn giữ lịch sử. Root backup đã xác nhận và kiểm tra read-only đang rỗng; chưa đăng ký lịch xóa. Tiếp theo thử startup/HTTP và backup/restore của synthetic sandbox, rồi mới cân nhắc lịch dọn riêng (mỗi 5 phút + khi đăng nhập/catch-up sau missed schedule). Mọi chuyển đổi/xóa dữ liệu lịch sử cần phạm vi được chốt lại; không tự reset deadline để kéo dài thời hạn.
- [ ] Privacy gate còn mở. Bộ role synthetic mới đạt 12/12 qua ba lượt, nhưng chỉ có bốn ảnh được thiết kế riêng và không chứng minh detector tổng quát. Code vẫn gửi ảnh gốc qua `IVisionExtractor`/OpenRouter dưới dạng data URL trước khi `ReceiptPrivacyClassifier.Apply`; smoke trước đó từng có tracking identifier nhưng `privacyRisk=NONE`. Vì vậy `data_collection=deny`/Strict ZDR không phải redaction và không chứng minh detector recall hay điều khoản nhà cung cấp. Trước dữ liệu người dùng cần pre-inference redaction/local VLM hoặc route được chủ dữ liệu/pháp chế chấp thuận, threat model và holdout độc lập đo false negative/positive.
- [ ] Malware scanning/quarantine chưa được triển khai cho manual upload. Browser intake hiện chỉ kiểm tra extension/MIME, magic bytes và giới hạn kích thước. Drive sandbox có `RequireMalwareScanner=true` mặc định và từ chối khởi động khi không có scanner tích hợp; ca synthetic chỉ được phép khi explicitly đặt false trên Development loopback. Không dùng các kiểm tra định dạng này thay cho antivirus hoặc nhận hóa đơn thật từ nguồn không tin cậy.

- [x] Khóa schema đầu vào, field backend-only, metadata/log lỗi provider và tín hiệu Semantic có thể chứa dữ liệu cá nhân; chặn response provider quá 2 MiB. Evidence riêng tại mốc schema 24/10: Release/Debug 280/280, LocalDB opt-in 11/11, Release build 0 warning/error, EF không pending model change. Bộ kiểm thử hiện hành được ghi ở mục SQL clock bên dưới; `NU1900` không phải vulnerability audit thành công.
- [x] Đối chứng cùng 15 synthetic: Qwen3.6 Flash 15/15 quyết định, Qwen3.8 Flash 13/15; đây là bộ đã biết, không phải holdout mù.
- [ ] Tạo tập synthetic mới có nhãn độc lập, tách riêng tổng bị mờ với mô tả mặt hàng chung chung; đo missed escalation, critical fields, lỗi provider và latency qua nhiều lượt.
- [ ] Chỉ gửi 15 ảnh thật qua model thay thế sau khi chủ dữ liệu chấp thuận rõ route mới; giữ ảnh và kết quả chi tiết ngoài Git.
- [ ] Xác minh route ZDR, kiểm soát truy cập, lưu trữ riêng dùng chung, AI lease/fencing phân tán, malware/quarantine, retention, restore drill và tải nhiều instance trước pilot/scale.
- [x] Xác minh `rowversion` chặn stale write: LocalDB hai worker, A bị giữ qua thời điểm lease hết hạn, B reclaim và hoàn tất; A nhận concurrency exception, chỉ còn một kết quả/audit. Đây chưa phải bằng chứng an toàn để bật nhiều worker vì giới hạn AI/cooldown và ảnh vẫn theo process/host.
- [x] Thêm SQL application lock opt-in, shared cooldown dùng `SYSUTCDATETIME()` và fencing token tăng theo permit. Extractor xác minh lock/token sau khi provider trả lời; LocalDB mô phỏng đồng hồ host lệch ±nhiều năm, hai coordinator, session bị `KILL`, token successor cao hơn và owner cũ bị từ chối. Migration `20261023181204_AddSharedVisionProviderCooldown` và `20261023182852_AddVisionInvocationFence` cần được backup/áp dụng có kiểm soát trước khi bật gate; chưa áp dụng hosting. Provider đang xử lý có thể không dừng ngay khi SQL session mất. Giữ `Vision__UseSqlDistributedGate=false` tới khi có shared private storage và chaos/load test trên hai máy chủ thật.
- [x] Chuyển claim/reclaim, heartbeat, terminal ownership check và worker audit timestamp sang giờ SQL `SYSUTCDATETIME()` để không dựa vào đồng hồ của app host; LocalDB helper/lease-heartbeat checks pass, Debug/Release 285/285 và LocalDB integration 15/15. Chưa xác minh lệch giờ giữa hai SQL host hoặc hành vi trên hosting.
- [x] Dependency vulnerability audit 24/10 qua NuGet cho app và test project: không có package dễ tổn thương theo advisory feed hiện tại; đây không thay cho license/runtime review và cần chạy lại khi dependency/feed đổi.
- [x] Worker đối chiếu size/SHA-256 ảnh đã lưu với SQL trước khi gọi AI; ca file bị sửa dừng fail-safe. Chưa có bằng chứng private share hoạt động xuyên host, ACL/backup/restore hoặc tính bất biến trên mọi loại storage.
- [x] Smoke Development localhost 24/10 trên DB LocalDB mới, cô lập và queue rỗng: migrations apply, app khởi động trên loopback port 5101, `/healthz` HTTP 200 / `status=ok`, DB/schema/storage available, Drive/Verify off, AI concurrency 1, SQL distributed gate off. Sweeper ghi `deleted=1; skipped=0` và xóa đúng fixture staging synthetic đã quá 48h. Không gọi model, upload hay đọc dữ liệu người dùng; app dừng và DB/thư mục tạm đã xóa. Port demo 5097 vẫn không chạy; không tự dùng DB demo.
- [x] Diễn tập BACKUP/RESTORE cặp synthetic SQL + file ảnh trên LocalDB: thay đổi trạng thái và bytes nguồn sau backup, khôi phục DB cùng bản file đã backup, rồi đối chiếu pending state/size/SHA-256/audit. Đây là test local theo cặp, không chứng minh snapshot nguyên tử giữa hai hệ thống. Chưa có diễn tập restore DB hosting cùng GCS thật hoặc đo RPO/RTO.
- [x] Monitor nội bộ log số pending/processing/failed, tuổi pending lâu nhất và SQL-recorded bytes mỗi phút; cảnh báo transition ở 80% queue, pending age mặc định 300s, và failed tăng từ lúc monitor chạy. Không lộ trên public healthz hoặc ghi ID/file. Adapter log `usage.cost` hợp lệ theo từng response, không ghi chuỗi provider vào cost. Chưa có alert delivery/SLO, billing reconciliation, usage bucket thật hay metrics xuyên instance.
- [x] Hai host giả lập khác content root cùng dùng thư mục ảnh tuyệt đối private và DB chung; host B xử lý được job. Chưa có test share mạng/ACL/đứt kết nối hay backup ảnh trên hai máy thật.
- [x] Intake source-key ghi ảnh qua file tạm, retry chỉ dùng lại file đích nếu size/SHA-256 khớp; không ghi đè file thiếu/sai byte. LocalFileSystem có sweeper dọn tối đa 500 staging `.tmp` đúng mẫu AURA mỗi giờ, chỉ khi >24h và mở độc quyền được; không đụng receipt đã tiếp nhận, recent file hoặc file không theo mẫu. Chưa có atomicity transaction SQL + filesystem, object cleanup trên GCS hoặc đối chứng SMB thật.
- [x] Worker gia hạn lease SQL theo đúng attempt trong lúc chờ AI và trích xuất; LocalDB chứng minh lượt dài lưu đúng một audit và đổi ownership hủy lượt cũ. Chưa chứng minh SQL application-lock session loss, provider bỏ qua cancellation hoặc tải nhiều host thật; không bật nhiều worker.

## Checklist publish lịch sử (không áp dụng cho candidate hiện tại)

> Cập nhật 21/10: source mới có Verify chỉ dành cho Development, mặc định tắt, và giới hạn HTTP upload/Verify trong từng process. Gate offline **196/196 tests**, build 0 warning, EF model không đổi. Chưa publish/smoke live bản này. Chi tiết và NO-GO pilot thật: [POST_CONTEST_HARDENING_2026-10-21.md](POST_CONTEST_HARDENING_2026-10-21.md).

Ngày chốt: 17/10/2026. Candidate hiện hành giữ manual upload, multi-file UX, dashboard/CSV, privacy triage và multilingual fail-safe. Mailbox sandbox thử nghiệm cùng migration liên quan đã được gỡ khỏi source; email/folder ingestion chỉ còn là roadmap.

## 1. Trạng thái source

- [x] Manual upload đi qua SQL queue bền vững, worker, AI, semantic, privacy, policy và audit.
- [x] UI nhận tối đa 10 JPG/PNG trong một lượt chọn; mỗi ảnh là một hồ sơ và status URL độc lập.
- [x] UI không bắt nhập số tiền; final total phải đến từ dữ kiện AI đã qua schema/semantic validation.
- [x] Hóa đơn có chữ Trung Quốc được nhận diện bảo thủ và luôn chuyển `ESCALATE_FACT`, không tự duyệt.
- [x] Mailbox controller, API key, outbox/mailbox model, smoke script và migration thử nghiệm đã được loại bỏ.
- [x] Release regression sau rollback: 190/190 pass.
- [x] EF model không có pending model change.

## 2. Cấu hình hosting cần giữ

```text
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=<existing SQL connection string>

Vision__Provider=OpenRouter
Vision__FallbackEnabled=false
Vision__FallbackProvider=Ollama
Vision__MaxConcurrentRequests=1
Vision__ProviderCooldownSeconds=120

OpenRouter__ApiKey=<existing secret>
OpenRouter__Model=qwen/qwen3.8-flash
OpenRouter__ReasoningEffort=none
OpenRouter__DenyProviderDataCollection=true
OpenRouter__RequireZeroDataRetention=false

ReceiptStorage__Directory=App_Data/receipts
ReceiptStorage__MaxFileSizeMb=5
Verify__Enabled=false
Database__ApplyMigrationsOnStartup=false
```

Các biến `MailboxSandbox__*` không còn được source đọc. Có thể xóa khỏi hosting sau khi candidate rollback đã publish và health pass; giữ chúng tạm thời cũng không thay đổi hành vi ứng dụng.

## 3. Trước publish

- [ ] Ghi lại commit sẽ publish bằng `git rev-parse --short HEAD`.
- [ ] Xác nhận backup SQL và `App_Data/receipts` đã tải về máy và mở được.
- [ ] Chạy `dotnet test tests/AURA.Tests/AURA.Tests.csproj -c Release`; yêu cầu ít nhất 196/196 pass trên đúng commit được publish.
- [ ] Chạy `dotnet build AURA.csproj -c Release --no-restore`; yêu cầu 0 error.
- [ ] Chạy `dotnet ef migrations has-pending-model-changes --no-build`; yêu cầu `No changes have been made...`.
- [ ] Giữ `Database__ApplyMigrationsOnStartup=false`; không apply migration mailbox đã thất bại.
- [ ] Publish Release đúng commit; không xóa `App_Data/receipts` hoặc file ngoài package.

## 4. Sau publish

- [ ] Recycle application pool và chờ 10–30 giây.
- [ ] `GET /healthz` trả HTTP 200, `status=ok`.
- [ ] `databaseAvailable=true`, `databaseUpToDate=true`, `pendingMigrationCount=0`, `storageAvailable=true`.
- [ ] Health không còn field `mailboxSandbox*`; đây là dấu hiệu candidate rollback đã live.
- [ ] `verifyHarnessEnabled=false` ngay cả nếu hosting còn `Verify__Enabled=true`; chủ động đổi env thành `false` và recycle.
- [ ] Mở được một ảnh lịch sử trước khi tạo dữ liệu mới.
- [ ] Upload đúng một fixture synthetic; nhận HTTP 202 và đi tới terminal.
- [ ] Kiểm amount provenance, semantic/privacy route và audit timeline.
- [ ] Chọn một kỳ trên dashboard và tải CSV cùng kỳ.
- [ ] Verify không được chạy trên Production. Nếu cần benchmark, dùng Development/staging cô lập và bộ fixture synthetic.

## 5. Gate hóa đơn tiếng Trung

- [ ] Chỉ dùng ảnh synthetic hoặc đã được phép sử dụng.
- [ ] Khóa ground truth trước khi chạy: merchant, ID, ngày, currency, total, line items và policy signal.
- [ ] Kiểm chữ Trung được giữ nguyên; ngày `2026年10月17日` canonicalize thành `2026-10-17`; `人民币/RMB/元` thành `CNY`.
- [ ] Tổng chỉ lấy từ nhãn phù hợp như `合计/总计/应付金额/实付金额/价税合计`.
- [ ] Danh mục như bia, thuốc lá hoặc vé xem phim phải được nêu là policy signal.
- [ ] Chứng từ có chữ Trung Quốc hiện phải đi `ESCALATE_FACT`, không `AUTO_APPROVE`.
- [ ] Không dùng một ảnh để tuyên bố hỗ trợ production; benchmark holdout và rule theo thị trường vẫn là gate tương lai.

## 6. Điều kiện GO/NO-GO

**GO cho demo synthetic có giám sát** khi 196/196 test pass, EF model không đổi, publish đúng commit, `/healthz` có migration 0 và Verify tắt, ảnh lịch sử còn đọc được và một upload synthetic hoàn tất. Đây không phải GO cho pilot dữ liệu thật.

**NO-GO cho mailbox/Gmail claim:** source hiện không có mail input, OAuth, cursor, quarantine hay malware scanner. Hướng này chỉ được mở lại sau khi thiết kế schema tương thích, có migration rehearsal trên database clone và đầy đủ identity/least-privilege/privacy gates.
