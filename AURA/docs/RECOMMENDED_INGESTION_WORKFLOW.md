# Workflow tiếp nhận hóa đơn sau feedback

## Quyết định hiện tại

AURA giữ một pipeline xử lý chung. Manual upload là nguồn đã triển khai. Mailbox hoặc folder là nguồn bổ sung trong roadmap và chỉ được nối khi hoàn tất các gate về identity, quyền truy cập và quarantine. Agent tương lai chỉ điều phối công cụ; model không giữ OAuth token, không gọi Gmail hoặc Microsoft Graph trực tiếp, không tự quyết định PASS và không tự gửi hồ sơ ra ngoài.

Lượt chọn nhiều ảnh hiện tại không tạo aggregate `InvoiceBatch`: UI gọi tuần tự cùng endpoint và mỗi ảnh có row/status/audit riêng. Thiết kế này phù hợp demo có giám sát và tránh migration gấp. Chỉ thêm `InvoiceBatch` khi cần progress/retry/report theo lô; khi đó batch phải là aggregate trong SQL, không được tồn tại chỉ trong memory hoặc JavaScript.

## Luồng mục tiêu

```text
Manual upload đã chạy       Mailbox connector roadmap
          │                           │
          └──────── Invoice source ───┘
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
- Báo cáo CSV tháng hiện tại lấy từ SQL, có UTF 8 BOM để mở trong Excel, không xuất raw AI facts và vô hiệu hóa CSV formula injection từ chuỗi do người dùng cung cấp.

## Ranh giới của mailbox agent

Kiến trúc khuyến nghị chia ba tầng:

1. Agent hoặc scheduler chọn hành động tiếp theo như tìm thư mới, đọc metadata hoặc tạo job.
2. Application service kiểm scope, allow list, idempotency, kích thước, định dạng và audit.
3. Connector riêng gọi Gmail API hoặc Microsoft Graph bằng OAuth token được lưu trong secret store.

Agent không được nhận credential thô. Model chỉ phân loại hoặc trích xuất từ nội dung đã được connector và application service giới hạn. Policy C# vẫn là nơi duy nhất chọn route nghiệp vụ. Nhân viên phải review trước khi forward hoặc submit ra ngoài.

## State tối thiểu cho đồng bộ mailbox

- `MailboxConnection`: tenant hoặc account, provider, scope đã cấp, trạng thái consent và tham chiếu secret.
- `MailboxSyncCursor`: history ID hoặc delta token, thời điểm sync cuối và trạng thái backfill.
- `EmailMessage`: external message ID, thread ID, sender, subject, received time và processing status.
- `EmailAttachment`: external attachment ID, hash, MIME, kích thước và quarantine status.
- `InvoiceProcessingJob`: liên kết attachment với SQL queue hiện hành.

Không quét lại toàn bộ mailbox mỗi lần. Connector đọc thay đổi kể từ cursor, chống trùng bằng provider ID kết hợp SHA 256 và chỉ tạo job sau khi file vượt qua quarantine.

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

1. Giữ candidate hiện tại làm baseline và hoàn tất publish, health, synthetic smoke.
2. Pilot connector bằng mailbox sandbox chỉ đọc, một folder allow list và dữ liệu synthetic.
3. Tạo sync cursor, idempotency và quarantine trước khi bật AI extraction.
4. Nối attachment hợp lệ vào cùng ingestion boundary và SQL queue; không tạo AI pipeline thứ hai.
5. Thêm batch review và báo cáo chi tiết. Forward vẫn là thao tác của nhân viên.
6. Chỉ mở rộng tenant hoặc mailbox sau capacity test, privacy review và kiểm thử revoke token.
