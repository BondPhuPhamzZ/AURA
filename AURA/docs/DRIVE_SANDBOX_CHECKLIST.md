# Drive sandbox: checklist trạng thái và bước tiếp theo

Cập nhật sau lần kiểm tra local ngày 22/10/2026. Checklist này mô tả **một
prototype Development-only với fixture tổng hợp**, không phải phê duyệt dùng
hóa đơn cá nhân hay tuyên bố production-ready.

Hardening tiếp theo cùng ngày: Drive GET đã có retry giới hạn cho 429/5xx và
audit mới giảm dữ liệu nhận dạng; **218/218** test pass. LocalDB chạy được
ngoài sandbox của phiên công cụ; DB thử riêng có health `ok`, dashboard 200.
E2E Drive sau thay đổi và batch 5/20/100 chưa chạy. Xem
[báo cáo evidence mới](DRIVE_PIPELINE_HARDENING_2026-10-22.md); các số
216/216 và E2E bên dưới là snapshot trước hardening này.

## Đã triển khai và có evidence

- [x] Connector Drive bị tắt mặc định; runtime policy chặn ngoài Development,
  khi thiếu loopback binding, hoặc khi malware scanner còn bắt buộc.
- [x] Service account chỉ có quyền đọc folder test Restricted; connector dùng
  scope `drive.readonly`, kiểm tra folder/parent, version, MD5, kích thước,
  MIME và magic bytes trước khi intake. Tải file có giới hạn byte.
- [x] Không ghi key/ID/tên file/raw facts vào log hoặc Git; key JSON nằm ngoài
  repo và ACL của đúng file key đã được giới hạn trên máy test.
- [x] Cùng file ID + version tạo một request ID ổn định; poll/restart không tạo
  thêm SQL job. Intake đi qua cùng boundary và audit `AI_QUEUED` như manual
  intake.
- [x] E2E local đã gọi Drive API và OpenRouter thật trên fixture synthetic:
  3 lần xử lý hoàn tất (2 fixture khác nhau, 1 lần chạy lại để kiểm tra sửa
  false positive), 1 file `.txt` bị từ chối trước SQL intake, restart không
  tạo job trùng. Chi tiết và giới hạn từng phép đo ở
  [evidence E2E](DRIVE_SANDBOX_EVIDENCE_TEMPLATE.md).
- [x] Phone triage không còn coi mã số thuế 10 chữ số là số điện thoại trong
  trường số liệu; fixture sửa lại chuyển `AUTO_APPROVE`, còn audit cũ được giữ.
- [x] Rejection log hiện chống ghi trùng cho cùng `sourceHash + reasonCode`,
  kể cả sau restart. Test worker chạy nhiều chu kỳ poll và xác nhận chỉ một
  dòng được ghi.
- [x] Fake-API boundary test: 101 entries với giới hạn 100 làm scan fail-closed;
  không download, không private file và không intake một phần. Đây không phải
  load test 100 hóa đơn.
- [x] Regression suite: **216/216 pass** (`dotnet test ... -c Release
  --no-restore`); Release build: **0 warning, 0 error**; EF xác nhận không có
  pending model changes. E2E Drive/OpenRouter là bằng chứng riêng, không được
  suy ra chỉ từ unit test.
- [x] Connector được tắt lại sau E2E; DB local test là DB riêng, không phải
  hosting/live. Không publish connector lên live.

## Chưa làm / chưa có đủ bằng chứng

- [ ] Batch 5 → 20 → 100 file: queue wait, terminal/error counts, throughput,
  rate limit, memory/disk và latency p50/p95. Chưa có capacity/SLA claim.
- [ ] Hành vi API lỗi thực tế: 401/403/revoked key, quota/429, timeout,
  transient download error và retry/backoff; kiểm tra app vẫn hoạt động an toàn.
