# Drive sandbox evidence — PENDING live folder test

Đây là form ghi kết quả, không phải tuyên bố đã chạy Drive API thật.
Không ghi folder ID, email, tên file hóa đơn, token hay dữ liệu ảnh vào Git.

| Gate | Kỳ vọng | Kết quả/UTC | Evidence an toàn |
|---|---|---|---|
| Folder Restricted + service account Viewer | Không có public link | PENDING | Người sở hữu xác nhận |
| /healthz local + SQL riêng | 200, DB up-to-date, migration 0 | PENDING | JSON đã ẩn cấu hình |
| Một PNG synthetic | 1 SQL row, AI_QUEUED, terminal | PENDING | Request ID rút gọn, status |
| Restart/poll lại | Không có row thứ hai | PENDING | Count trước/sau |
| JPG thứ hai | Job độc lập | PENDING | Count, status |
| PDF/ảnh sai/oversize | Không queue | PENDING | Reason code, không có bytes |
| AI/semantic/policy | Status và audit hợp lệ | PENDING | Route/category, không raw facts |
| 5 → 20 → 100 ảnh | Count terminal, queue age, errors | PENDING | Bảng số liệu |

Chỉ sau khi điền kết quả thật và review bảo mật mới đồng bộ README/deployment
claim; kiểm thử giả lập trong xUnit không thay thế E2E Google Drive.
