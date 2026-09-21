# Báo cáo kiểm định hiện trạng AURA

Ngày cập nhật: 21/09/2026
Phạm vi: `D:\aura\AURA\AURA`, Track A - The Escalation Referee, ưu tiên Sprint 1

## 1. Kết luận điều hành

**Mã nguồn hiện đã đạt một vertical slice Sprint 1 chạy thật ở local**, nhưng **chưa sẵn sàng bấm nộp** vì chưa có Live URL được xác minh và chưa có bằng chứng hoàn tất video/đúng 5 slide.

Các blocker kỹ thuật ban đầu đã được sửa:

- Verify Harness không còn bị hàm JavaScript trong layout ghi đè và báo sai “quay về trang chủ”.
- Năm fixture/kỳ vọng đã được thay bằng bộ nhất quán 3 auto + 2 escalate.
- Gemini 3.6 dùng Structured Output; policy C# mới quyết định tất định.
- Policy được đọc từ `BUSINESS_RULES.md` ở content root, không còn public trong `wwwroot`.
- Upload có claimed amount, kiểm extension + MIME + magic bytes + 5 MB, tên file ngẫu nhiên và private storage.
- Hóa đơn, metadata, SHA-256, facts JSON, decision và audit được lưu để tra cứu.
- Nhân viên chủ động chuyển một/toàn bộ ca `ESCALATE_*`; hàng đợi quản lý chỉ nhận hồ sơ đã chuyển và có Đồng ý/Từ chối/undo.
- 49 automated tests đạt 49/49 (47 policy/workflow + 2 Test Kit integrity); build 0 warning/0 error.
- Benchmark lịch sử fixture v1 đạt 5/5 trong khoảng 32 giây; fixture v2 đa layout đang chờ đúng một lượt benchmark live để bảo toàn quota.

## 2. Bằng chứng kiểm thử

| Kiểm thử | Kết quả |
|---|---|
| `dotnet build --no-restore` | Đạt, 0 warning, 0 error |
| `dotnet test tests/AURA.Tests/AURA.Tests.csproj --no-restore` | Đạt 49/49 |
| EF migration `PersistReceiptEvidence` | Áp dụng thành công vào LocalDB |
| `GET /`, `/Home/Index`, `/Home/Privacy` | 200 |
| `GET /Applicant`, `/Verify` | 302 về trang chủ |
| `GET /Applicant/Receipt/not-found` | 404 |
| `GET /BUSINESS_RULES.md` | 404 |
| `POST /Verify/RunHarness` thiếu antiforgery | 400 |
| Verify có antiforgery + Gemini thật | Fixture v1: 200, 5/5 PASS; fixture v2: chưa chạy API |
| File upload được lưu và tải lại qua receipt route | 200, MIME `image/png` |
| Gemini transient failure | Đã quan sát HTTP 429/503; retry 3 lần + mã lỗi an toàn + safe fallback |

Benchmark lịch sử fixture v1 ngày 20/09/2026:

| Case | Expected | Actual | Latency |
|---|---|---|---:|
| TC-01 | AUTO_APPROVE | AUTO_APPROVE | 8.745 ms |
| TC-02 | AUTO_APPROVE | AUTO_APPROVE | 9.063 ms |
| TC-03 | AUTO_APPROVE | AUTO_APPROVE | 3.943 ms |
| TC-04 | ESCALATE_FACT | ESCALATE_FACT | 5.697 ms |
| TC-05 | ESCALATE_POLICY | ESCALATE_POLICY | 4.057 ms |

Ảnh v1 đã được thay bằng Test Kit v2 đa layout. Bảng trên chỉ là bằng chứng lịch sử, không phải kết quả của v2 và không được diễn giải là accuracy 100% trên hóa đơn đời thực.

## 3. Nguyên nhân lỗi Verify Harness