- [ ] Thử file giả MIME/PDF, file quá cỡ và file bị đổi/move giữa metadata với
  download qua Drive thật. Các nhánh này hiện có unit/fake-API coverage chứ
  chưa có live Drive evidence.
- [ ] Malware scanner/quarantine, decoder ảnh cách ly, và quy trình duyệt file
  bị cách ly. Hiện `RequireMalwareScanner=true` là mặc định an toàn; bypass chỉ
  được cho fixture tổng hợp local.
- [ ] User identity, SSO/RBAC, phân quyền xem/tải ảnh, consent và audit truy cập.
- [ ] Quyết định retention/backup/restore (kể cả ảnh, DB, audit, rejection
  log); mục tiêu “giữ một tuần” chưa được biến thành chính sách xóa/kiểm chứng.
- [ ] Xác nhận độc lập chính sách dữ liệu/ZDR đúng theo model-route đang dùng;
  `data_collection=deny` hoặc health status không tự chứng minh Strict ZDR.
- [ ] Secret vault/workload identity và quy trình rotate/revoke service account;
  key file hiện chỉ phù hợp cho local test.
- [ ] Multi-instance/distributed idempotency, locking, rate limiting và
  monitoring/alerting. Connector hiện chỉ là một worker/process local.
- [ ] Pilot có consent với bộ dữ liệu được doanh nghiệp phê duyệt, tiêu chí
  chấp nhận đã khóa trước, human review và rollback.

## Tiến độ kiểm thử đề xuất

1. **Giữ an toàn baseline:** xác nhận `DriveSandbox:Enabled=false`,
   `RequireMalwareScanner=true`, app không chạy connector khi không test; giữ
   folder Restricted và chỉ synthetic fixtures. Không đưa hóa đơn thật vào.
2. **Smoke local sau thay đổi:** chạy Release build, 216+ unit tests, kiểm EF
   no-pending-model-change; dùng SQL local riêng. Kiểm `/healthz`, dashboard,
   và xác nhận connector vẫn tắt.
3. **Một file hợp lệ:** bật sandbox tạm theo runbook, dùng một JPG/PNG synthetic
   mới; xác nhận một SQL row, `AI_QUEUED`, trạng thái terminal, private storage
   và không có raw PII trong log. Tắt connector sau test.
4. **Idempotency/restart:** poll cùng version và restart worker; số request/audit
   không tăng. Sửa nội dung tạo version mới phải được xem như input mới; không
   sửa file trong lúc đang xử lý.
5. **Negative tests:** từ chối extension/MIME/magic bytes sai, quá cỡ, sai
   folder, source đổi giữa lúc tải; quá ngưỡng file fail-closed không intake
   một phần; không tạo SQL job. Kiểm rejection log chỉ có hash/reason/timestamp
   và không phình do poll/restart lặp.
6. **Batch tăng dần:** chỉ sau khi 1-file và negative tests pass, chạy 5, rồi
   20, rồi tối đa 100 synthetic images; mỗi cấp ghi số nguồn, accepted,
   terminal, error, p50/p95 latency, max queue age và dung lượng storage. Dừng
   nếu có mất/trùng job, raw secret/PII trong log, tăng lỗi hoặc provider quota.
7. **Chỉ sau phê duyệt riêng:** thiết kế scanner/quarantine, identity/RBAC,
   retention/restore, privacy/provider, secret management và vận hành trước
   bất kỳ pilot dữ liệu cá nhân hay publish production nào.

## Quyết định hiện tại

**GO có điều kiện** cho thử nghiệm local với fixture tổng hợp và folder test
Restricted. **NO-GO** cho hóa đơn cá nhân/doanh nghiệp, public demo intake,
publish Drive connector lên hosting, batch hàng trăm/hàng nghìn, hoặc tuyên bố
production-ready. Bộ test hiện tại và một E2E nhỏ chứng minh các nhánh đã thử
hoạt động; chúng chưa thay thế load, security, privacy, operational hay
compliance review.
