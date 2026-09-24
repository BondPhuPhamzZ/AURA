# Architecture and Integration Report

## AURA Automated Underwriting and Reimbursement AI

Cập nhật ngày 24/09/2026. Tài liệu này mô tả cấu trúc mã nguồn, hợp đồng tích hợp và đường đi dữ liệu của bản Sprint 1. Cách đánh giá chuẩn là clone và chạy localhost theo README; bản SmarterASP.NET chỉ là môi trường demo tùy chọn.

## 1. Sơ đồ thành phần

```text
AURA/
├── Program.cs                         # DI, middleware, SQL Server, /healthz
├── Controllers/
│   ├── ApplicantController.cs         # upload, xem ảnh, chuyển quản lý
│   ├── VerifyController.cs            # chạy 5 ca Verify qua cùng pipeline thật
│   ├── ReviewerController.cs          # quản lý đồng ý, từ chối, hoàn tác
│   └── HomeController.cs              # trang chính và các component đọc dữ liệu
├── Services/
│   ├── OpenRouterVisionExtractorService.cs  # Qwen vision và JSON Schema
│   ├── PolicyDecisionEngine.cs               # quyết định nghiệp vụ tất định
│   ├── ReimbursementRepository.cs            # transaction hồ sơ và audit
│   ├── AuditLogger.cs                        # ghi sự kiện
│   ├── AuditTrailProjector.cs                # gom sự kiện thành timeline UI
│   └── WorkflowOperationGate.cs               # chặn mutation chồng trong một instance
├── Data/                              # EF Core DbContext và migrations
├── Models/                            # request, facts AI, audit, DTO
├── Views/                             # Razor UI nhân viên, quản lý, lịch sử
├── wwwroot/test_data/                 # 5 fixture được Verify chạy trực tiếp
├── test_kit/                          # ngân hàng 30 ca và gói BGK 15 ca
├── tests/AURA.Tests/                  # 56 kiểm thử tự động offline
├── docs/                              # runbook, deploy, test, checklist
├── submission/                        # 5 slide, Build Log Word, workflow
└── tools/                             # tái tạo fixture và artifact nộp bài
```

## 2. Kiến trúc triển khai

```text
Trình duyệt
    │ HTTPS
    ▼
SmarterASP.NET / IIS / ASP.NET Core 8
    ├── Razor UI và controller
    ├── PolicyDecisionEngine
    ├── SQL Server: hồ sơ và audit event
    └── App_Data/receipts: ảnh riêng tư, tên ngẫu nhiên và SHA-256
             │
             │ HTTPS, API key đặt trong Pool Manager
             ▼
        OpenRouter
             │
             ▼
      Qwen3-VL-8B-Instruct
```

Qwen chỉ trích xuất dữ kiện nhìn thấy trong ảnh. Quyết định `AUTO_APPROVE` hay `ESCALATE_*` nằm trong C# policy engine. Sprint 1 chưa có đăng nhập theo vai trò, nên mọi bản demo công khai chỉ dùng dữ liệu tổng hợp và không phù hợp để nhận hóa đơn cá nhân thật.

## 3. Route và hợp đồng chính

| Route | Phương thức | Vai trò |
|---|---|---|
| `/` | GET | Dashboard nhân viên, quản lý và lịch sử |
| `/healthz` | GET | Kiểm tra policy, cấu hình AI và model; không lộ API key |
| `/Applicant/UploadReceipt` | POST | Kiểm file, lưu ảnh, gọi Qwen, chạy policy và ghi hồ sơ |
| `/Applicant/Receipt/{id}` | GET | Đọc ảnh từ storage riêng tư theo mã hồ sơ |
| `/Applicant/ForwardToManager` | POST | Chuyển một hồ sơ cần người quyết định |
| `/Applicant/ForwardAllToManager` | POST | Chuyển toàn bộ hồ sơ đang chờ |
| `/Verify/RunHarness` | POST | Chạy tuần tự 5 fixture bằng pipeline production |
| `/Reviewer/EscalateAction` | POST | Quản lý chọn `YES`, `NO` hoặc `UNDO` |
| `/Home/EmployeeEscalations` | GET | Fragment hàng đợi nhân viên |
| `/Home/ReviewerQueue` | GET | Fragment hàng đợi quản lý |
| `/Home/AuditTrail` | GET | Fragment timeline audit |

