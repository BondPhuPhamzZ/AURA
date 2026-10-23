# Checklist hiện hành và mốc publish lịch sử

> **23/10 — NO-GO publish bản source mới và đổi model hosting.** Checklist
> publish phía dưới là mốc lịch sử 17–21/10, không còn là lệnh triển khai
> candidate hiện tại. Xem [trạng thái hiện hành](CURRENT_PROJECT_STATUS.md)
> và [đối chứng model](MODEL_ROUTE_COMPARISON_2026-10-23.md).

Việc cần làm trước khi mở lại gate publish/pilot:

- [x] Khóa schema đầu vào, field backend-only, metadata/log lỗi provider và tín hiệu Semantic có thể chứa dữ liệu cá nhân; chặn response provider quá 2 MiB. Lượt mới nhất 24/10: Release/Debug 280/280, LocalDB opt-in 11/11, Release build 0 warning/error, EF không pending model change. `NU1900` không phải vulnerability audit thành công.
- [x] Đối chứng cùng 15 synthetic: Qwen3.6 Flash 15/15 quyết định, Qwen3.8 Flash 13/15; đây là bộ đã biết, không phải holdout mù.
- [ ] Tạo tập synthetic mới có nhãn độc lập, tách riêng tổng bị mờ với mô tả mặt hàng chung chung; đo missed escalation, critical fields, lỗi provider và latency qua nhiều lượt.
- [ ] Chỉ gửi 15 ảnh thật qua model thay thế sau khi chủ dữ liệu chấp thuận rõ route mới; giữ ảnh và kết quả chi tiết ngoài Git.
- [ ] Xác minh route ZDR, kiểm soát truy cập, lưu trữ riêng dùng chung, AI lease/fencing phân tán, malware/quarantine, retention, restore drill và tải nhiều instance trước pilot/scale.
- [x] Xác minh `rowversion` chặn stale write: LocalDB hai worker, A bị giữ qua thời điểm lease hết hạn, B reclaim và hoàn tất; A nhận concurrency exception, chỉ còn một kết quả/audit. Đây chưa phải bằng chứng an toàn để bật nhiều worker vì giới hạn AI/cooldown và ảnh vẫn theo process/host.
- [x] Thêm SQL application lock opt-in và cooldown provider dùng chung trong SQL; cooldown phân tán tính theo `SYSUTCDATETIME()` để tránh lệch giờ giữa host, được kiểm chứng bằng LocalDB khi caller truyền thời gian lệch ±nhiều năm. LocalDB còn kiểm tra hai coordinator, cancellation, session bị kết thúc, và cooldown read/extend/clear. Watchdog phát `AI_GATE_LOST` để hủy provider call/giữ job retryable. Migration `20261023181204_AddSharedVisionProviderCooldown` cần được backup/áp dụng có kiểm soát trước khi bật gate; chưa áp dụng hosting. Giữ `Vision__UseSqlDistributedGate=false` tới khi có shared private storage và chaos/load test trên hai máy chủ thật; phát hiện mất session theo chu kỳ 5 giây không đảm bảo provider dừng tức thời.
- [x] Worker đối chiếu size/SHA-256 ảnh đã lưu với SQL trước khi gọi AI; ca file bị sửa dừng fail-safe. Chưa có bằng chứng private share hoạt động xuyên host, ACL/backup/restore hoặc tính bất biến trên mọi loại storage.
- [x] Smoke Development localhost sau khi xác minh 0 job đang chờ và tắt Drive: `/healthz` OK, DB/schema/storage và key presence sẵn sàng; không gọi model, không publish.
- [x] Diễn tập BACKUP/RESTORE trên DB LocalDB tạm chỉ chứa job và audit synthetic; DB phục hồi giữ trạng thái đúng lúc backup, DB/file tạm được dọn. Chưa có diễn tập restore DB hosting **cùng** kho ảnh và kiểm tra RPO/RTO.
- [x] Monitor nội bộ log số pending/processing/failed và tuổi pending lâu nhất mỗi phút; không lộ trên public healthz hoặc ghi ID/file. Adapter log `usage.cost` hợp lệ theo từng response, không ghi chuỗi provider vào cost. Chưa có SLO/cảnh báo, billing reconciliation hay metrics xuyên instance.
- [x] Hai host giả lập khác content root cùng dùng thư mục ảnh tuyệt đối private và DB chung; host B xử lý được job. Chưa có test share mạng/ACL/đứt kết nối hay backup ảnh trên hai máy thật.
- [x] Intake source-key ghi ảnh qua file tạm, retry chỉ dùng lại file đích nếu size/SHA-256 khớp; không ghi đè file thiếu/sai byte. Chưa có dọn `.tmp` sau crash, atomicity SQL + filesystem hoặc đối chứng SMB thật.
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