`Views/Home/Index.cshtml` đã có hàm `startVerifyHarness()` đúng. Tuy nhiên `_Layout.cshtml` tải sau body và khai báo thêm một hàm cùng tên. Hàm thứ hai gọi tab `verify` không tồn tại, tìm `runTests` không tồn tại, rồi hiện alert “Vui lòng quay lại trang chủ”. Do thứ tự tải JavaScript, hàm sai luôn ghi đè hàm đúng.

Đã xóa hàm trùng trong layout, giữ một implementation ở trang chủ và dùng Tag Helper để sinh route POST có antiforgery token.

## 4. Ma trận route

| Method | Route | Mục đích | Kiểm soát |
|---|---|---|---|
| GET | `/` hoặc `/Home/Index` | Dashboard và thao tác chính | Public Sprint 1 |
| GET | `/Applicant` | Redirect về dashboard | Không có view bị thiếu |
| POST | `/Applicant/UploadReceipt` | Upload + Gemini + quyết định + lưu | Antiforgery, size/MIME/signature |
| GET | `/Applicant/Receipt/{id}` | Tra cứu chứng từ đã lưu | No-store; production cần authorization |
| GET | `/Verify` | Redirect về dashboard | Tránh route tài liệu sai |
| POST | `/Verify/RunHarness` | Chạy 5 ca tuần tự | Antiforgery |
| POST | `/Reviewer/EscalateAction` | Approve/reject/undo | Antiforgery + state/decision validation |
| GET | `/Home/Privacy` | Trang privacy mặc định | Public, nội dung cần hoàn thiện trước production |
| GET | `/Home/Error` | Error page production | No-store |

Route/action hiện khớp view và JavaScript. Không còn action `Applicant.Index` trỏ tới view không tồn tại.

## 5. Đánh giá Track A Sprint 1

| Yêu cầu | Kết quả | Bằng chứng / khoảng trống |
|---|---|---|
| Quy trình thường quy cụ thể | Đạt | Hoàn ứng chi phí |
| Policy rõ ràng | Đạt cơ bản | Root policy + deterministic engine; còn giới hạn tại mục 7 |
| Tối thiểu 15 trường hợp | Đạt | 39 automated; 40 dòng ma trận |
| Mơ hồ / ngoài policy / vượt authority | Đạt | FACT/POLICY/AUTHORITY có test |
| Câu hỏi chuyển tiếp cụ thể | Đạt | Nêu amount/item/vấn đề và CÓ/KHÔNG |
| Không over-escalate | Đạt trên internal fixtures | 3 routine cases auto |
| Không khẳng định input nghi vấn | Đạt | FACT precedence; system failure không auto |
| Verify 3 auto + 2 escalate, một nút | Code/fixture v2 đạt, chờ benchmark | Kết quả 5/5 hiện có chỉ thuộc fixture v1 |
| Expected/actual/pass/timestamp/question | Đạt | JSON + bảng UI |
| Input mới | Đạt local | Upload dùng cùng extraction/policy path |
| Audit input/action/time/reason | Đạt cơ bản | DB AuditLogs + receipt/facts/decision metadata |
| Stop/override/undo | Đạt cơ bản | employee handoff + manager Đồng ý/Từ chối/undo; chưa có authentication |
| Lưu hóa đơn để tra cứu | Đạt local | private path + receipt route; deploy cần persistent volume |
| Demo 90 giây | Đạt local | benchmark khoảng 32 giây |
| Live URL public | **Không đạt/Blocker** | Chưa có URL |
| Public repo đầy đủ lịch sử | Đạt local, chờ push/xác minh public | Không squash/force-push |

## 6. Đánh giá Gemini 3.6 Flash

### Phù hợp cho Sprint 1

- Nhận ảnh và xuất text/JSON; Structured Output ép schema giúp giảm output sai định dạng.
- Latency quan sát 4-9 giây/ảnh, đủ ngân sách 90 giây cho năm ca tuần tự.
- Kết quả 5/5 trên fixture v1 cho thấy model từng đủ dùng cho demo được kiểm soát; fixture v2 phải được xác minh lại đúng một lượt.
- Free Tier phù hợp prototype nếu quota dự án còn đủ.

