# Quyết định demo, Live URL và self-hosted VLM — 02/10/2026

## Quyết định ngắn gọn

1. **Demo Chung kết:** chạy localhost trên laptop cá nhân, OpenRouter là primary, fallback tắt. Đây là đường BTC đã xác nhận và là baseline có nhiều evidence nhất.
2. **Live URL:** là bằng chứng bàn giao phụ, không phải dependency của phần trình diễn. Tiếp tục dùng SmarterASP.NET hiện có nếu smoke lại toàn bộ cổng; không đổi nền tảng chỉ để có URL mới sát ngày chốt.
3. **Ollama local:** giữ như đường offline/manual có kiểm soát. Fallback live 1/1 đã pass, nhưng latency 84,425 giây và missed escalation TK-12 khiến nó chưa phù hợp làm fallback tự động mặc định hoặc phục vụ 4–5 request đồng thời trên GPU 4 GB.
4. **GPU cloud/doanh nghiệp:** chỉ làm POC tách biệt sau khi baseline local đã freeze. Không public cổng Ollama thô và không dùng POC làm đường demo duy nhất.

## Tại sao đây là lựa chọn phù hợp

| Phương án | Phù hợp hiện tại | Ưu điểm | Rủi ro/điều kiện |
|---|---|---|---|
| Laptop + OpenRouter | **Đường demo chính** | Đúng hướng BTC; judge 15/15; concurrency smoke 5/5; không phụ thuộc hosting app | Vẫn cần Internet cho OpenRouter; phải có video/evidence dự phòng |
| SmarterASP.NET + OpenRouter | **Live URL phụ** | Đã smoke; khớp ASP.NET Core + SQL Server hiện tại; ít thay đổi | Trial/runtime/storage cần smoke lại; không dùng LocalDB trên host |
| Azure App Service + Azure SQL | Phương án production/pilot sạch hơn | Managed web + managed SQL, managed identity/GitHub deployment có tài liệu chính thức | Chi phí và setup cao hơn; migration/deploy lại không đáng làm sát demo |
| Railway Hobby | Không phải drop-in tốt nhất | Triển khai container/GitHub đơn giản, mức chi tối thiểu thấp | AURA dùng EF SQL Server; vẫn cần SQL Server ngoài hoặc đổi provider/database, tạo rủi ro migration |
| Runpod GPU + Ollama/vLLM | POC self-hosted VLM | GPU theo nhu cầu, phù hợp thử privacy/latency | Phải bảo vệ endpoint, quản lý model/storage/monitoring; không thay thế app/database host |

Nguồn chính thức để kiểm tra lại trước lúc mua: [Runpod Pod pricing](https://docs.runpod.io/pods/pricing), [Runpod expose ports](https://docs.runpod.io/pods/configuration/expose-ports), [Runpod network volumes](https://docs.runpod.io/storage/network-volumes), [Railway pricing](https://railway.com/pricing), [Azure App Service pricing](https://azure.microsoft.com/en-us/pricing/details/app-service/windows/), [Microsoft App Service + Azure SQL tutorial](https://learn.microsoft.com/en-us/azure/app-service/tutorial-dotnetcore-sqldb-app), [Ollama Docker/GPU](https://docs.ollama.com/docker).

## Ranh giới kỹ thuật bắt buộc

- LocalDB chỉ dùng local development/demo; không dùng làm database cho web production. Production dùng SQL Server/Azure SQL có backup và connection secret riêng.
- `127.0.0.1:11434` trên server là loopback của server, không phải laptop của người trình bày.
- AURA hiện cho phép remote `Ollama:BaseUrl` qua HTTPS nhưng chưa gắn API key/bearer header cho Ollama. Do đó remote GPU chỉ an toàn khi đi qua private network/VPN, hoặc sau khi app được bổ sung auth header và regression đầy đủ.
- HTTPS proxy Runpod có giới hạn request khoảng 100 giây. Pipeline AURA đã dùng HTTP 202 + background queue + polling, nên inference không cần giữ request upload mở; tuy nhiên request từ worker tới model vẫn cần timeout/queue phù hợp.
- Network volume của GPU provider giải quyết persistence model, không thay thế SQL database, object storage hóa đơn, retention policy hay audit.

## Nếu doanh nghiệp cho mượn GPU

Xin rõ các thông tin sau trước khi cấu hình:

1. GPU model, VRAM khả dụng, thời hạn/quota và có được chạy 24/7 hay không.
2. Hệ điều hành Linux, NVIDIA driver, CUDA/container runtime và quyền dùng Docker.
3. Private IP/VPN/VPC hay public IP; domain và TLS do ai cấp.
4. Cơ chế xác thực: bearer token/mTLS/IP allowlist; tuyệt đối không mở Ollama 11434 công khai không auth.
5. Persistent volume đủ chứa model, log và cache; model Qwen3-VL nào được phép tải.
6. Chính sách dữ liệu: receipt có được rời laptop không, vùng địa lý, retention, backup, ai được truy cập và DPA/consent.
7. Monitoring: health, GPU memory/utilization, queue depth, timeout, error rate, cost/quota alert.

Khuyến nghị bắt đầu với ít nhất 8 GB VRAM cho POC 4B; 12–16 GB tạo khoảng trống tốt hơn cho context và concurrency. Đây là sizing ban đầu, không phải cam kết capacity: phải đo 1, 2 rồi 5 request trên đúng model/quantization trước khi công bố.

## Cổng chấp nhận cho Live URL

Chỉ công bố Live URL khi cùng một revision đạt đủ:

- `/healthz` HTTP 200, database up-to-date, storage writable;
- UI tải đúng ở cửa sổ ẩn danh và mobile;
- một positive receipt và một fail-safe receipt đúng decision;
- Verify Harness 5/5;
- manager YES/NO/UNDO có audit;
- recycle/redeploy không làm mất status, ảnh hoặc audit;
- không có secret/PII trong Git, log, ảnh chụp hoặc video.

Nếu một cổng fail, vẫn demo localhost và mô tả Live URL là staging chưa đạt gate; không che lỗi bằng fallback.

## Lộ trình an toàn trước ngày chốt

1. Freeze code baseline; chạy build, 108 test, preflight và ba dress rehearsal local.
2. Hoàn tất blind holdout 10–15 ảnh đa merchant, khóa ground truth/hash trước lượt đầu.
3. Smoke lại SmarterASP.NET đúng checklist; chỉ giữ link trong submission nếu pass.
4. Thu feedback ba người dùng khi doanh nghiệp kết nối; ghi task completion, hiểu decision, thời gian và lỗi UX.
5. GPU POC là nhánh riêng: synthetic first, không receipt thật; đo correctness/latency/concurrency; chỉ sau đó mới cân nhắc remote fallback.
6. Sau demo mới triển khai auth cho remote Ollama, managed object storage, RBAC và retention nếu đi production pilot.

## Claim được phép dùng

> AURA đã chứng minh fallback OpenRouter sang Ollama hoạt động và audit đúng trên một ca cô lập. Baseline Chung kết vẫn ưu tiên OpenRouter và để fallback tắt vì local 4B chưa qua safety gate và latency một ca đã sát 90 giây. Localhost là đường demo chính; Live URL và GPU self-hosted là các cổng triển khai riêng, chỉ được công bố sau khi pass health, persistence, privacy và regression.
