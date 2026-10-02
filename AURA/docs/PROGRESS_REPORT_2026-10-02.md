# Báo cáo tiến độ AURA ngày 02 10 2026

## 1. Kết luận hiện tại

AURA đã hoàn thành vertical slice dùng để trình diễn: người dùng tải ảnh chứng từ, server trả `202 Accepted`, job được lưu trong SQL và xử lý nền, Vision AI trích xuất dữ kiện, semantic validator kiểm tra và sửa đúng một lần khi cần, `PolicyDecisionEngine` C# tạo quyết định tất định, ngoại lệ đi qua nhân viên và quản lý, còn toàn bộ hành động được ghi audit.

Baseline hiện tại đủ điều kiện để tiếp tục diễn tập Chung kết trên laptop. Baseline chưa phải hệ thống production và chưa đủ cơ sở để công bố accuracy trên thị trường. Các cổng còn mở gồm holdout hóa đơn thật đa dạng đã ẩn danh, phản hồi người dùng, smoke Live URL nếu vẫn dùng URL public và ba lượt diễn tập có bấm giờ trên commit chốt.

## 2. Những thay đổi chính sau feedback Sprint 1

### 2.1 Xử lý nền bền vững

- Upload hợp lệ được lưu thành hồ sơ `PENDING` cùng audit `AI_QUEUED` rồi trả HTTP 202.
- Worker claim job bằng lease trong database. Refresh không làm mất job; sau restart, worker có thể tiếp tục hoặc reclaim job hết lease.
- SQL là nguồn sự thật. Channel trong bộ nhớ chỉ đánh thức worker, không giữ job duy nhất.
- Worker dùng exponential backoff có trần khi database tạm mất kết nối để tránh bão log.

### 2.2 Ranh giới quyết định rõ ràng

- Qwen3-VL chỉ đọc ảnh và trả facts theo JSON Schema.
- Semantic validator kiểm quan hệ giữa loại chứng từ, ngày, định danh, tiền, giảm giá và dòng hàng.
- Backend chỉ repair output mâu thuẫn đúng một lần; claimed amount không được dùng để dẫn model đến đáp án.
- `PolicyDecisionEngine` áp dụng thứ tự `FACT -> POLICY -> AUTHORITY`. Model không có quyền tự phê duyệt.
- Lỗi provider hoặc output không đáng tin cậy chuyển sang con người, không tạo kết quả nghiệp vụ giả.

### 2.3 Hardening dữ liệu chứng từ

- Tách `invoiceNumber`, `receiptNumber` và `transactionReference`; ShopID, POS, MID, TID hoặc pager không được giả làm mã giao dịch.
- Chuẩn hóa ngày chứng từ giấy khi model đặt ngày mua duy nhất vào `transactionDate`; `completionDate` không bao giờ được nâng thành ngày hóa đơn.
- Thêm `discountAmount` và đối chiếu `subtotal + tax - discount = totalAmount` hoặc biến thể subtotal đã gồm thuế.
- Tổng nhân viên khai được so với số tiền cuối cùng phải trả, không phải tổng trước giảm giá.
- Giá trị điểm hoặc số không tác động bị nhận nhầm là giảm giá chỉ được loại khi các tổng độc lập đã chứng minh phép tính không đổi.
- Hóa đơn giấy có `lineItems=[]` không còn được hiểu là “không có hạng mục cấm”. Hệ thống đọc lại một lần; nếu vẫn thiếu, thêm missing field và warning, giới hạn confidence ở 0.69 rồi `ESCALATE_FACT`.

### 2.4 Provider, fallback và khả năng vận hành

- OpenRouter/Qwen3-VL-8B là provider chính của baseline demo.
- Ollama/Qwen3-VL-4B là cấu hình local tùy chọn. Không cần cài Ollama để BGK chạy baseline.
- Fallback có allowlist lỗi hạ tầng, gọi tối đa một lần, ghi provider thực sự phục vụ và có circuit breaker/cooldown.
- Fallback mặc định tắt trong benchmark để không trộn hai provider và không che lỗi semantic/schema.
- `/healthz` kiểm AI configuration, policy, database, migration và receipt storage mà không gọi API trả phí.
- `Test-DemoReadiness.ps1` có `-OutputPath` để tự tạo transcript; người dùng không cần ghép `Tee-Object` hoặc tự khai báo biến đường dẫn.