### Không thể xem là “đủ tuyệt đối”

- Hóa đơn mờ, viết tay, đa trang, nhiều loại tiền, locale số/ngày, ảnh chụp nghiêng và chỉnh sửa tinh vi chưa benchmark độc lập.
- Vision không xác minh tính hợp pháp của MST/chữ ký số chỉ từ pixel.
- Free Tier có thể rate-limit; đã quan sát HTTP 429 và 503 trong kiểm thử thật.
- Ảnh gửi tới dịch vụ cloud; phải công bố và không dùng dữ liệu cá nhân thật khi chưa có đồng thuận.

Kết luận: **Gemini 3.6 Flash là lựa chọn khả thi nhất để kịp Sprint 1**, tốt hơn setup Qwen local sát deadline. Kiến trúc hiện tại cho phép đổi model qua `Gemini:Model` mà không đổi policy engine.

## 7. Đánh giá BUSINESS_RULES.md

### Những điểm đã rõ

- Tách vai trò “extractor” khỏi “approver”; AI không được trả quyết định.
- Chống prompt injection trong ảnh và cấm suy đoán dữ kiện không đọc được.
- Phân biệt seller/buyer tax ID, invoice number, order/booking ID và shipping tracking code; tracking chỉ là bằng chứng logistics.
- Chuẩn hóa ngày, giờ, currency, tổng tiền và line items.
- Nêu giới hạn pháp lý: identifier nhìn thấy không đồng nghĩa invoice thật/hợp pháp.
- Schema bắt buộc được định nghĩa trong code bằng `responseJsonSchema`.
- Precedence nhiều lỗi là FACT -> POLICY -> AUTHORITY.
- Policy version/date và checklist output đã có.

### Khoảng trống còn lại

| Vấn đề | Ưu tiên | Hướng xử lý |
|---|---|---|
| PDF/multi-page/multi-image claim | P1 | Document aggregator + page completeness |
| Tax/e-invoice legal lookup | P1 | External registry, signed evidence, dependency failure policy |
| Ngoại tệ | P1 | Tỷ giá nguồn nào, thời điểm, rounding |
| Sales/client entertainment exception | P1 | Thêm department, purpose và approver matrix vào input |
| Safe category whitelist đầy đủ | P1 | Cost category taxonomy thay vì chỉ keyword cấm |
| Arithmetic inconsistency | P1 | Deterministic line subtotal/tax/discount reconciliation |
| Near-duplicate crop/resize | P1 | Perceptual hash, không chỉ SHA-256 |

Chế độ demo hiện cho phép tái sử dụng ảnh trùng để BGK chạy lại bộ dữ liệu. SHA-256 và cờ `DuplicateDetected` vẫn được ghi audit; production bật `DecisionPolicy:EscalateDuplicateReceipts=true` để chặn bằng `ESCALATE_FACT`.
| Confidence calibration | P1 | Dataset độc lập; threshold theo field thay vì một số tổng |
| Retention/deletion/PII | P0 deploy | Xác định thời hạn, encryption, access role, delete workflow |
| Prompt/policy/model version per event | P1 | Lưu hash/version trong audit event |

Không thể đạt “minh bạch tuyệt đối” chỉ bằng prompt. Tính minh bạch phải đến từ policy engine, version, evidence, audit, test độc lập và disclosure giới hạn.

## 8. Đánh giá ASP.NET Core và cấu trúc project

### Đang áp dụng đúng/nhất quán

- MVC + Razor Views; không pha Blazor/Razor Pages không cần thiết.
- DI qua interface cho repository, audit và Vision extractor.
- Typed `HttpClient`, strongly typed options, timeout và retry.
- EF Core migrations, index status/hash, async I/O.
- ViewComponent cho escalation và audit; lỗi DB không làm sập landing page.
- Tag Helpers + antiforgery cho form thay đổi trạng thái.
- Data annotations và nullable warnings sạch.
- Static files trước routing; HTTPS production; logging console/debug portable.
- Upload file có validation nhiều lớp và canonicalized private path.

