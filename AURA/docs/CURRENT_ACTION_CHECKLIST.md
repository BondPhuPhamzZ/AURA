# Checklist hiện hành và mốc publish lịch sử

> **24/10 — NO-GO publish/scale bản source mới.** Checklist
> publish phía dưới là mốc lịch sử 17–21/10, không còn là lệnh triển khai
> candidate hiện tại. Xem [trạng thái hiện hành](CURRENT_PROJECT_STATUS.md)
> và [đối chứng model](MODEL_ROUTE_COMPARISON_2026-10-23.md).

Việc cần làm trước khi mở lại gate publish/pilot:

- [x] Sửa fail-safe source intake: xác nhận kết quả insert lỗi mơ hồ bằng truy vấn `AsNoTracking`, không nhận nhầm entity chưa commit còn trong EF change tracker thành hồ sơ bền vững. Focused intake tests 29/29 và full Debug/Release 300/300 (18 LocalDB integration cases bật).
- [x] SQL-trigger rollback integration test chạy trên LocalDB thật: transaction rollback không bị hiểu là thành công; retry trong scope mới tạo đúng một job/audit. Race test chạy hai intake/repository/DbContext độc lập cùng đọc trước và cùng insert source key: một request, một audit, một file. Full Debug/Release 300/300 với `AURA_RUN_LOCALDB_INTEGRATION=1`, cả 18 integration cases hoạt động và không skip. Chỉ DB test tên ngẫu nhiên được tạo/xóa; LocalDB user instance được giữ Running.
- [x] Privacy request invariant: bật `OpenRouter__RequireZeroDataRetention` luôn kéo `data_collection` về `deny` dù biến deny riêng false; payload và `/healthz` dùng cùng giá trị hiệu lực. Regression test pass. Đây không phải xác nhận provider/model hỗ trợ ZDR hoặc kiểm thử request thật.
- [x] Controlled model smoke 24/10: một ca synthetic `TC-01` chạy qua manual intake → SQL queue → OpenRouter thật → semantic/policy; HTTP 202/200, `COMPLETED`, `AUTO_APPROVE` đúng nhãn, số tiền đúng, 6,659 ms, không fallback/lỗi provider. DB và ảnh tạm đã dọn. `strictZeroDataRetention=false`, SQL distributed gate off; output có mã vận chuyển nhưng privacy triage `NONE`, cần cải thiện/đánh giá detector trước khi mở dữ liệu thật. Evidence: [MODEL_SMOKE_2026-10-24.md](MODEL_SMOKE_2026-10-24.md).
- [x] ZDR metadata + synthetic route check 24/10: public endpoint catalog shows no ZDR-listed Alibaba endpoint for `qwen/qwen3.8-flash`; `qwen/qwen3.8-27b` has eligible ZDR routes. Two repeats of one synthetic case succeeded with Strict ZDR; the repeat with endpoint telemetry identified DekaLLM, not Alibaba infrastructure. This is not accuracy evidence or legal assurance; model default unchanged. See [route evidence](OPENROUTER_ZDR_ROUTE_CHECK_2026-10-24.md).
- [x] Three-repeat Strict-ZDR v3.1 candidate evaluation 24/10: Qwen3.8-27B achieved 15/15, 14/15, 15/15 decision matches; 0/30 expected escalations missed, one routine false escalation, zero system errors; pooled fact exact-match 405/438 (92.47%). R3-04 varied on “Bìa hồ sơ” vs “Bia hò sơ”; semantic repair was attempted but the invalid total evidence remained and all runs failed safely to `ESCALATE_FACT`. Do not change model default yet; this is locked synthetic evidence only. [Details](MODEL_ROUTE_COMPARISON_2026-10-24.md).

- [x] Tách receipt storage thành `IReceiptFileStore`; giữ local filesystem mặc định và thêm adapter GCS private opt-in. Intake retry tạo object chỉ khi key vắng mặt, không ghi đè hash khác; worker/controller đọc qua abstraction, worker kiểm size/hash trước inference. Ca opt-in nối intake thật + SQL với worker ở host thứ hai dùng chung fake GCS client đã chạy thành công. Cấu hình auth hỗ trợ ADC hoặc external JSON key rõ ràng; validation từ chối mode/path mơ hồ và credential JSON trong app root, kể cả `GOOGLE_APPLICATION_CREDENTIALS`. Full Debug/Release **297/297 mỗi cấu hình với 16 LocalDB tests bật** (`AURA_RUN_LOCALDB_INTEGRATION=1`) trên DB tạm; Release build **0 warning/error**, EF không pending model changes, `git diff --check` sạch. Isolated localhost smoke `/healthz=ok` trên DB rỗng đã được dừng và dọn. Đây vẫn là fake GCS; ADC/IAM/bucket thật và two-machine test chưa được thực hiện.
- [ ] Chưa tạo bucket GCS thật, cấp IAM hoặc kiểm credentials thật. Trước đó cần bucket private với uniform bucket-level access + public access prevention và service account riêng; chỉ cấp quyền object ở bucket, không dùng Drive read-only account. Trên GCP ưu tiên ADC với attached identity; local ưu tiên ADC impersonation, không dùng personal ADC rộng quyền.
- [ ] Kiểm tra integration thực trên bucket test: health/read/write/retry/hash/missing-object; sau đó 2 host cùng đọc object + DB chung, mất lease/restart, backup/restore SQL và object đồng bộ. Chưa bật SQL distributed AI gate hoặc tăng workers.
- [ ] Chưa có retention cleanup. Không đặt lifecycle xóa sau một ngày trước khi biết job đã terminal và retention được phê duyệt; bytes cần còn để retry/recovery.
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
- [x] Diễn tập BACKUP/RESTORE trên DB LocalDB tạm chỉ chứa job và audit synthetic; DB phục hồi giữ trạng thái đúng lúc backup, DB/file tạm được dọn. Chưa có diễn tập restore DB hosting **cùng** kho ảnh và kiểm tra RPO/RTO.
- [x] Monitor nội bộ log số pending/processing/failed và tuổi pending lâu nhất mỗi phút; không lộ trên public healthz hoặc ghi ID/file. Adapter log `usage.cost` hợp lệ theo từng response, không ghi chuỗi provider vào cost. Chưa có SLO/cảnh báo, billing reconciliation hay metrics xuyên instance.
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