### 2.5 UX, human workflow và audit

- UI polling status sau HTTP 202 và tiếp tục theo dõi sau refresh.
- Bảng escalation đọc lại database định kỳ; hồ sơ cũ không cần một request mới để “đánh thức” giao diện.
- UI có thông báo processing/pending rõ ràng và giới hạn chiều cao audit để giữ dashboard gọn.
- Nhân viên chuyển tiếp từng hồ sơ; quản lý có thể đồng ý, từ chối và hoàn tác quyết định gần nhất.
- Audit lưu provider, trạng thái, câu hỏi, quyết định và chuỗi sự kiện. Audit hiện là append-oriented ở tầng ứng dụng, chưa phải WORM/tamper-proof ở tầng database.

## 3. Bằng chứng đã xác minh

| Cổng | Kết quả | Cách diễn giải đúng |
|---|---|---|
| Release build | 0 warning, 0 error | Source compile sạch trên baseline hiện tại |
| Automated tests | 102/102 pass | Bảo vệ code path; không phải 102 hóa đơn AI |
| Official Verify sau guard | 5/5 pass | 3 `AUTO_APPROVE`, 1 `ESCALATE_FACT`, 1 `ESCALATE_POLICY` |
| Phê La bị che vùng món | `ESCALATE_FACT` | Fail-safe đúng; ca cũ auto-approve được giữ làm incident evidence |
| Vinamilk đủ dòng hàng | `AUTO_APPROVE` | 7 dòng hàng, giảm 2.828, tổng cuối 180.286, không semantic issue |
| Restart persistence | Pass | Status, facts, ảnh receipt và audit còn sau restart |
| Human workflow | Pass | Có bằng chứng YES, NO và UNDO trong audit |
| Concurrent smoke sau guard | 5/5 hoàn tất | Accepted P95 530 ms; end-to-end P95 39.380 s; dưới 90 s |
| Receipt thật bổ sung | 3/3 đúng nhánh | Oppa AUTO có repair; Texas AUTO không repair; Phê La che món FACT |
| Fallback regression cô lập | 1/1 hoàn tất | OpenRouter `AI_NOT_CONFIGURED` → Ollama; E2E 84.425 ms; audit provider đúng |
| Judge set lịch sử OpenRouter | 15/15 decision, 70/75 field | Synthetic regression trước các guard mới; không phải production accuracy |
| Judge set lịch sử Ollama | 14/15 decision, 73/75 field | Có missed escalation TK-12; chưa đạt safety gate để làm primary |

`semanticRepairApplied=false` trên một ca đúng ngay lượt đầu là tín hiệu tốt, nhưng không phải điều kiện pass bắt buộc. Một ca có repair vẫn pass nếu chỉ repair tối đa một lần, facts cuối nhất quán, validation issues rỗng và decision khớp ground truth.

Ba receipt thật mới là development validation, không phải blind holdout. Đặc biệt Texas còn QR/reference thanh toán nên chỉ giữ cục bộ; không đưa ảnh gốc vào Git hoặc slide công khai.

## 4. Mức độ ảnh hưởng của thay đổi

Các thay đổi có ảnh hưởng lớn đến pipeline nhưng được giới hạn bằng test và evidence:

- HTTP request không còn chờ toàn bộ thời gian AI; processing chuyển sang SQL-backed worker.
- JSON contract và semantic rules đã thay đổi để phản ánh định danh, ngày, giảm giá và dòng hàng thực tế.
- Quyết định fail-safe chặt hơn có thể tăng `ESCALATE_FACT` ở ảnh mờ. Đây là thay đổi chủ đích để giảm false approval.
- Official Verify vẫn giữ đúng năm ca và expected result; không có thay đổi đáng kể bộ test đã báo BTC.
- Migration/schema database không phát sinh từ các guard cuối vì facts được lưu trong JSON hiện có.

