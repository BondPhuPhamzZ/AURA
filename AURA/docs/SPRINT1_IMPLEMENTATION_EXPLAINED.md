# AURA Sprint 1 — Tài liệu giải thích toàn bộ phần mềm

> Cập nhật: 21/09/2026  
> Phạm vi: mã nguồn sản phẩm tại `D:\aura\AURA\AURA`  
> Mục tiêu: giúp thành viên nhóm, giám khảo và người tiếp quản hiểu sản phẩm đã làm gì, chạy ra sao, đã kiểm chứng đến đâu và còn thiếu gì.

## 1. Kết luận ngắn gọn

AURA hiện là một **vertical slice chạy thật** cho Track A — The Escalation Referee trong nghiệp vụ hoàn ứng chi phí:

1. Nhân viên tải một ảnh hóa đơn JPG/PNG và nhập số tiền đề nghị.
2. Server kiểm tra file, lưu bản gốc vào vùng không public và tạo SHA-256.
3. Gemini 3.6 Flash chỉ trích xuất dữ kiện theo JSON Schema.
4. Bộ luật C# tất định quyết định `AUTO_APPROVE` hoặc một trong ba loại chuyển tiếp.
5. Ca chuyển tiếp xuất hiện ở cửa sổ quản lý với câu hỏi cụ thể.
6. Quản lý có thể duyệt, từ chối và hoàn tác.
7. Mọi bước quan trọng được ghi vào audit trail; ảnh gốc có thể mở lại.

Phần mềm **đủ để bắt đầu test local ngay**. Kết quả hiện có: build 0 warning/0 error, 22 policy test đạt, Verify Vision 5/5 trên fixture tổng hợp và các route chính đã smoke-test. Tuy vậy, Sprint 1 **chưa hoàn tất để nộp** cho tới khi có live URL, kiểm thử lại trên môi trường deploy, 5 slide, video tối đa 3 phút và build log cuối.

Đánh giá công tâm:

- Lõi demo Sprint 1: **đạt cơ bản và có thể trình diễn**.
- Tính đúng trên dữ liệu fixture: **đạt**.
- Khả năng tổng quát hóa cho hóa đơn đời thực: **chưa thể khẳng định**.
- Mức production: **chưa đạt**, chủ yếu do chưa có đăng nhập/phân quyền, rate limiting, chính sách lưu/xóa dữ liệu, hạ tầng deploy và tập đánh giá độc lập.

## 2. Bài toán và ranh giới trách nhiệm

### 2.1 Bài toán

Nghiệp vụ cần giảm số hồ sơ thường quy mà quản lý phải đọc thủ công, nhưng không được để AI tự tin duyệt một hồ sơ thiếu chứng cứ. AURA chia bài toán thành hai phần:

- **Nhìn và đọc:** Gemini đọc pixel và trả dữ kiện.
- **Ra quyết định:** C# kiểm tra dữ kiện bằng luật rõ ràng, có thể test và tái lập.

Sự tách biệt này quan trọng hơn việc dùng một prompt “thông minh” để AI tự quyết định. Cùng một JSON dữ kiện và cùng tham số đầu vào, `PolicyDecisionEngine` luôn trả cùng kết quả.

### 2.2 Những điều AURA không tuyên bố

- Không chứng minh hóa đơn hợp pháp hoặc chính hãng chỉ từ ảnh.
- Không gọi cơ quan thuế để xác thực mã số thuế/mã hóa đơn.
- Không tự quy đổi ngoại tệ.
- Không hỗ trợ PDF, nhiều trang hoặc nhiều hóa đơn trong một ảnh ở Sprint 1.
- Không phải hệ thống on-premise/zero-cloud: ảnh được gửi đến Gemini API.
- Không dùng kết quả 5/5 nội bộ để tuyên bố accuracy trên thị trường.

## 3. Kiến trúc tổng thể

```text
Browser / Razor UI
  -> ASP.NET Core MVC Controller
  -> kiểm CSRF + amount + extension + MIME + magic bytes + size
  -> private receipt storage (App_Data/receipts)
  -> Gemini 3.6 Flash / Structured Output
  -> ReceiptExtractionDto
  -> PolicyDecisionEngine (FACT > POLICY > AUTHORITY)
  -> SQL Server: request + extracted JSON + decision
  -> AuditLog
  -> Reviewer queue / receipt lookup / approve-reject-undo
```

Các lớp được ghép bằng Dependency Injection:

- `IVisionExtractor` → `GeminiVisionExtractorService`
- `IReimbursementRepository` → `ReimbursementRepository`
- `IAuditLogger` → `AuditLogger`
- `AppDbContext` → EF Core SQL Server