Các POST thay đổi trạng thái đều kiểm antiforgery token. Upload chỉ nhận JPG/PNG tối đa 5 MB và server kiểm cả MIME, magic bytes lẫn kích thước.

## 4. Luồng xử lý một hóa đơn

1. Server xác thực file và số tiền đề nghị.
2. `ReceiptStorage` lưu ảnh ngoài `wwwroot`, đặt tên ngẫu nhiên và tính SHA-256.
3. `OpenRouterVisionExtractorService` gửi ảnh cùng policy tới Qwen và yêu cầu JSON theo schema.
4. UI hiển thị dữ kiện AI đọc được; dữ kiện không chắc chắn giữ nguyên là không đọc được, không tự suy đoán.
5. `PolicyDecisionEngine` áp quy tắc theo thứ tự `FACT`, `POLICY`, `AUTHORITY`.
6. Hồ sơ và audit AI được ghi nhất quán. `AUTO_APPROVE` đi vào lịch sử; `ESCALATE_*` ở bảng thẩm định.
7. Nhân viên xác nhận chuyển tiếp. Quản lý đồng ý hoặc từ chối theo câu hỏi đã sinh; quyết định có thể hoàn tác.
8. UI gom các audit event của cùng hồ sơ thành một timeline, tránh hiển thị ba dòng trùng nghĩa.

Các thao tác ghi workflow dùng `WorkflowOperationGate` để từ chối mutation chồng trong cùng một tiến trình và dùng `RowVersion` của SQL Server làm chốt optimistic concurrency khi có nhiều tiến trình. Sau chuyển tiếp/quyết định/hoàn tác, trình duyệt điều hướng toàn trang về tab đích để dựng lại tất cả bảng từ trạng thái database đã commit; không còn polling nền 10 giây.

Nếu AI lỗi xác thực, rate limit, schema hoặc provider, AURA trả mã lỗi rõ ràng và chuyển `ESCALATE_SYSTEM_ERROR`. Hệ thống không dùng mock ngầm và không tạo PASS giả.

## 5. Ma trận quyết định

| Nhánh | Điều kiện chính | Kết quả |
|---|---|---|
| Dữ kiện đủ và hợp lệ | Identifier, ngày, tổng tiền, trạng thái và dòng hàng phù hợp | `AUTO_APPROVE` |
| Dữ kiện thiếu hoặc đáng ngờ | Thiếu identifier, ảnh mờ/crop, amount mismatch, draft/refund | `ESCALATE_FACT` |
| Ngoài chính sách | Hạng mục bị loại trừ theo `BUSINESS_RULES.md` | `ESCALATE_POLICY` |
| Vượt thẩm quyền | Số tiền vượt hạn mức được cấu hình | `ESCALATE_AUTHORITY` |
| AI hoặc hạ tầng lỗi | Auth, 429, 5xx sau retry giới hạn, JSON không hợp lệ | `ESCALATE_SYSTEM_ERROR` |

Khi một hồ sơ có nhiều vấn đề, precedence là `FACT > POLICY > AUTHORITY`. Duplicate byte luôn được ghi audit; cấu hình demo có thể không chuyển tiếp duplicate để BGK chạy lại cùng fixture.

## 6. Hợp đồng OpenRouter và Qwen

- Endpoint: OpenRouter Chat Completions qua HTTPS.
- Model deploy: `qwen/qwen3-vl-8b-instruct`.
- Output: JSON Schema chặt, sau đó server chuẩn hóa có giới hạn và validate lại.
- Retry: không retry HTTP 429; tối đa một retry cho lỗi 5xx tạm thời.
- Quan sát chi phí: OpenRouter Activity và key limit.
- Bí mật: chỉ lưu `OpenRouter__ApiKey` trong user-secrets local hoặc Pool Manager production.

## 7. Database, storage và audit

EF Core dùng SQL Server. `Database__ApplyMigrationsOnStartup` cho phép migration có kiểm soát khi khởi động; production hiện đã có database và biến môi trường tại hosting. Ảnh nằm ở `ReceiptStorage__Directory`, không nằm trong static web root. Database giữ metadata và đường dẫn để route có kiểm soát phục vụ ảnh.