### Chưa đủ cho production

- Không authentication/role: manager/audit/receipt route đang public cho demo. CSRF không thay thế authorization.
- Không rate limiting cho upload/Verify; public endpoint có thể làm cạn quota API.
- Không antivirus/decompression dimensions; magic bytes chỉ là lớp cơ bản.
- LocalDB và Windows NanoServer Docker chưa phải cấu hình cloud được chứng minh.
- Receipt file + DB không transactional; DB save lỗi có thể để orphan file.
- Audit event chưa append-only/immutable bằng database permission hoặc hash chain.
- Chưa có optimistic concurrency khi hai quản lý thao tác đồng thời.
- Đã có health endpoint `/healthz`; chưa có telemetry/correlation ID và health check này chưa thăm dò database/Gemini.
- Test hiện khóa policy; chưa có automated integration tests cho controller/DB/file/HTTP client.

## 9. Lưu trữ hóa đơn

Yêu cầu tra cứu đã được đáp ứng local bằng:

- file vật lý ngoài public web root;
- GUID storage name, giữ original filename chỉ làm metadata;
- content type, size, SHA-256 và extracted facts trong DB;
- route tải lại dựa trên request ID, không nhận đường dẫn từ client;
- audit link từ manager/audit UI.

Điểm bắt buộc khi deploy: gắn `ReceiptStorage__Directory` vào persistent volume hoặc thay bằng Azure Blob/S3-compatible storage. Nếu host dùng ephemeral filesystem, tính năng “lưu để tra cứu” sẽ mất sau redeploy dù database còn metadata.

## 10. Backlog theo ưu tiên

### P0 trước deadline Sprint 1

1. Tạo tài nguyên cloud và deploy Linux container với SQL Server/Azure SQL cùng persistent storage; cấu hình secret không commit. Xem `docs/DEPLOYMENT.md`.
2. Chạy migration và smoke-test live URL từ cửa sổ ẩn danh.
3. Chạy một ảnh smoke test rồi đúng một lượt Verify v2 trên live URL; ghi nhận expected/actual và thời gian dưới 90 giây, tránh tiêu hao quota bằng các lượt lặp không cần thiết.
4. Điền Live URL vào README, slide, video và form nộp.
5. Push toàn bộ commit lên public GitHub, xác minh không có secret.
6. Hoàn thiện đúng 5 slide và video tối đa 3 phút.
7. Cân nhắc rate limit tối thiểu cho Verify trước khi public URL.

### P1 tăng độ tin cậy

- Authentication/roles; secure receipt access.
- Blob storage, retention, encryption và deletion workflow.
- Integration tests với fake Gemini handler + temporary database/storage.
- Policy/model/prompt hash trong audit; immutable event design.
- PDF/multi-image, foreign currency, tax lookup, arithmetic reconciliation.
- Benchmark ít nhất 30-100 ảnh độc lập và confusion matrix missed/over-escalation.

### P2 Sprint 2

- Thử nghiệm với ba người dùng thực tế và phản hồi nguyên văn có đồng thuận.
- Đo missed-escalation, over-escalation, review time và workload trước/sau.
- Calibration threshold dựa trên dataset độc lập, không tự học mù từ override.
- Accessibility/mobile/visual regression và dashboard governance.

## 11. Trạng thái cuối

- **Chất lượng code local:** đạt Sprint 1 cơ bản.
- **Verify/model trên fixture nội bộ:** đạt.
- **Business rules:** đủ rõ cho phạm vi JPG/PNG đơn trang hiện tại; chưa đủ production/toàn bộ biên thực tế.
- **Sẵn sàng nộp:** chưa, vì Live URL + slide/video chưa có bằng chứng hoàn tất.