## 4. Bản đồ thư mục và trách nhiệm

| Vị trí | Trách nhiệm |
|---|---|
| `Controllers/` | Route HTTP, điều phối upload, Verify và quyết định quản lý |
| `Services/` | Gọi Gemini, luật quyết định, repository và audit persistence |
| `Interfaces/` | Hợp đồng DI giúp tách controller khỏi implementation |
| `Models/` | Entity DB, DTO trích xuất, manifest Verify và error model |
| `Options/` | Cấu hình có kiểu cho Gemini và kho chứng từ |
| `Data/` | `AppDbContext`, index và precision |
| `Migrations/` | Lịch sử schema SQL Server |
| `ViewComponents/` | Tải queue quản lý và audit độc lập |
| `ViewModels/` | Dữ liệu ghép cho audit; có một view model cũ chưa dùng |
| `Views/` | Razor UI một trang, queue, audit, privacy và error |
| `wwwroot/` | CSS/JS/thư viện giao diện và fixture tổng hợp công khai |
| `tests/AURA.Tests/` | xUnit cho policy engine |
| `tools/` | Script tái tạo 5 ảnh Verify tổng hợp |
| `docs/` | Runbook, test matrix, measurement plan và trạng thái nộp |
| `App_Data/receipts/` | Ảnh upload runtime; bị Git ignore và không được static serve |
| `BUSINESS_RULES.md` | Hợp đồng extraction mà server nạp vào system instruction |

Các thư mục `bin/`, `obj/`, `publish/` và `wwwroot/lib/` là output build hoặc thư viện bên thứ ba, không phải logic do nhóm viết. Không đưa chúng vào đánh giá chất lượng nghiệp vụ.

## 5. Startup và cấu hình ASP.NET Core

### 5.1 `AURA.csproj`

- Target `.NET 8`.
- Bật nullable reference types và implicit usings.
- Dùng EF Core SQL Server 8.0 và EF tooling.
- Có `UserSecretsId`, cho phép giữ API key ngoài source code.
- Loại `tests/**` khỏi project web để test project không bị compile hai lần.
- Docker hiện đặt target Windows; cần kiểm chứng lại với nhà cung cấp deploy.

### 5.2 `Program.cs`

- Log ra Console và Debug, phù hợp local/container hơn Windows Event Log.
- Đăng ký MVC, options, EF Core và các service scoped/typed client.
- `ReceiptStorageOptions` được DataAnnotations validation và `ValidateOnStart`.
- Giới hạn multipart được đồng bộ từ `ReceiptStorage:MaxFileSizeMb`.
- `HttpClient` lấy Base URL, timeout và model từ config.
- Production bật exception handler, HSTS và HTTPS redirection.
- Static file, routing và conventional route đều được bật.
- Route mặc định: `{controller=Home}/{action=Index}/{id?}`.

Điểm cần hiểu: `GeminiOptions` không `ValidateOnStart` để trang demo vẫn có thể mở khi API key chưa được inject. Upload/Verify sẽ fail-safe thành lỗi hệ thống, không tự duyệt.

### 5.3 Cấu hình

`appsettings.json` chứa:

- LocalDB connection string phục vụ Windows local.
- Model `gemini-3.6-flash`.
- Gemini API base URL.
- `PolicyPath = BUSINESS_RULES.md`.
- Timeout 60 giây.
- Kho ảnh `App_Data/receipts`, tối đa 5 MB.

API key không nằm trong file config. Local dùng user secrets; deploy dùng `Gemini__ApiKey`. Connection string deploy phải override `ConnectionStrings__DefaultConnection`.

## 6. Toàn bộ route hiện có

| Method | Route theo conventional routing | Chức năng | Bảo vệ |
|---|---|---|---|
| GET | `/`, `/Home`, `/Home/Index` | Dashboard ba tab | Public demo |
| GET | `/Home/Privacy` | Công bố dữ liệu/quyền riêng tư | Public |
| GET | `/Home/Error` | Trang lỗi production | No-store |
| GET | `/Applicant` | Chuyển về trang chủ | Redirect |
| POST | `/Applicant/UploadReceipt` | Upload và xử lý một ảnh mới | Antiforgery + validation |
| GET | `/Applicant/Receipt/{id}` | Stream ảnh đã lưu | No-store, path containment |
| GET | `/Verify` | Chuyển về trang chủ | Redirect |
| POST | `/Verify/RunHarness` | Chạy đúng 5 fixture | Antiforgery |
| POST | `/Reviewer/EscalateAction` | Approve/reject/undo | Antiforgery + state validation |

