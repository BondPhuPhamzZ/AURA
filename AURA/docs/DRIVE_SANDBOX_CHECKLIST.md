# Drive sandbox: checklist trạng thái và bước tiếp theo

> **Xác minh hiện hành 26/10:** thư mục cấu hình có 3 JPEG trực tiếp, 2 folder con
> và 1 TXT. E2E mới trên DB/ảnh riêng: 3/3 ảnh hoàn tất qua approval gate 49 hash,
> Qwen3.8-27B/Strict ZDR request; 3 job/6 audit không tăng sau restart. Không chấm
> accuracy hoặc quét folder con. Launcher hỗ trợ `-EnableSyntheticDrive` rõ ràng;
> mặc định vẫn tắt, không đổi User Secrets. Full Release sau packaging guard
> 371/371, không skip. Profile regression đang tắt duplicate escalation; một ảnh
> trùng được phát hiện nhưng vẫn AUTO theo cấu hình. Xem [assessment hiện hành](READINESS_AND_SCALE_ASSESSMENT_2026-10-23.md)
> và [runbook](RUNBOOK.md); các số 22–23/10 bên dưới là lịch sử.

Cập nhật sau lần kiểm tra local đêm 22–23/10/2026. Checklist này mô tả
**Drive prototype Development-only với fixture tổng hợp** và một lần test
manual intake có consent riêng; không phải phê duyệt dùng dữ liệu người dùng
đại trà hay tuyên bố production-ready.

Sau hardening, batch 5/20/100 đã được chạy qua Drive API, SQL queue,
OpenRouter và policy trên localhost. Lượt 100 đầu phát hiện 4 job trùng do
Drive metadata version đổi; bản sửa đã chạy lại trên DB sạch: đúng 100 job,
98 hoàn tất và 2 fail-safe, không tăng job/audit sau poll và restart. 15 ảnh
thật có consent cũng hoàn tất qua manual intake local, nhưng không có nhãn
khớp hash để tính accuracy. Xem [báo cáo kiểm thử MVP](DRIVE_MVP_VALIDATION_2026-10-22.md).
E2E 3 ca bên dưới là snapshot lịch sử; tại mốc đó Release suite **231/231 pass**.
Storage dưới `wwwroot` bị chặn và gate hash xác nhận 70 bản sao trong bộ 100;
xem [evidence hardening 23/10](STORAGE_AND_DATASET_GATE_2026-10-23.md).

## Đã triển khai và có evidence

- [x] Connector Drive bị tắt mặc định; runtime policy chặn ngoài Development,
  khi thiếu loopback binding, hoặc khi malware scanner còn bắt buộc.
- [x] Service account chỉ có quyền đọc folder test Restricted; connector dùng
  scope `drive.readonly`, kiểm tra folder/parent, version, MD5, kích thước,
  MIME và magic bytes trước khi intake. Tải file có giới hạn byte.
- [x] Không ghi key/ID/tên file/raw facts vào log hoặc Git; key JSON nằm ngoài
  repo và ACL của đúng file key đã được giới hạn trên máy test.
- [x] Cùng folder ID + file ID + MD5 nội dung tạo một request ID ổn định;
  version vẫn được kiểm để phát hiện file đổi khi tải. Metadata-only edit,
  poll và restart không tạo thêm SQL job. Intake dùng cùng boundary và audit
  `AI_QUEUED` như manual intake. Lưu ý dùng DB test mới sau đổi thuật toán ID.
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
- [x] Regression suite: **231/231 pass** (`dotnet test ... -c Release
  --no-restore`); Release build: **0 warning, 0 error**; EF xác nhận không có
  pending model changes. E2E Drive/OpenRouter là bằng chứng riêng, không được
  suy ra chỉ từ unit test.
- [x] Connector được tắt lại sau E2E; DB local test là DB riêng, không phải
  hosting/live. Không publish connector lên live.
- [x] Batch tăng dần 5 → 20 → 100 đã chạy. Bản sửa nhận đúng 100/100,
  30 nội dung khác nhau, 200 audit event; 98 `COMPLETED`, 2
  `ESCALATE_SYSTEM_ERROR` fail-safe. Poll/restart không sinh job trùng.
