# Trạng thái sẵn sàng demo ngày 29 09 2026

## Kết luận

Core workflow đã đủ ổn định để tiếp tục kiểm thử thủ công trên laptop: database đã migrate, upload dùng hàng đợi bền vững, OpenRouter là provider chính, policy quyết định tất định, human review và audit có thể phục hồi sau refresh. Build hiện tại đạt 0 warning, 0 error và 88 trên 88 automated test.

Project chưa được xem là hoàn tất cho bàn giao cuối. Bốn cổng còn mở là smoke Live URL sau hardening, holdout hóa đơn thật đã ẩn danh, phản hồi của ba người dùng và diễn tập có bấm giờ.

## Điều chỉnh đã hoàn tất

1. Upload lưu hồ sơ `PENDING`, trả HTTP 202 và để worker xử lý AI từ SQL queue. Refresh hoặc restart không làm mất job.
2. Global gate chỉ còn bảo vệ Verify Harness; upload và thao tác quản lý không khóa lẫn nhau.
3. Worker dùng lease, reclaim job và exponential backoff 5 đến 60 giây khi database tạm mất kết nối.
4. `/healthz` kiểm database, pending migration, storage, policy và cấu hình AI. Readiness không gọi provider nên không tốn credit.
5. `Test-DemoReadiness.ps1` kiểm đúng `AuraDb`, LocalDB, migration, storage, policy, provider và fallback trước demo mà không in secret.
6. Hàng đợi quản lý và Lịch sử hành vi hiển thị tên, mã và phòng ban người nộp.
7. OpenRouter và Ollama dùng chung schema, semantic validator và policy. Fallback chỉ áp dụng cho lỗi hạ tầng đủ điều kiện và mặc định tắt.

## Bằng chứng hiện có

| Cổng | Kết quả |
|---|---|
| Build | 0 warning, 0 error |
| Automated tests | 88 trên 88 pass, không gọi API trả phí |
| Local readiness | `AuraDb`, LocalDB, policy, storage, migration và health đều pass |
| Judge set OpenRouter | 15 trên 15 quyết định, 70 trên 75 field, P95 16,459 giây |
| Judge set Ollama | 14 trên 15 quyết định, 73 trên 75 field, P95 58,318 giây; bỏ sót TK-12 |
| OpenRouter concurrency smoke | 5 trên 5 hoàn tất, P95 17,072 giây |
| Official Verify | Vẫn đúng 5 fixture và expected result BTC đã biết |

Các số judge set dùng dữ liệu tổng hợp có nhãn. Chúng chứng minh regression và khả năng tái lập, không chứng minh accuracy production.

## Quyết định provider

Giữ OpenRouter làm primary cho demo. Model này không bỏ sót escalation trong judge set và đáp ứng mục tiêu 90 giây tốt hơn trên máy hiện tại. Giữ Ollama làm đường offline hoặc thao tác thủ công có giới hạn; chưa bật auto-fallback toàn cục vì Ollama tự duyệt sai TK-12 và không đủ công suất cho 4 đến 5 request đồng thời trên RTX 3050 Laptop 4 GB.

Không tự nâng package hoặc đổi model sát ngày demo. Project target .NET 8 nhưng máy hiện chỉ có SDK 9.0.312. Cấu hình này đã build được; chỉ thêm `global.json` pin SDK 8 sau khi cài đúng SDK 8 trên máy demo và chạy lại toàn bộ cổng kiểm thử.

NuGet vulnerability scan hiện không phát hiện package dễ bị tấn công trong app hoặc test project. Lệnh outdated chủ yếu đề xuất EF Core 10, Test SDK 18 và xUnit runner 4; đây là các major upgrade có thể thay runtime hoặc test behavior nên được hoãn tới nhánh nâng cấp riêng sau Chung kết. `Microsoft.VisualStudio.Azure.Containers.Tools.Targets` 1.23.0 là bản mới hơn nhưng không sửa blocker đang có, vì vậy giữ 1.22.1 cho build demo đã xác minh.