Vì Verify Harness là một phần của dashboard nên `/Verify` không phải trang riêng. Lỗi cũ “Vui lòng quay về trang chủ” xuất phát từ hai hàm JavaScript trùng tên; hàm sai trong layout ghi đè hàm đúng của trang chủ. Bản hiện tại đã xóa hàm trùng và route POST đúng được lấy từ Razor form.

## 7. Luồng upload một hóa đơn mới

`ApplicantController.UploadReceipt` thực hiện theo thứ tự:

1. Yêu cầu file không rỗng và `claimedAmount > 0`.
2. Giới hạn dung lượng theo config.
3. Chỉ cho extension `.jpg`, `.jpeg`, `.png` và MIME tương ứng.
4. Đọc byte với cancellation token.
5. Kiểm magic bytes PNG/JPEG để chống đổi đuôi file đơn giản.
6. Sinh ID ngẫu nhiên và tên lưu ngẫu nhiên.
7. Bảo đảm thư mục cấu hình nằm dưới content root.
8. Lưu file ngoài `wwwroot`.
9. Tính SHA-256 và hỏi DB xem ảnh đã xuất hiện chưa.
10. Gọi Gemini để trích xuất facts.
11. Serialize facts để giữ bằng chứng máy đã đọc gì.
12. Gọi policy engine với facts, claimed amount và duplicate flag.
13. Nếu AI/API lỗi, chuyển `ESCALATE_SYSTEM_ERROR`; không giả kết quả nghiệp vụ.
14. Ghi latency thật, request và audit log.
15. Trả JSON cho bảng kết quả.

Lưu ý kỹ thuật còn tồn tại: file được ghi trước DB. Nếu DB save thất bại, file có thể thành orphan. Sprint 2 nên có cleanup/transactional workflow hoặc object storage có trạng thái staging.

## 8. Gemini Vision và `BUSINESS_RULES.md`

### 8.1 Gemini làm gì

`GeminiVisionExtractorService`:

- Từ chối chạy nếu thiếu API key, ảnh hoặc policy file.
- Chỉ cho phép policy path nằm trong application root.
- Đọc toàn bộ `BUSINESS_RULES.md` mỗi request làm `systemInstruction`.
- Gửi ảnh inline Base64 với đúng MIME.
- Yêu cầu `application/json` và khai báo response JSON Schema.
- Dùng temperature 0, thinking LOW, tối đa 4096 output token.
- Retry tối đa ba lần với 429/500/502/503/504 và backoff/Retry-After.
- Kiểm HTTP status, prompt block, candidate, finish reason và text part.
- Deserialize có kiểu; chuẩn hóa array null thành rỗng và clamp confidence 0..1.

Google chính thức xác nhận Gemini 3.6 Flash là model stable, nhận input hình ảnh và hỗ trợ Structured Outputs. Structured Outputs bảo đảm hình dạng JSON tốt hơn nhưng **không bảo đảm giá trị đúng về ngữ nghĩa**, vì vậy validation C# vẫn bắt buộc.

### 8.2 AI có đọc được `BUSINESS_RULES.md` không?

Có. File không cần nằm trong `wwwroot`. Server dùng `IWebHostEnvironment.ContentRootPath` ghép với `Gemini:PolicyPath`, đọc text và gửi trực tiếp trong request. Đặt file ở root ứng dụng còn tốt hơn đặt trong `wwwroot`, vì URL `/BUSINESS_RULES.md` hiện trả 404.

### 8.3 Chất lượng policy hiện tại

File đã rõ ở các điểm:

- Ranh giới AI chỉ extraction, không approve.
- Cấm làm theo prompt injection trong ảnh.
- Không được đoán; thiếu/mờ phải `null` + `missingFields`.
- Phân biệt seller/buyer, tax ID/invoice number/booking ID.
- Chuẩn hóa date/time/currency/amount.
- Yêu cầu mọi line item và confidence.
- Không cáo buộc fraud; chỉ báo visible anomaly.
- Nêu precedence `FACT > POLICY > AUTHORITY > AUTO_APPROVE`.

Nhưng chưa “bao phủ tuyệt đối”. Những khoảng trống cần version 1.1:

