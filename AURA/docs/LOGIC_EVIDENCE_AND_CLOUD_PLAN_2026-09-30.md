# Logic, evidence và kế hoạch self-host VLM ngày 30 09 2026

## Kết luận

Gói hardening này sửa một false escalation có cơ sở từ hóa đơn giấy thực tế: `Check`, `Receipt No` và mã giao dịch riêng trước đây bị ép chung vào `invoiceNumber`. Contract mới tách ba loại định danh, không nới rule cho ShopID/POS/MID/TID/pager, và vẫn giữ thứ tự `FACT → POLICY → AUTHORITY`. Bộ test offline tăng từ 88 lên 93 và Official Verify vẫn giữ đúng 5 ca/expected đã báo BTC.

Không nên đổi major framework/package hoặc bật cloud fallback tự động trước khi có endpoint được bảo vệ và benchmark lại. Từ 30/09 đến ngày chốt 15/10 còn đủ thời gian cho một proof of concept self-host tách biệt, nhưng không đủ an toàn để thay baseline OpenRouter nếu chưa qua cùng safety gate, latency gate và restart test.

## Thay đổi logic

### Ba loại định danh

- `invoiceNumber`: số hóa đơn chính thức, ví dụ `Số hóa đơn`, `Invoice No`.
- `receiptNumber`: số biên nhận hoặc bill, ví dụ `Receipt No`, `Bill No`, `Số biên nhận`.
- `transactionReference`: mã riêng cho đúng lần mua, ví dụ `Check`, `Transaction No`, `Trace`, `RRN`, `Mã giao dịch`.

Một chứng từ giấy có ít nhất một trong ba trường trên mới vượt qua traceability gate. Các giá trị ShopID, branch ID, POS/register, MID/TID, pager/table, tax ID, số điện thoại, serial và mẫu số không được tính là mã giao dịch.

### Precedence có giải thích đầy đủ

Nếu một hồ sơ vừa thiếu facts vừa có bia/rượu hoặc hạng mục cấm, status vẫn là `ESCALATE_FACT` để không quyết định policy từ facts chưa đáng tin. Tuy nhiên reason và manager question ghi thêm policy finding thứ cấp. Cách này giữ authority nhất quán mà không che rủi ro đã nhìn thấy.

### Không tự suy khoản chi cá nhân

Tên món cà phê không đủ để kết luận chi cá nhân. Muốn quyết định đúng cần policy doanh nghiệp và context do người nộp cung cấp như mục đích chi, cost center/project, khách hàng/người tham dự và xác nhận công tác. Chưa có contract đã duyệt cho các trường này nên build hiện tại không tự thêm rule; mọi tuyên bố như vậy sẽ tạo false rejection khó bảo vệ.

## Bằng chứng tự động

Năm test mới khóa các hành vi sau:

1. Paper receipt có `receiptNumber` hợp lệ có thể auto approve nếu mọi rule khác đạt.
2. Paper receipt có per-purchase `transactionReference` hợp lệ có thể auto approve.
3. ShopID và terminal/POS không thay mã giao dịch, nên vẫn FACT.
4. FACT vẫn là status chính nhưng reason không che policy finding thứ cấp.
5. Semantic validator xóa field số bị lặp sau repair và từ chối một mã được gán đồng thời cho nhiều field canonical.

Lệnh tái lập:

```powershell
cd D:\aura\AURA\AURA
dotnet test .\tests\AURA.Tests\AURA.Tests.csproj --configuration Release
```

Kỳ vọng hiện tại: 100/100 pass, không gọi API trả phí.

## Test thật bắt buộc sau thay đổi schema

Vì prompt/JSON schema thay đổi, evidence OpenRouter cũ không đủ để chốt build mới. Cần chạy theo thứ tự:

1. Preflight offline với `-StartLocalDb -SkipHttp`.
2. `dotnet run`, sau đó preflight đầy đủ ở PowerShell thứ hai.
3. Upload lại hóa đơn có `Check: 221196`, claimed amount 59000. Ghi raw facts và xác nhận model đặt mã vào `transactionReference`, không phải ShopID/POS/pager.
4. Chạy Official Verify đúng một batch. Kỳ vọng không đổi: 3 AUTO_APPROVE, 1 ESCALATE_FACT, 1 ESCALATE_POLICY.
5. Chạy một receipt chỉ có ShopID/POS nhưng không có mã riêng. Kỳ vọng FACT.
6. Chạy một fixture vừa thiếu identifier vừa có beer. Kỳ vọng primary status FACT và reason có policy finding thứ cấp.
7. Chụp F12 request/status, UI facts, audit event, OpenRouter Activity/generation metadata và terminal commit hash.

Không sửa expected của Official Verify. Nếu actual thay đổi, dừng và điều tra thay vì sửa ground truth.

## LocalDB registry test

