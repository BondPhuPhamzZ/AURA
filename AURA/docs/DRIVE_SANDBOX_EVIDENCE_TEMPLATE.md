# Drive synthetic sandbox — E2E local ngày 22/10/2026

Đã gọi Google Drive API và OpenRouter thật với **fixture synthetic** trên
`127.0.0.1:5097` và LocalDB riêng. Đây không phải test hosting, tải lớn,
độ chính xác trên hóa đơn thật hay chứng nhận production-ready. Không ghi
folder ID, service-account email, JSON key, token hoặc raw facts vào Git.
JSON key nằm ngoài repo; sau khi phát hiện ACL kế thừa cho nhóm Windows rộng,
quyền trên đúng file key đã được giới hạn cho chủ file, SYSTEM và
Administrators. Không sửa nội dung key.

| Gate | Kỳ vọng | Kết quả/UTC | Evidence an toàn |
|---|---|---|---|
| Folder Restricted + service account Viewer | Không có public link | Chủ sở hữu xác nhận Restricted/Viewer; AURA token + folder GET HTTP 200 | Quyền General access chưa được audit độc lập |
| /healthz local + SQL riêng | 200, DB up-to-date, migration 0 | PASS: `status=ok`, DB true, migration 0, `driveSandboxConfigured=true`; dashboard HTTP 200 | Chỉ khi connector được bật tạm để test |
| JPG synthetic đầu tiên | 1 SQL row, AI_QUEUED, terminal | PASS: request `d0bdf59a`, `COMPLETED/AUTO_APPROVE`, OpenRouter, 17,249 ms | 1 queued + 1 processed audit |
| Restart/poll lại | Không có row thứ hai | PASS: vẫn 1 row/1 queued audit | Source ID tất định theo file ID + version |
| File sai định dạng | Không queue | PASS: `.txt` có `UNSUPPORTED_FILE`, row/audit không tăng | Chưa test PDF giả MIME hoặc >5 MB qua Drive thật |
| JPG synthetic thứ hai | Job độc lập | PASS vận hành: request `df957656`, `COMPLETED/ESCALATE_PRIVACY`, 7,591 ms | Đây là **false positive nghiệp vụ** so với manifest `AUTO_APPROVE` |
| Truy nguyên privacy false positive | Không nhận nhầm mã số thuế là phone | PASS sau sửa: mã số thuế 10 chữ số không còn vào phone fallback; 214/214 tests | Bản audit cũ được giữ nguyên; heuristic privacy không đảm bảo phát hiện mọi PII |
| Chạy lại fixture thứ hai bằng file ID mới | Route đúng manifest hiện hành | PASS: request `9e384faf`, `COMPLETED/AUTO_APPROVE`, privacy=false, 10,455 ms | 3 SQL rows tổng cộng; 3 queued, 3 processed |
| 5 → 20 → 100 ảnh | Count terminal, queue age, errors | CHƯA ĐO | Không claim capacity hoặc production SLA |

Sau test, app local đã dừng và user-secrets được trả về `Enabled=false`,
`RequireMalwareScanner=true`. Không publish connector lên hosting. Folder test
hiện còn fixture JPG và file `.txt` thử từ chối; nếu bật lại, file `.txt` có
thể được ghi mã từ chối mỗi lần scan, nhưng không vào SQL queue.

E2E này chỉ đủ cơ sở công bố **local synthetic sandbox hoạt động**. Trước
production còn malware quarantine, auth/RBAC cho ảnh và thao tác, consent,
retention/backup, provider privacy, quản lý key, distributed coordination và
capacity test. xUnit không thay thế E2E, và E2E này không thay thế pilot thật.