- Chưa mô tả hóa đơn điều chỉnh/thay thế/hủy, credit note, refund hoặc total âm.
- Chưa định nghĩa rounding/tolerance khi VAT tạo số lẻ.
- Chưa có taxonomy chi phí được phép; hiện chỉ có blacklist hạng mục cấm.
- Chưa có rule cho tip/service charge/discount/voucher/shared bill.
- Chưa giải quyết nhiều hóa đơn trong một ảnh, hai mặt hoặc nhiều trang.
- Chưa chuẩn hóa timezone và ngày qua đêm cho ride-hailing.
- Chưa quy định duplicate theo nội dung hóa đơn khi ảnh bị resize/crop; SHA-256 chỉ bắt byte-identical.
- Chưa có external verification cho tax ID/e-invoice và chưa thể kết luận pháp lý.
- Chưa có policy lưu dữ liệu, consent, retention/deletion trong file nghiệp vụ.

Với Sprint 1, contract hiện tại đủ rõ cho scope một ảnh JPG/PNG. Với production, phải mở rộng rule matrix và có policy owner ký duyệt.

## 9. JSON dữ kiện

`ReceiptExtractionDto` nhận:

- Loại tài liệu, merchant, seller tax ID, booking ID, invoice number.
- Ngày/giờ hóa đơn và currency.
- Subtotal, tax, total.
- Danh sách line item gồm description, quantity, unit price, amount.
- Missing fields, warnings, suspicious signals.
- Confidence toàn bản trích xuất.

Schema bắt buộc mọi property xuất hiện, nhưng nhiều field được phép `null`. Đây là cách phân biệt “AI không đọc được” với “AI quên trả field”.

## 10. Bộ luật quyết định tất định

### 10.1 Kết quả

- `AUTO_APPROVE`: đủ dữ kiện, đúng policy và trong thẩm quyền.
- `ESCALATE_FACT`: thiếu/sai/không đáng tin cậy ở bằng chứng.
- `ESCALATE_POLICY`: có hạng mục ngoài chính sách.
- `ESCALATE_AUTHORITY`: hợp lệ nhưng trên 1.000.000 VND.
- `ESCALATE_SYSTEM_ERROR`: hạ tầng/AI lỗi; không phải kết luận nghiệp vụ.
- `APPROVED_BY_MANAGER`, `REJECTED_BY_MANAGER`: trạng thái sau thao tác người quản lý.

### 10.2 Precedence

Khi có nhiều vấn đề: `FACT` thắng `POLICY`, `POLICY` thắng `AUTHORITY`. Ví dụ ảnh mờ + bia + 2 triệu phải đi `ESCALATE_FACT`, vì trước hết chưa có bằng chứng đáng tin để khẳng định hạng mục hoặc số tiền.

### 10.3 Điều kiện FACT

- Facts null hoặc confidence dưới 0,70.
- Byte-identical duplicate.
- Total thiếu/không dương; claimed amount không hợp lệ; amount lệch sau round VND.
- Thiếu merchant hoặc invoice number.
- Hóa đơn giấy thiếu seller tax ID.
- Tài liệu số thiếu cả booking ID và tax ID.
- Currency không phải VND.
- Ngày không đúng `YYYY-MM-DD`, tương lai, quá 90 ngày hoặc cuối tuần.
- Giờ có in nhưng sai `HH:mm` hoặc ngoài 06:00–22:00.
- Warning/signal critical: blur, crop, unreadable, tamper, missing page, prompt injection.

### 10.4 Điều kiện POLICY và AUTHORITY

Blacklist hiện tìm từ khóa alcohol/beer/bia/rượu/wine/whisky/vodka, tobacco/thuốc lá, karaoke/cinema/massage/entertainment và personal item trong mô tả line item. Nếu không có FACT và có từ cấm → POLICY. Nếu không có FACT/POLICY và total > 1.000.000 → AUTHORITY. Mốc đúng 1.000.000 vẫn có thể auto-approve.

Rủi ro: keyword matching chưa phải classifier hoàn chỉnh, có thể false positive/false negative hoặc bị cách viết lạ né. Cần bộ synonym có version và test tiếng Việt/Anh phong phú hơn.

## 11. Lưu trữ, DB và audit

### 11.1 Entity `ReimbursementRequest`

Lưu ID, claimed amount, receipt route, tên file gốc/tên lưu, MIME, size, SHA-256, extracted JSON, status, reasoning, manager question, latency và UTC created time.

### 11.2 Entity `AuditLog`

Lưu auto-increment ID, request ID, action, details và UTC timestamp. Repository dùng async EF Core; các truy vấn danh sách dùng `AsNoTracking`.

### 11.3 Migration

- `InitialCreate`: tạo requests và audit logs.
- `RemoveUserFields`: bỏ dữ liệu nhân viên khỏi prototype zero-login.
- `PersistReceiptEvidence`: thêm metadata, SHA-256 và extracted facts; index SHA-256.
- Designer files và snapshot là metadata do EF sinh để migration tiếp theo đúng schema.