Vì pipeline đã thay đổi đáng kể so với Sprint 1, nội dung này phù hợp để đưa vào báo cáo tiến độ. Không cần tạo ticket riêng về thay đổi Verify, nhưng nên báo rõ pipeline 202, durable queue, semantic hardening và kết quả tái kiểm.

## 5. Việc còn phải hoàn tất trước freeze

### Bắt buộc

1. Chạy ba dress rehearsal Official Verify trên cùng commit, cùng model, fallback tắt; mỗi batch phải 5/5 và dưới 90 giây.
2. Thu thập holdout 10 đến 15 hóa đơn thật có quyền sử dụng, ẩn danh và khóa ground truth trước khi chạy model. Nếu chưa đủ ảnh, công bố đúng số mẫu thực có.
3. Thực hiện ít nhất ba phiên usability ngắn khi doanh nghiệp kết nối người dùng; ghi thời gian, lỗi, điểm khó hiểu và thay đổi sản phẩm phát sinh.
4. Smoke Live URL sau hardening nếu Live URL xuất hiện trong slide hoặc phần trình bày.
5. Chốt commit, chạy full preflight, build/test, một upload smoke, mở video/ảnh dự phòng và diễn tập toàn bài.

### Chỉ làm nếu còn thời gian và có cổng kiểm thử riêng

- Proof of concept self-hosted VLM trên GPU cloud.
- A/B resize ảnh để đo latency và mất chữ nhỏ.
- SignalR cho cross-tab/multi-user realtime.

### Không chen vào baseline trước Chung kết

- Authentication/RBAC production, PDF nhiều trang, antivirus, tax/e-invoice lookup, object storage/retention hoàn chỉnh, distributed queue/circuit state và immutable audit database.

## 6. Nội dung báo cáo tiến độ có thể gửi BTC

> Sau feedback Sprint 1, em đã chuyển luồng upload sang HTTP 202 và hàng đợi xử lý nền lưu trong SQL, bổ sung lease/recovery/backoff để refresh hoặc restart không làm mất hồ sơ. Em tách rõ AI chỉ trích xuất dữ kiện, semantic validator kiểm tra và repair tối đa một lần, còn PolicyDecisionEngine C# quyết định theo FACT, POLICY và AUTHORITY. Hệ thống đã bổ sung controlled fallback/circuit breaker nhưng mặc định tắt khi benchmark, readiness kiểm database/migration/storage và bộ đánh giá 15/30 ca chạy ngoài UI nên Official Verify vẫn giữ nguyên 5 ca.
>
> Sau các lượt test hóa đơn thật, em tiếp tục hardening ba loại định danh, ngày chứng từ giấy, tổng tiền sau giảm giá và trường hợp vùng món hàng bị che. Baseline hiện build 0 warning/0 error, 102/102 automated tests pass. Live re-validation sau guard đạt Phê La `ESCALATE_FACT`, Vinamilk `AUTO_APPROVE`, Official Verify 5/5, restart giữ được status/ảnh/audit, manager YES/NO/UNDO có audit và concurrent smoke 5/5 với P95 end-to-end 39,380 giây. Đây là regression evidence; em chưa dùng các số này để tuyên bố accuracy production.
>
> Phần tiếp theo là ba lượt diễn tập có bấm giờ, holdout 10-15 hóa đơn thật đã ẩn danh và khóa ground truth, phản hồi của ba người dùng khi doanh nghiệp hỗ trợ kết nối, cùng smoke Live URL nếu tiếp tục dùng đường dẫn public. Em không thay đổi expected result của 5 ca Verify; nếu có thay đổi đáng kể em sẽ báo BTC trước.

## 7. Nguồn sự thật khi số liệu mâu thuẫn

1. Source và migration ở commit đang chạy.
2. Automated test và preflight của đúng commit đó.
3. Final JSON/CSV/transcript có request ID, provider, model, fallback và timestamp.
4. Tài liệu kỹ thuật trong `docs/` và `submission/`.
5. Tài liệu học cá nhân và slide được cập nhật sau cùng.

Không sửa expected sau khi xem output, không loại run fail khỏi mẫu và không commit API key, Authorization/cookie, HAR thô hoặc hóa đơn thật có PII.
