# Workflow tiếp nhận hóa đơn sau feedback

## Quyết định hiện tại

AURA giữ một pipeline xử lý chung. Manual upload và mailbox sandbox synthetic đã dùng chung `IInvoiceIngestionService`. Sandbox không đọc Gmail, không giữ OAuth token và mặc định tắt; mục tiêu là kiểm chứng idempotency, quarantine, cursor/outbox và khả năng tái sử dụng pipeline trước khi làm connector thật. Agent tương lai chỉ điều phối công cụ; model không giữ OAuth token, không gọi Gmail hoặc Microsoft Graph trực tiếp, không tự quyết định PASS và không tự gửi hồ sơ ra ngoài.

Lượt chọn nhiều ảnh hiện tại không tạo aggregate `InvoiceBatch`: UI gọi tuần tự cùng endpoint và mỗi ảnh có row/status/audit riêng. Thiết kế này phù hợp demo có giám sát và tránh migration gấp. Chỉ thêm `InvoiceBatch` khi cần progress/retry/report theo lô; khi đó batch phải là aggregate trong SQL, không được tồn tại chỉ trong memory hoặc JavaScript.

## Luồng mục tiêu

```text
Manual upload            Synthetic mailbox sandbox       Gmail/Graph tương lai
       │                 API key + allow-list             OAuth read-only adapter
       └──────────────────────── Invoice source ─────────────────────────┘
                         │
                         ▼
              Ingestion boundary
        validate file + idempotency + audit
                         │
                         ▼
                   SQL queue
                         │
                         ▼
        VLM extraction + JSON Schema
                         │
                         ▼
        Semantic validation + one repair
                         │
                         ▼
          Deterministic C# policy
                         │
              ┌──────────┴──────────┐
              ▼                     ▼
         AUTO_APPROVE          ESCALATE_*
                                    │
                                    ▼
                    Employee and manager review
                                    │
                                    ▼
                      Audit and operations report
```

## Phần đã chạy trong candidate

- Manual upload nhận tối đa 10 JPG hoặc PNG. Mỗi file tạo một SQL job và status URL riêng.
- Worker dùng SQL lease, gọi cùng VLM pipeline, semantic validator và `PolicyDecisionEngine` hiện tại.
- UI không nhận số tiền từ người dùng. Worker dùng final total AI trích xuất sau validation và ghi provenance; tham số `USER_PROVIDED` chỉ được giữ để tương thích test kit/API lịch sử.
- Privacy triage chỉ lưu category và chuyển `ESCALATE_PRIVACY` để con người quyết định.
- Dashboard lấy số liệu ngày, tháng và năm từ SQL.
- Báo cáo CSV theo kỳ dashboard được chọn lấy từ SQL, có UTF 8 BOM để mở trong Excel, không xuất raw AI facts và vô hiệu hóa CSV formula injection từ chuỗi do người dùng cung cấp.
- Mailbox sandbox có unique source identity theo connection/message/attachment, persisted message/attachment/cursor, quarantine và transactional outbox.
- Delivery hợp lệ trả 202 rồi dùng cùng worker. Replay trả record cũ; file không hợp lệ hoặc scanner-required chưa sẵn sàng không tạo AI job.

## Ranh giới của mailbox sandbox và agent tương lai

Sandbox hiện tại nhận multipart synthetic qua endpoint nội bộ có API key. Nó chỉ mô phỏng delivery của provider, không quét inbox, không có webhook và không xác thực người dùng Gmail. Subject/body không được gửi vào VLM; chỉ attachment đã qua gate mới đi tiếp.

Kiến trúc khuyến nghị chia ba tầng:

1. Agent hoặc scheduler chọn hành động tiếp theo như tìm thư mới, đọc metadata hoặc tạo job.
2. Application service kiểm scope, allow list, idempotency, kích thước, định dạng và audit.
3. Connector riêng gọi Gmail API hoặc Microsoft Graph bằng OAuth token được lưu trong secret store.

Agent không được nhận credential thô. Model chỉ phân loại hoặc trích xuất từ nội dung đã được connector và application service giới hạn. Policy C# vẫn là nơi duy nhất chọn route nghiệp vụ. Nhân viên phải review trước khi forward hoặc submit ra ngoài.

## State tối thiểu cho đồng bộ mailbox

- `MailboxConnection`: provider/mailbox synthetic và trạng thái bật. OAuth consent/secret reference chưa có vì Gmail chưa được nối.
- `MailboxSyncCursor`: history ID hoặc delta token, thời điểm sync cuối và trạng thái backfill.
- `EmailMessage`: external message ID, thread ID, sender, subject, received time và processing status.
- `EmailAttachment`: external attachment ID, hash, MIME, kích thước và quarantine status.
- `ReimbursementRequest`: liên kết attachment hợp lệ với SQL queue hiện hành; không tạo loại job song song.
- `IngestionOutboxEvent`: commit cùng request/audit để đánh thức worker sau transaction.

Sandbox cập nhật cursor theo message ID và chống trùng bằng unique provider/message/attachment; SHA-256 vẫn phát hiện nội dung trùng giữa các nguồn. Connector Gmail tương lai phải đọc thay đổi kể từ history cursor thay vì quét lại toàn inbox.

## Gate trước khi nối mailbox thật

| Gate | Điều kiện đạt |
|---|---|
| Identity | OAuth authorization code với PKCE hoặc OIDC phù hợp; token nằm trong secret store; có revoke và refresh lifecycle |
| Least privilege | Chỉ scope đọc cần thiết; allow list mailbox hoặc folder; không có quyền gửi hoặc xóa thư |
| Idempotency | Cùng message và attachment không tạo hai invoice; retry an toàn |
| Quarantine | Chặn attachment sai loại, quá kích thước, archive lồng, macro hoặc malware trước private storage |
| Privacy | Redaction hoặc local route theo policy; retention và deletion có owner; access log có identity |
| Reliability | Cursor, webhook và backfill không làm mất thư; dead letter hoặc retry có giới hạn |
| Human authority | Agent chỉ chuẩn bị batch; nhân viên review rồi mới approve hoặc forward |
| Observability | Đo queue age, sync lag, duplicate rate, quarantine count, provider latency và error rate |

## Thứ tự triển khai an toàn

1. Baseline manual đã giữ nguyên; chạy 203 test và kiểm migration trước publish.
2. Backup/migrate rồi bật mailbox sandbox ở môi trường riêng với một folder/sender allow-list và fixture synthetic.
3. Smoke unauthorized, ACCEPTED, terminal status, replay DUPLICATE và quarantine; ghi evidence bằng script.
4. Tích hợp malware scanner + quarantine storage và pre-inference privacy control trước dữ liệu thật.
5. Thêm Gmail/Graph adapter read-only với OAuth/token vault/revoke/webhook/backfill; vẫn gọi cùng ingestion service.
6. Chỉ mở rộng tenant/mailbox sau capacity test, privacy review, monitoring và kiểm thử revoke token.