### 11.4 Tra cứu ảnh

`GET /Applicant/Receipt/{id}` lấy record DB, chỉ dùng basename của stored filename, kiểm full path vẫn nằm trong storage root rồi mới stream. Response `NoStore` và có range processing.

Điểm yếu production: chưa có authentication/authorization. ID ngẫu nhiên khó đoán không thay thế quyền truy cập. Trước khi dùng dữ liệu thật phải có login, owner/role check, encryption, retention/delete policy và access audit.

## 12. Verify Harness

Nút Verify gọi `POST /Verify/RunHarness` kèm antiforgery token. Server yêu cầu manifest có **đúng 5 ca**, kiểm path từng image, chạy cùng `IVisionExtractor` và policy engine như upload thật, lưu request/audit rồi trả expected/actual/pass/reason/question/latency/timestamp.

Năm fixture:

| Case | Nội dung | Claimed | Kỳ vọng |
|---|---|---:|---|
| TC-01 | Grab e-receipt đủ ID | 150.000 | AUTO_APPROVE |
| TC-02 | Phở + nước, đủ MST | 80.000 | AUTO_APPROVE |
| TC-03 | Văn phòng phẩm, đủ MST | 350.000 | AUTO_APPROVE |
| TC-04 | Phiếu ghi thiếu MST | 200.000 | ESCALATE_FACT |
| TC-05 | Nhà hàng có Tiger Beer | 850.000 | ESCALATE_POLICY |

Comparator chỉ PASS khi status đúng; expected `ESCALATE` tổng quát mới khớp mọi `ESCALATE_*`. `ESCALATE_SYSTEM_ERROR` không được hợp thức hóa thành PASS.

Benchmark thật ngày 20/09/2026 với Gemini 3.6 Flash đạt 5/5, tổng khoảng 31,5 giây. Đây là sanity check nội bộ, không phải benchmark độc lập.

## 13. Giao diện

Dashboard có ba tab:

- **Cửa Sổ Nhân Viên:** Verify một nút, upload thủ công, bảng kết quả.
- **Cửa Sổ Quản Lý:** chỉ liệt kê status bắt đầu `ESCALATE_`, cho xem chứng từ và approve/reject.
- **Lịch Sử:** hiển thị action, thời gian, chi tiết, liên kết chứng từ và nút undo khi phù hợp.

Razor tạo URL và antiforgery token nên không hard-code route POST. JavaScript dùng `textContent`/`escapeHtml` khi render dữ liệu AI và lỗi để giảm DOM XSS. Loading skeleton cho người dùng biết request đang chạy.

Hai ViewComponent bắt lỗi DB và hiển thị thông báo thay vì làm hỏng toàn bộ trang chủ. Đây là fail-soft cho demo, không thay thế monitoring production.

## 14. Kiểm thử đã có

### 14.1 Automated policy tests

22 xUnit cases phủ routine, null facts, confidence, total, amount mismatch, merchant/identifier, tax ID, currency, date, future/stale/weekend, late time, blur, duplicate, bốn nhóm item cấm, authority và precedence.

### 14.2 Smoke tests đã thực hiện

- `/`, `/Home/Index`, `/Home/Privacy` trả 200.
- `/Applicant` và `/Verify` redirect về trang chủ.
- Receipt ID không tồn tại trả 404.
- `/BUSINESS_RULES.md` trả 404.
- POST Verify thiếu CSRF trả 400.
- Upload hợp lệ lưu file và route ảnh trả đúng MIME.
- Manager approve → audit → undo đã được thử local.
- Components hiển thị lỗi có kiểm soát khi DB unavailable.

### 14.3 Điều chưa có

- Chưa có automated integration/end-to-end browser test.
- Chưa mock `IVisionExtractor` để test đầy đủ controller mà không tốn quota.
- Chưa test concurrency/idempotency khi double-click.
- Chưa test persistence sau restart trên hạ tầng deploy.
- Chưa có tập ảnh độc lập đủ rộng để đo missed/over-escalation.

## 15. Có thể bắt đầu test chưa?

**Có, bắt đầu ngay được**, theo bốn tầng:

### Tầng A — không dùng API, chạy mỗi commit

```powershell
dotnet build --no-restore
dotnet test tests/AURA.Tests/AURA.Tests.csproj --no-restore
```

Mục tiêu: build sạch và 22/22 test policy.

### Tầng B — fixture chuẩn hóa