## Việc người vận hành cần thực hiện

### Một lượt xác nhận local sau khi pull

1. Mở PowerShell tại `D:\aura\AURA\AURA`.
2. Chạy `.\tools\Test-DemoReadiness.ps1 -StartLocalDb -SkipHttp`.
3. Chạy `dotnet ef database update`.
4. Chạy `dotnet build ..\AURA.sln --no-restore`.
5. Chạy `dotnet test tests\AURA.Tests\AURA.Tests.csproj --no-restore` và xác nhận 88 trên 88.
6. Chạy `dotnet run`.
7. Ở PowerShell thứ hai, chạy `.\tools\Test-DemoReadiness.ps1` và chỉ tiếp tục khi kết quả là `READY`.
8. Upload một ảnh tổng hợp bằng OpenRouter, refresh trong lúc xử lý và xác nhận trạng thái vẫn về `COMPLETED`.
9. Chạy Verify đúng một lượt. Không cần chạy lại judge set 15 ca nếu code AI, policy và fixture không đổi.
10. Chuyển một hồ sơ, cho quản lý trả lời rồi Undo; xác nhận người nộp và timeline hiển thị đúng.

### Bằng chứng còn phải thu thập

1. Mười đến mười lăm hóa đơn thật có sự đồng thuận, đã xóa tên, địa chỉ, số điện thoại, mã thẻ và dữ liệu nhận diện. Khóa ground truth trước khi gọi model.
2. Ba người từng nộp hoặc duyệt hoàn ứng. Ghi thời gian hoàn thành, lỗi gặp phải, số lần mở ảnh gốc và một thay đổi sản phẩm xuất phát từ phản hồi.
3. Một HAR đã xóa Authorization, cookie và PII cho upload 202, chuỗi status và một Verify.
4. Một smoke Live URL sau hardening gồm health, upload, refresh, manager action, audit và kiểm tra ảnh sau recycle.
5. Ba dress rehearsal OpenRouter dưới 90 giây cho Official Verify, cùng một video dự phòng.

## Khi nào project được xem là hoàn tất

### Hoàn tất cho Chung kết

- Preflight trả `READY` trên laptop demo.
- Build sạch và 88 trên 88 test pass.
- Official Verify đạt 5 trên 5 trong ba buổi diễn tập, mỗi lượt dưới 90 giây.
- Một upload mới, forward, manager decision, Undo và audit chạy trọn luồng.
- Năm upload OpenRouter đồng thời không có global 409 và đều đạt trạng thái cuối.
- Live URL đã smoke lại hoặc có ticket ghi rõ blocker hạ tầng cùng video dự phòng.
- Có holdout độc lập và phản hồi ba người dùng, hoặc văn bản xác nhận BTC rằng hạng mục người dùng là tùy chọn.
- README, slide, Build Log, workflow và báo cáo kiến trúc dùng cùng số liệu.

### Chưa đủ cho production doanh nghiệp

Production còn cần authentication và RBAC, object storage có retention, antivirus, rate limiting, distributed queue/circuit state, backup và restore được diễn tập, giám sát cảnh báo, chính sách xóa PII và đánh giá dữ liệu lớn hơn. Những hạng mục này không nên chen vào build Chung kết nếu chưa có thời gian kiểm thử riêng.

## Khi cần báo BTC

Không cần ticket chỉ vì số automated test tăng hoặc tài liệu được sửa. Nên tạo check-in vì pipeline đã thay đổi đáng kể từ request đồng bộ sang HTTP 202 và DB-backed worker, đồng thời bổ sung controlled fallback và bằng chứng 15-case. Official Verify vẫn giữ nguyên năm ca và expected result, nên không phải thông báo thay bộ test. Nếu Live URL chưa smoke được sau hardening, nêu rõ đây là blocker hạ tầng trong check-in thay vì đánh dấu hoàn tất.