Lỗi LocalDB là lỗi theo Windows user-instance/runtime, có thể biến mất sau reboot hoặc chỉ xuất hiện khi tiến trình stale còn giữ trạng thái. `dotnet run` thành công trong một phiên không chứng minh registry luôn khỏe. Kiểm tra bằng một lệnh duy nhất:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Test-DemoReadiness.ps1" -StartLocalDb -SkipHttp -DiagnoseLocalDb
```

Script in user, version, instance list và instance details; không in credentials và không sửa registry/MDF. Gate hợp lệ là `READY`. Nếu output chứa registry error dù exit code bằng 0, coi là `NOT READY`, reboot một lần, kiểm lại và lưu ảnh trước/sau. Chỉ cân nhắc repair instance sau khi đã backup database và xác định đúng file.

## Package decision

Ngày 30/09/2026, `dotnet list package --vulnerable --include-transitive` không báo package vulnerable. NuGet có EF Core 10 mới hơn nhưng project target .NET 8 và đang pin EF Core 8.0.31; không nâng major trước demo. `Microsoft.VisualStudio.Azure.Containers.Tools.Targets` có minor mới nhưng không sửa business/runtime path đang demo, nên giữ 1.22.1 cho tới sau ngày chốt.

## Phương án cloud cho model self-host

### Khuyến nghị cho Chung kết

Giữ OpenRouter 8B làm primary và Ollama laptop làm manual offline option. Đây là baseline đã có số liệu. Self-host cloud là proof of concept tách biệt, không được quảng bá là fallback production trước khi qua test.

### Phương án dễ nhất trong 15 ngày

Runpod Secure Cloud phù hợp hơn Google Compute Engine cho proof of concept ngắn: tạo Pod hoặc Serverless endpoint từ template, tính phí theo thời gian chạy và không phải xin GPU quota kiểu GCP. Với AURA có hai nhánh:

- Pod + Ollama: gần adapter hiện tại nhất nhưng bắt buộc đặt reverse proxy có TLS và bearer auth; không mở thẳng port 11434 công khai.
- Serverless + vLLM: có API key và OpenAI-compatible endpoint, Qwen3-VL-4B được vLLM hỗ trợ; AURA cần thêm adapter/config riêng và phải benchmark image + JSON schema trước khi dùng.

Runpod Serverless có cold start khi worker bằng 0. Nếu dùng trong demo, đặt một active worker để warm, chấp nhận idle cost và chạy preflight trước giờ chấm. Pod dễ quan sát hơn nhưng phải tự start/stop và dữ liệu persistent cần network volume hoặc tải lại model.

### Vì sao chưa chọn Google Cloud VM

GCP G2/L4 có 24 GB VRAM và phù hợp inference, nhưng cần billing, GPU quota, VPC/firewall, image/driver, TLS/auth, disk lifecycle và budget alert. Nó hợp pilot dài hơn; proof of concept hai tuần có nhiều bước vận hành hơn Runpod. Không dùng Spot/Preemptible cho live demo vì instance có thể bị thu hồi.

### Checklist tạo tài khoản và proof of concept

1. Tạo account bằng email riêng cho project, bật MFA.
2. Nạp ngân sách nhỏ, đặt spend alert/limit và không dùng savings plan dài hạn.
3. Chọn Secure Cloud; không đưa hóa đơn thật có PII vào Community Cloud.
4. Dùng Qwen3-VL-4B trước; chọn GPU có tối thiểu khoảng 16 GB VRAM để còn headroom ảnh/context/runtime.
5. Tạo endpoint, đặt API key/model token bằng secret, không ghi vào source/log/screenshot.
6. Khóa network: TLS, bearer auth, chỉ backend AURA gọi; browser không gọi model trực tiếp.
7. Smoke một ảnh tổng hợp, rồi chạy Official 5 và judge15 với fallback tắt.
8. Ghi cold/warm P50/P95, missed/over escalation, system error, cost/case và served model revision.
9. Chạy primary fail, cloud fallback success và both fail; xác nhận audit đúng provider.
10. Stop/terminate resource sau test; xác nhận billing về 0 và xóa receipt/model cache không cần giữ.

### Gate trước khi nối vào fallback

- 5/5 Official Verify trong ba lượt tách biệt.
- Không missed escalation trên judge15 và holdout mới.
- P95 nằm trong time budget khi warm; cold start được mô tả riêng.
- Endpoint có TLS/auth, timeout và health check.
- AURA audit primary/served/error/fallback chính xác.
- Có rollback một config về OpenRouter-only.
- Không dùng real receipt nếu chưa có consent, redaction và retention rule.

## Definition of done trước 15 10

Core demo được chốt khi 100/100 offline test pass, OpenRouter schema mới qua one-real anonymized smoke và cổng discount, Official Verify 5/5 ba rehearsal, LocalDB preflight ổn định sau reboot, restart persistence pass và slide/script dùng đúng commit chốt. Cloud self-host không phải blocker; chỉ đưa vào demo nếu hoàn tất toàn bộ gate trên trước freeze ít nhất 48 giờ.