Audit được ghi theo sự kiện để không mất dấu hành động. Giao diện chiếu các sự kiện thành một hồ sơ duy nhất với trạng thái hiện tại, câu hỏi, câu trả lời quản lý và khả năng hoàn tác. Thiết kế này giữ lịch sử mà không tạo cảm giác lưu trùng ba bản ghi nghiệp vụ.

## 8. Dữ liệu kiểm thử

| Vị trí | Mục đích | Có được chạy tự động bởi Verify không |
|---|---|---|
| `wwwroot/test_data/` | 5 ảnh và expected result của Verify Harness | Có |
| `test_kit/judge-manifest.json` | 15 ca challenge để BGK đọc và tạo biến thể | Không |
| `test_kit/manifest.json` và `test_kit/images/` | Ngân hàng 30 ca tổng hợp cho benchmark có kiểm soát | Không |
| `test_kit/local_real/` | Ảnh thật đã được phép dùng và ẩn danh, chỉ test thủ công | Không và ảnh bị Git ignore |
| `publish/` | Output build cục bộ, bị Git ignore; có thể chứa bản fixture cũ | Không phải nguồn dữ liệu chuẩn |

`tools/generate_verify_receipts.py` tạo ngân hàng 30 ca với seed cố định, sau đó sao chép 5 ca đầu sang `wwwroot/test_data`. Bản trong `publish/` chỉ là output cũ; Web Deploy phải tạo package mới từ source thay vì tải thủ công folder này.

## 9. Biến môi trường production

| Biến | Chức năng |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | Chọn cấu hình runtime |
| `OpenRouter__ApiKey` | API key bí mật |
| `OpenRouter__Model` | Model Qwen |
| `ReceiptStorage__Directory` | Folder lưu ảnh riêng tư |
| `ReceiptStorage__MaxFileSizeMb` | Giới hạn upload |
| `ConnectionStrings__DefaultConnection` | SQL Server production |
| `Database__ApplyMigrationsOnStartup` | Bật/tắt migration khi khởi động |

Recycle application pool chỉ nạp lại bảy biến hiện có. Không cần tải lại publish XML hoặc publish lại code nếu chỉ thay giá trị Pool Manager.

## 10. Bằng chứng xác minh hiện tại

- Build .NET 8 sạch, 0 warning và 0 error tại lần kiểm tra gần nhất.
- 56 automated tests kiểm policy, workflow, audit, chống thao tác chồng, hợp đồng Qwen/OpenRouter và tính toàn vẹn Test Kit.
- Verify fixture v2 đạt 5/5 local ngày 22/09/2026.
- Người dùng xác nhận production upload, AI extraction và audit hoạt động đúng sau khi cập nhật API key ở Pool Manager.
- Video demo dưới ba phút đã được liên kết từ README; đường đánh giá tái lập cho BGK vẫn là localhost cùng test key được cấp riêng.

Kết quả fixture tổng hợp không phải accuracy trên tập hóa đơn độc lập.

## 11. Quyết định kiến trúc và giới hạn

| Quyết định | Lý do |
|---|---|
| Tách extraction và policy | Có thể đổi model mà không đổi quy tắc duyệt; policy unit-test được |
| Hosted Qwen 8B cho Sprint 1 | Triển khai nhanh, không đòi phần cứng 125B; Qwen 4B self-host là hướng benchmark sau |
| Fail-safe thay cho mock ngầm | Lỗi provider cần chuyển người xử lý, không được che bằng kết quả giả |
| Audit event và timeline projection | Giữ dấu vết đầy đủ nhưng UI chỉ hiển thị một hồ sơ nhất quán |
| Fixture tổng hợp tái lập | BGK có expected result rõ ràng mà không nhận dữ liệu cá nhân |

Sprint 1 chưa hỗ trợ PDF/nhiều trang, antivirus, tra cứu MST/e-invoice, tỷ giá, object storage hoặc authentication theo vai trò. Xem thêm [workflow đầy đủ](submission/AURA_WORKFLOW_SPEC.md), [runbook](docs/RUNBOOK.md), [test matrix](docs/TEST_CASES.md) và [hướng dẫn deploy](docs/DEPLOYMENT.md).