1. Cấu hình API key bằng user secrets.
2. Chạy migration và app.
3. Bấm Verify đúng một lần.
4. Kỳ vọng 3 auto + FACT + POLICY, đủ 5 PASS, dưới 90 giây.
5. Nếu 429/5xx sau retry thì ghi lỗi thật, không chạy liên tục để “chọn” kết quả đẹp.

### Tầng C — ảnh mới do nhóm tự tạo

Tạo tối thiểu 30 ảnh, không dùng lại ảnh để thiết kế prompt. Mỗi scenario có ground truth và expected status được hai người duyệt trước. Biến đổi ảnh: nghiêng, crop, blur, glare, low light, nền phức tạp, độ phân giải thấp và nhiều font.

### Tầng D — holdout độc lập

Giữ một tập chưa từng nhìn khi viết rule. Báo missed-escalation, over-escalation, exact match amount/date/currency và p50/p95 latency. Đây mới là căn cứ nói về độ chính xác.

## 16. Nên lấy ảnh hóa đơn test ở đâu?

Thứ tự an toàn khuyến nghị:

1. **Fixture hiện có:** `wwwroot/test_data/images`, tái tạo bằng `tools/generate_verify_receipts.py`. Dùng để kiểm tra regression/demo, không dùng để tuyên bố tổng quát hóa.
2. **Tự tạo dữ liệu tổng hợp:** Canva, Figma, Word/PowerPoint hoặc script. In rõ `DỮ LIỆU GIẢ LẬP — KHÔNG PHẢI HÓA ĐƠN THẬT`; dùng MST/số hóa đơn hư cấu; không sao chép thương hiệu khi không cần.
3. **Ảnh do nhóm tự chụp:** chỉ dùng hóa đơn thuộc quyền sở hữu, che tên, số điện thoại, địa chỉ, mã thẻ/QR và thông tin định danh; có đồng thuận; không commit lên public GitHub.
4. **CORD:** 1.000 receipt có annotation, license CC BY 4.0; tốt để thử layout/OCR nhưng chủ yếu Indonesia, không đại diện policy Việt Nam.
5. **SROIE/ICDAR 2019:** receipt scan có OCR và key-information labels; kiểm tra điều khoản tải/dùng của challenge trước khi đưa vào artifact.
6. **WildReceipt/MMOCR:** phù hợp robustness/KIE; cần đọc license và hướng dẫn dataset chính thức trước khi phân phối lại.

Không lấy ảnh tùy tiện từ Google Images, Facebook, email công ty hoặc hóa đơn khách hàng. “Ảnh công khai trên Internet” không đồng nghĩa được phép xử lý, tái phân phối hoặc gửi sang AI API.

Dataset nước ngoài nên dùng để test **extraction/robustness**, không chấm trực tiếp chính sách VND. Currency nước ngoài bị FACT là đúng với policy hiện tại.

## 17. Bộ ảnh 30 ca nên chuẩn bị

- 6 routine hợp lệ: taxi, ăn công tác, văn phòng phẩm; cả giấy và digital.
- 6 FACT thiếu dữ kiện: mờ total, crop MST, thiếu số hóa đơn, unknown currency, low confidence, thiếu trang.
- 4 FACT thời gian: quá 90 ngày, tương lai, cuối tuần, 23:30.
- 3 amount: claimed lệch, đúng 1.000.000, 1.000.001.
- 4 POLICY: bia/rượu, thuốc lá, karaoke/giải trí, đồ cá nhân.
- 2 AUTHORITY hợp lệ trên hạn mức.
- 2 duplicate: cùng byte và cùng hóa đơn nhưng ảnh resize để lộ giới hạn SHA-256.
- 1 prompt injection in trong ảnh.
- 1 non-receipt.
- 1 nhiều hóa đơn/nhiều trang để xác nhận hệ thống từ chối hoặc chuyển thủ công.

Mỗi case lưu: ID, nguồn/license/consent, ground truth fields, claimed amount, expected class, lý do, model ID, timestamp, actual, latency và reviewer label.

## 18. Đánh giá mức đáp ứng Sprint 1