- [x] 15 ảnh thật có consent qua localhost manual intake: 15/15 completed,
  0 lỗi provider; ảnh không vào Drive. Đây không phải blind holdout/accuracy
  claim vì hash không khớp manifest nhãn cũ.

## Chưa làm / chưa có đủ bằng chứng

- [ ] Đo memory peak, disk theo thời gian và capacity/SLA trên 100 ảnh **độc
  lập** rồi 1.000 ảnh; batch vừa qua chỉ có 30 nội dung khác nhau. Latency,
  queue wait, terminal và lỗi đã ghi ở báo cáo MVP; chưa có SLA claim.
- [ ] Hành vi API lỗi thực tế: 401/403/revoked key, quota/429, timeout,
  transient download error và retry/backoff; kiểm tra app vẫn hoạt động an toàn.
- [ ] Thử file giả MIME/PDF, file quá cỡ và file bị đổi/move giữa metadata với
  download qua Drive thật. Các nhánh này hiện có unit/fake-API coverage chứ
  chưa có live Drive evidence.
- [ ] Malware scanner/quarantine, decoder ảnh cách ly, và quy trình duyệt file
  bị cách ly. Hiện `RequireMalwareScanner=true` là mặc định an toàn; bypass chỉ
  được cho fixture tổng hợp local.
- [ ] User identity, SSO/RBAC, phân quyền xem/tải ảnh, consent và audit truy cập.
- [ ] Retention/backup/restore cho dữ liệu thật, Drive source và rejection log còn
  mở. Local SQL/ảnh/facts/audit synthetic đã có chính sách 7 ngày và restore drill;
  không áp dụng tự động lên ảnh nguồn Drive, DB cũ hoặc hosting.
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
2. **Smoke local sau thay đổi:** chạy Release build, 231+ unit tests, kiểm EF
   no-pending-model-change; dùng SQL local riêng. Kiểm `/healthz`, dashboard,
   và xác nhận connector vẫn tắt.
3. **Một file hợp lệ:** bật sandbox tạm theo runbook, dùng một JPG/PNG synthetic
   mới; xác nhận một SQL row, `AI_QUEUED`, trạng thái terminal, private storage
   và không có raw PII trong log. Tắt connector sau test.
4. **Idempotency/restart:** poll và restart worker; số request/audit không tăng
   khi chỉ đổi metadata. Sửa **bytes** tạo checksum mới mới được xem là input
   mới; không sửa file trong lúc đang xử lý.
5. **Negative tests:** từ chối extension/MIME/magic bytes sai, quá cỡ, sai
   folder, source đổi giữa lúc tải; quá ngưỡng file fail-closed không intake
   một phần; không tạo SQL job. Kiểm rejection log chỉ có hash/reason/timestamp
   và không phình do poll/restart lặp.
6. **Batch tăng dần:** đã chạy 5/20/100 và phát hiện, sửa, kiểm lại lỗi nhân
   job. Với lần đo tiếp theo dùng 100 ảnh **khác nhau**, giữ ca lỗi trong mẫu,
   chốt ground truth trước và đo thêm tài nguyên/cost. Không gọi 2/100 lỗi
   kỹ thuật là tỷ lệ sai quyết định.
7. **Chỉ sau phê duyệt riêng:** thiết kế scanner/quarantine, identity/RBAC,
   retention/restore, privacy/provider, secret management và vận hành trước
   bất kỳ pilot dữ liệu cá nhân hay publish production nào.

## Quyết định hiện tại

**GO có điều kiện** cho MVP local với fixture tổng hợp/folder Restricted và
thử nghiệm ảnh thật có consent từng lần. **NO-GO** cho pilot công khai với dữ
liệu cá nhân/doanh nghiệp, publish Drive connector lên hosting, quy mô 1.000,
hoặc tuyên bố production-ready. Batch 100 chứng minh một worker xử lý tuần tự
và fail-safe, nhưng P95 chờ hàng đợi ~14 phút, có lỗi provider/model, chưa
đánh giá accuracy độc lập hoặc các gate security/privacy/compliance.
