# AURA Final Demo Test Plan

Cập nhật: 01/10/2026  
Baseline đã push: `master` tại `d4816f7` trước khi tài liệu kế hoạch này được chốt.  
Mục tiêu: hoàn tất thay đổi kỹ thuật và bằng chứng trước 06/10, dành trọn 07–10/10 để review source, luyện nói và diễn tập.

## Kết luận vận hành

Core workflow đủ điều kiện để tiếp tục kiểm thử. Không cần reset database, đổi LocalDB instance, nâng package hoặc bật fallback. Cổng quan trọng nhất còn thiếu là chạy lại provider thật sau khi JSON Schema tách `invoiceNumber`, `receiptNumber` và `transactionReference`.

Lỗi LocalDB registry chỉ được xem là đã khép lại khi lệnh full preflight chạy trong đúng Windows account dùng để demo kết thúc bằng `READY: 0 failures, 0 warning(s)` sau một lần reboot. Việc upload từng chạy được là bằng chứng tốt nhưng chưa thay thế preflight. Ngược lại, lỗi chỉ xuất hiện trong Codex sandbox không phủ định kết quả READY từ PowerShell tương tác của người dùng.

## Lệnh chuẩn không xuống dòng

Tất cả lệnh dưới đây chạy trong Windows PowerShell, không cần Run as Administrator.

### Một lần sau reboot khi app chưa chạy

```powershell
cd D:\aura\AURA\AURA
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Test-DemoReadiness.ps1" -StartLocalDb -SkipHttp -DiagnoseLocalDb
```

Kết quả chấp nhận được ở bước này là `0 failures` và đúng một warning vì chủ động bỏ qua HTTP. Nếu có registry failure, dừng tại đây, đóng AURA/Visual Studio/SSMS, reboot một lần và chạy lại. Không xóa instance, registry hoặc MDF.

### Xác minh offline sau khi source thay đổi

```powershell
cd D:\aura\AURA\AURA
dotnet build .\AURA.csproj -c Release --no-restore
dotnet test .\tests\AURA.Tests\AURA.Tests.csproj -c Release --no-restore
dotnet ef migrations has-pending-model-changes --project .\AURA.csproj --startup-project .\AURA.csproj --configuration Release --no-build
```

Kỳ vọng: build `0 warning, 0 error`; test `93/93`; EF báo không có model change chưa migration. Chỉ chạy `dotnet ef database update` khi source thực sự có migration mới hoặc health báo pending migration; không dùng lệnh này như thao tác reset.

### Chạy app và full preflight

PowerShell thứ nhất:

```powershell
cd D:\aura\AURA\AURA
dotnet run
```

PowerShell thứ hai, sau khi thấy `Now listening on: http://localhost:5000`:

```powershell
cd D:\aura\AURA\AURA
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Test-DemoReadiness.ps1" -DiagnoseLocalDb
```

Chỉ bắt đầu demo AI khi kết quả là `READY: 0 failures, 0 warning(s)`.

### Health check độc lập

```powershell
Invoke-RestMethod "http://localhost:5000/healthz" | ConvertTo-Json -Depth 5
```

Kỳ vọng: `status=ok`, `databaseAvailable=true`, `databaseUpToDate=true`, `pendingMigrationCount=0`, `storageAvailable=true`, provider/model đúng và `fallbackEnabled=false`.

## Ma trận test bắt buộc sau hardening định danh và trường ngày

| Nhóm | Số lượt | Kỳ vọng | Evidence tối thiểu |
|---|---:|---|---|
| Receipt có Receipt No hoặc Bill No | 2 | `receiptNumber` được điền; decision đúng ground truth | Network response, facts JSON, UI, latency |
| Receipt có Check, Trace, RRN hoặc Transaction No | 2 | `transactionReference` được điền; không gán vào ShopID/POS | Network response, facts JSON, UI, latency |
| Negative chỉ có ShopID/POS/terminal | 2 | Không được dùng làm mã truy vết; `ESCALATE_FACT` | Reason, facts JSON, ảnh synthetic |
| Official Verify 5 ca | 3 batch | Mỗi batch 3 AUTO, 1 FACT, 1 POLICY; 5/5 PASS; dưới 90 giây | Bảng đủ 5 dòng, total time, response |
| Một hóa đơn thật đã ẩn danh | 3 lượt cùng ảnh trên commit sau repair ngày | Decision, `invoiceDate` và ba identifier ổn định | Consent/ẩn danh, raw facts JSON, expected khóa trước, actual, latency |
| Restart persistence | 1 lượt sau commit chốt | Cùng request ID, status, ảnh và audit còn sau restart | Before/after + full preflight |
| Human review | 1 accept, 1 reject, 1 undo | State và audit đúng | Timeline trước/sau |
| Concurrent smoke | 5 upload tổng hợp | Không global 409; cả 5 về trạng thái cuối | Accepted/final status và latency |