| Hạng mục | Trạng thái | Nhận xét |
|---|---|---|
| Chủ đề và workflow Track A | Đạt | Hoàn ứng chi phí, input→decision→HITL rõ |
| Policy rõ | Đạt cơ bản | Tốt cho scope một ảnh; còn gap production |
| ≥15 tình huống | Đạt | 22 automated, matrix >30 |
| FACT/POLICY/AUTHORITY | Đạt | Có precedence tất định |
| Câu hỏi chuyển tiếp cụ thể | Đạt | Tiếng Việt, dữ kiện cụ thể, CÓ/KHÔNG |
| 3 ca tự xử lý + 2 chuyển tiếp một nút | Đạt local | Live benchmark 5/5 |
| Input mới | Đạt local | Cùng extraction/policy path |
| Audit trail, timestamp, latency | Đạt local | Có DB audit và UI |
| Stop/override/undo | Đạt local | Approve/reject/undo |
| Lưu và tra cứu ảnh | Đạt local | Private folder + route |
| Chạy dưới 90 giây | Đạt benchmark | ~31,5 giây, quota vẫn biến động |
| Live URL | **Không đạt** | Blocker trước nộp |
| Public repo + lịch sử commit | Đạt sau push | Không chứa API key |
| Video ≤3 phút | Chưa xác minh | Phải quay sau deploy |
| Đúng 5 slide | Chưa xác minh | Phải hoàn tất artifact |
| Build log một trang | Đạt bản nháp | Cần khóa số liệu cuối |

## 19. Blocker và backlog

### P0 — trước khi nộp Sprint 1

1. Chọn host hỗ trợ ASP.NET Core 8, SQL Server/Azure SQL và persistent volume/object storage.
2. Inject secret, chạy migration, smoke-test từ máy/ẩn danh khác.
3. Test upload → restart/redeploy → mở lại receipt.
4. Chạy Verify live một lần, chụp evidence 5/5 dưới 90 giây.
5. Điền cùng một live URL vào README, slide, video description và form.
6. Hoàn thành đúng 5 slide, video tối đa 3 phút và build log cuối.

### P1 — tăng độ tin cậy

- Authentication + role Employee/Manager/Auditor và authorization theo receipt.
- Rate limiting cho upload/Verify; chống double-submit và quota exhaustion.
- Mock AI integration tests và browser E2E tests.
- Transaction/cleanup cho file orphan; backup và retention/delete.
- Antivirus/content scanning, image decode validation và metadata stripping.
- Tax/e-invoice lookup, perceptual duplicate detection và immutable audit.
- Rule taxonomy/versioning và benchmark độc lập ≥100 ảnh.

### P2 — sau Sprint 1

- PDF/nhiều trang, multi-receipt, batch upload.
- Ngoại tệ/tỷ giá theo nguồn và ngày xác định.
- Dashboard metric, p50/p95, missed/over-escalation.
- Object storage signed URL, queue/background worker và observability.

## 20. Rủi ro deploy cụ thể

- LocalDB không chạy như production cloud DB; phải override connection string.
- Dockerfile dùng Windows NanoServer 1809 và build context `AURA/AURA.csproj`; nhiều host rẻ chỉ hỗ trợ Linux container. Chưa được coi là deploy-ready cho tới khi build trên target host thành công.
- Filesystem container mặc định là tạm thời; cần volume hoặc object storage.
- Free tier/rate limit không có SLA; Verify gọi năm request tuần tự.
- Chưa có account/role nên không được dùng hóa đơn thật trên public URL.

## 21. Các tệp đáng chú ý, theo từng file

- `Program.cs`: composition root và middleware pipeline.
- `Controllers/HomeController.cs`: dashboard/privacy/error.
- `Controllers/ApplicantController.cs`: upload, validation, persistence và receipt stream.
- `Controllers/VerifyController.cs`: one-click five-case harness.
- `Controllers/ReviewerController.cs`: approve/reject/undo với state guard.
- `Services/GeminiVisionExtractorService.cs`: REST client, schema, retry, parsing.
- `Services/PolicyDecisionEngine.cs`: luật nghiệp vụ thuần, không phụ thuộc DB/API.
- `Services/ReimbursementRepository.cs`: CRUD requests và duplicate hash query.
- `Services/AuditLogger.cs`: append/read audit entries.
- `Data/AppDbContext.cs`: entity sets, status/hash index, money precision.
- `Interfaces/*.cs`: các abstraction DI.
- `Models/ReceiptExtractionDto.cs`: contract giữa Vision và C#.
- `Models/ReimbursementRequest.cs`: hồ sơ + evidence metadata.
- `Models/AuditLog.cs`: nhật ký.
- `Models/ExpectedResult.cs`: manifest DTO.
- `Options/*.cs`: typed configuration validation.
- `ViewComponents/*.cs`: queue/audit có fail-soft khi DB lỗi.
- `ViewModels/AuditEntryViewModel.cs`: ghép log với request.
- `ViewModels/ReimbursementViewModel.cs`: di sản chưa dùng; nên xóa sau khi xác nhận không còn consumer.
- `Views/Home/Index.cshtml`: UI và AJAX upload/Verify.
- `Views/Shared/_Layout.cshtml`: shell và tab navigation.
- `Views/Shared/Components/**`: bảng manager/audit.
- `wwwroot/css/site.css`: visual system responsive tối màu.
- `wwwroot/js/site.js`: template script nhỏ; logic nghiệp vụ UI hiện chủ yếu ở Razor view.
- `wwwroot/test_data/**`: fixture synthetic + expected manifest.
- `tools/generate_verify_receipts.py`: tái tạo fixture.
- `tests/AURA.Tests/**`: test project và policy tests.
- `BUSINESS_RULES.md`: runtime AI extraction contract.
- `README.md`, `BUILD_LOG.md`, `docs/**`: hồ sơ giải thích, vận hành, kiểm thử và nộp bài.
- `Dockerfile`: scaffold Windows container, chưa chứng minh tương thích target deploy.
- `Properties/launchSettings.json`: URL/profile local, không có giá trị secret.

## 22. Những điểm nhất quán và chưa nhất quán trong ASP.NET Core

Điểm tốt:

- MVC/DI/Options/EF Core/HttpClientFactory/async được dùng đúng hướng.
- POST quan trọng đều có antiforgery.
- Không hard-code API key.
- Policy engine là pure logic, dễ unit test.
- Upload kiểm nhiều lớp và storage nằm ngoài static root.
- ViewComponent tách queue/audit khỏi controller chính.

Điểm nên chuẩn hóa:

- Namespace/file style đang trộn file-scoped và block-scoped; nhiều `using` cũ không cần.
- JavaScript/CSS lớn đang inline trong `Index.cshtml`; nên tách module sau Sprint 1.
- `ReimbursementViewModel` không dùng và còn trường EmployeeName đã bỏ khỏi entity.
- Repository chưa nhận cancellation token.
- Entity status là string tự do; nên dùng constants/value object/enum mapping và max length.
- `UseAuthorization` có nhưng chưa `AddAuthentication`/policy — đúng với zero-login demo, không đủ production.
- Không có global rate limit, CSP/security headers đầy đủ hoặc health check.

## 23. Checklist thao tác trước demo

1. Không mở trang user-secrets hoặc log key khi share màn hình.
2. Mở live URL trong cửa sổ ẩn danh.
3. Chạy Verify một lần; nếu API lỗi, giải thích fail-safe thay vì refresh liên tục.
4. Upload một ảnh synthetic mới với claimed amount chuẩn.
5. Cho thấy FACT/POLICY/AUTHORITY và câu hỏi quản lý.
6. Approve/reject một case, mở audit, mở receipt rồi undo.
7. Nói rõ dữ liệu nào thật/giả, model nào đang chạy và giới hạn một ảnh JPG/PNG.
8. Giữ demo kỹ thuật khoảng 90 giây để video tổng không vượt 3 phút.

## 24. Nguồn tham khảo kiểm thử và model

- Gemini models: `https://ai.google.dev/gemini-api/docs/models`
- Gemini 3.6 Flash: `https://ai.google.dev/gemini-api/docs/models/gemini-3.6-flash`
- Gemini Structured Outputs: `https://ai.google.dev/gemini-api/docs/structured-output`
- Gemini image understanding: `https://ai.google.dev/gemini-api/docs/image-understanding`
- CORD official repository: `https://github.com/clovaai/cord`
- SROIE official challenge: `https://rrc.cvc.uab.es/?ch=13&com=tasks`
- MMOCR/WildReceipt: `https://github.com/open-mmlab/mmocr`

## 25. Kết luận cuối

AURA đã vượt mức “ý tưởng trên slide”: có request thật tới model, Structured Output, luật quyết định độc lập, human-in-the-loop, lưu evidence, audit và test. Thiết kế `Vision extraction + deterministic policy` phù hợp với Sprint 1 hơn tự host Qwen gấp trước deadline, vì giảm rủi ro setup/deploy và giúp giải thích quyết định.

Điều đúng đắn lúc này không phải mở rộng thêm nhiều feature, mà là:

1. khóa scope hiện tại;
2. deploy an toàn;
3. kiểm thử bằng dữ liệu synthetic/được phép;
4. ghi lại evidence trung thực;
5. hoàn thiện đúng bộ hồ sơ nộp.

Sau khi live URL chạy ổn, P1 quan trọng nhất là phân quyền, rate limiting, persistence production và tập đánh giá độc lập. Khi đó mới có đủ bằng chứng để nói sản phẩm không chỉ demo tốt mà còn đáng tin cậy trên dữ liệu mới.