Không dùng 93 automated tests để tuyên bố model đã đọc đúng 93 ảnh. Automated tests bảo vệ code; ma trận trên mới kiểm provider thật.

Ca Highlands ngày 01/10 đã chứng minh model có thể đặt cùng ngày in trên phiếu vào `transactionDate` ở một lượt và `invoiceDate` ở lượt sau. Sau hardening, không trộn hai lượt cũ vào tỷ lệ pass của commit mới. Cổng đạt là ba upload mới liên tiếp đều có `invoiceDate=2026-09-29`, `transactionReference=221196`, decision `AUTO_APPROVE`, raw JSON lưu được và không có `ValidationIssues`/provider error.

## Những phần còn phải xem xét

### Bắt buộc trước 06/10

1. Live re-validation ba loại identifier trên OpenRouter.
2. Official Verify ba lượt sau schema mới.
3. Một restart persistence sau commit chốt và full preflight sau reboot.
4. Hai nhánh manager, Undo và audit.
5. Một concurrent smoke năm upload nếu muốn chứng minh tình huống nhiều BGK.
6. Khóa evidence theo commit, provider, model, fallback, timestamp và expected/actual.

### Nên hoàn tất nếu có dữ liệu

1. Holdout 10–15 hóa đơn thật có quyền sử dụng, đã ẩn danh và khóa ground truth trước khi chạy.
2. Ba người dùng thử; nếu BTC xác nhận optional thì vẫn nên có ít nhất ba lượt usability ngắn.
3. Smoke Live URL sau hardening nếu còn sử dụng URL public trong presentation.

### Tùy chọn và có điểm cắt

Cloud self-hosted VLM chỉ là PoC. Quyết định Go/No-Go cuối ngày 05/10. Nếu chưa có HTTPS/auth, model load ổn định và Verify 5/5 thì loại khỏi demo chính; không kéo dài sang giai đoạn review.

### Không triển khai trước Chung kết

Authentication/RBAC production, object storage/retention hoàn chỉnh, antivirus, distributed queue/circuit state, SignalR cross-tab, PDF nhiều trang và tax/e-invoice lookup. Đây là backlog production, không phải blocker của vertical slice demo.

## Lộ trình từ 01 đến 10 tháng 10

| Ngày | Mục tiêu | Điều kiện kết thúc ngày |
|---|---|---|
| 01/10 | Đồng bộ runbook, lệnh preflight và kế hoạch test | Một nguồn lệnh duy nhất, không còn hướng dẫn LocalDB cũ |
| 02/10 | Test live identifier và negative ShopID/POS | Sáu request có evidence, không có false auto-approve |
| 03/10 | Verify ba batch và hóa đơn thật ổn định | 15/15 Verify rows pass; real receipt expected/actual khóa |
| 04/10 | Restart, manager accept/reject/undo, audit | Persistence và human workflow có before/after |
| 05/10 | Concurrent smoke; quyết định cloud PoC | 5 upload hoàn tất; cloud có quyết định Go/No-Go |
| 06/10 | Sửa blocker cuối, build/test, sync docs và tạo commit candidate | Không còn P0; artifact và evidence cùng baseline |
| 07/10 | Review source và kiến trúc | Tự giải thích được request path, queue, AI, policy, DB, audit |
| 08/10 | Review AI, benchmark, privacy và provider | Tự giải thích được VLM/LLM, metrics, fallback, OpenRouter/Ollama |
| 09/10 | Rehearsal có bấm giờ và Q&A | Một lượt hoàn chỉnh, không cần nhìn runbook liên tục |
| 10/10 | Freeze tính năng và tổng duyệt | Chỉ sửa blocker có rollback; video/PDF/screenshots sẵn |

## Nội dung cần học trong bốn ngày review

1. `ApplicantController` → HTTP 202 → database record → worker claim/lease → provider → semantic validation → policy → audit.
2. FACT, POLICY và AUTHORITY; vì sao model không có quyền duyệt.
3. JSON Schema, one-repair và ba loại identifier.
4. LocalDB, EF migration, RowVersion, private receipt storage và restart recovery.
5. OpenRouter so với Ollama: decision exact, field exact, P50/P95, privacy, cost và missed escalation.
6. Vì sao fallback có allowlist/circuit breaker nhưng baseline vẫn tắt.
7. Ranh giới prototype với production và câu trả lời khi BGK đưa input mới.

## Go no go ngày demo

GO khi full preflight 0/0, health `ok`, một smoke đạt trạng thái cuối, fallback tắt, OpenRouter credit/mạng ổn và evidence dự phòng mở được. NO GO cho live AI nếu registry/full preflight fail, health 503, migration pending, provider 401/402/429 chưa xử lý hoặc request trước còn PENDING/PROCESSING.
