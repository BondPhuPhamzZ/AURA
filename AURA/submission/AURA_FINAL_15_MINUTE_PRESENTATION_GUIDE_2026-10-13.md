# AURA — Kịch bản Chung kết tối đa 15 phút

## Mục tiêu

Trình bày trong khoảng **13 phút 30 giây**, giữ 90 giây dự phòng. Trọng tâm là bài toán nghiệp vụ,
luồng con người quyết định, bằng chứng kỹ thuật và demo chạy thật. Không đọc toàn bộ chữ trên slide.

**Phân biệt hai artifact:** video nộp Drive là một bản product-only tối đa 3 phút theo đề Track, không
trình bày slide. Phần `Live demo hoặc video` bên dưới là khung onsite trong phiên thuyết trình. Nếu live
ổn, không cần phát lại toàn bộ video nộp. Nếu live lỗi, dùng chính video 2:45–2:58 đã nộp làm fallback
và dành thời gian còn lại để mở evidence/Audit.

## Timeline

| Thời gian | Phần | Mục tiêu |
|---:|---|---|
| 0:00–1:15 | Slide 1 | Nêu vấn đề và luận điểm sản phẩm |
| 1:15–2:45 | Slide 2 | Giải thích luồng và hai điểm con người quyết định |
| 2:45–4:20 | Slide 3 | Trình bày evidence, ranh giới claim và KPI pilot |
| 4:20–5:30 | Slide 4 | Giải thích kiến trúc và vai trò giới hạn của model |
| 5:30–6:30 | Slide 5 | Nêu fail-safe và phần chưa có |
| 6:30–9:28 | Live demo hoặc video product-only 2 phút 58 giây | Chứng minh Verify, quyết định, human workflow, audit và Undo |
| 9:28–11:20 | Kết quả và bài học | Nói rõ safety, over-escalation và concurrency |
| 11:20–12:30 | User evidence | Chỉ trình bày session đã có thật; nếu chưa đủ thì công bố gate còn mở |
| 12:30–13:30 | Kết luận | Giá trị, trạng thái hiện tại và bước pilot |
| 13:30–15:00 | Buffer | Chuyển màn hình hoặc xử lý câu hỏi ngắn |

## Slide 1 — Hoàn ứng hôm nay

### Lời nói gợi ý

“Một hồ sơ hoàn ứng hiện phải qua nhiều điểm chờ: nhân viên gửi ảnh, người xử lý đọc thủ công,
đối chiếu policy, hỏi quản lý và lưu lịch sử. Nút thắt không chỉ là OCR. Nút thắt là biết khi nào dữ
kiện đủ để đi tiếp và khi nào phải dừng để hỏi người có thẩm quyền.

AURA đóng vai trò Escalation Referee. Qwen chỉ đọc dữ kiện. Policy C# quyết định theo thứ tự FACT,
POLICY rồi AUTHORITY. Con người xử lý ngoại lệ và toàn bộ timeline được audit.”

### Câu chuyển

“Điểm khác biệt nằm ở cách AURA đặt con người vào đúng hai vị trí trong quy trình.”

## Slide 2 — Một luồng, hai điểm con người quyết định

### Lời nói gợi ý

“Khi upload, server xác thực file, lưu hồ sơ PENDING và trả HTTP 202. Worker nhận job bằng lease nên
refresh hoặc restart không làm mất hàng đợi. Vision trích dữ kiện và có đúng một semantic repair.
Policy tất định mới tạo AUTO_APPROVE hoặc ESCALATE.

Điểm người thứ nhất là nhân viên xác nhận chuyển tiếp. Hệ thống không tự đẩy hồ sơ âm thầm. Điểm
người thứ hai là quản lý trả lời một câu hỏi cụ thể bằng Đồng ý hoặc Từ chối. Quyết định gần nhất có
thể hoàn tác và Audit vẫn giữ lịch sử.”

### Câu chuyển

“Thiết kế này chỉ có ý nghĩa khi evidence được đo minh bạch.”

## Slide 3 — Bằng chứng kỹ thuật

### Lời nói gợi ý

“Candidate hiện đạt 163 trên 163 test offline và Release build sạch. Verify Harness chạy đúng năm ca
BTC, đạt 5 trên 5 trong 65,004 giây, dưới gate 90 giây.

Prospective mini-holdout mới đạt 8 trên 10, không có missed escalation và có hai over-escalation
fail-safe. Chúng tôi không gọi con số này là production accuracy. Lượt lặp tuần tự trên cùng 12 ảnh
đạt 11 trên 12, nhưng chỉ dùng để đo stability vì dữ liệu đã được nhìn thấy.

KPI cần đo trong pilot vẫn là touchless rate, review time, over-escalation và missed escalation.”

### Câu cần tránh

- Không nói 11/12 là holdout mới.
- Không nói 163 test tương đương 163 hóa đơn.
- Không nói hệ thống đạt production accuracy.

## Slide 4 — Kiến trúc

### Lời nói gợi ý

“AURA là vertical slice chạy thật trên ASP.NET Core. Razor UI gửi upload bất đồng bộ. SQL giữ queue,
lease, hồ sơ và audit. Qwen3.8 Flash qua Alibaba là primary hiện hành. Ollama chỉ là fallback opt-in
trên máy local.

Model có thể thay nhưng policy không đổi. Model không có quyền phê duyệt. Test Kit là dữ liệu mô
phỏng; upload, vision provider, policy, SQL, Audit và Undo đều chạy thật.”

### Câu chuyển

“Khi upstream hoặc dữ liệu không đáng tin cậy, AURA ưu tiên dừng an toàn.”

## Slide 5 — Fail-safe và giới hạn

### Lời nói gợi ý

“HTTP 429 hoặc timeout chỉ được fallback một lần khi fallback đã bật và lỗi nằm trong allowlist.
Schema hoặc semantic sai chỉ repair một lần rồi chuyển FACT. Job nằm trong database nên app restart
có thể reclaim.

Baseline đã có queue bền vững, 163 test, Verify 5/5 dưới 90 giây, concurrency upload 5/5 và mobile
4G/5G. Chúng tôi công khai phần chưa có: authentication production, PDF nhiều trang, object storage
và ba user session chính thức.”

## Demo product-only tối đa 3 phút

### Phương án chính

Ưu tiên live demo, nhưng mở sẵn video dự phòng. Nếu live URL không phản hồi sau khoảng 10 giây,
chuyển ngay sang video và nói rõ đây là bản ghi trên cùng candidate.

Video chính thức nộp Drive phải theo `submission/AURA_OFFICIAL_VIDEO_SCRIPT_3_MIN_VI_2026-10-14.md`,
không vượt 180 giây và không chèn slide. Không quay thêm video riêng cho Verify, kiến trúc hoặc
evidence overlay.

1. **0:00–0:12 — Mở live:** nêu một câu về vai trò của model, policy và con người.
2. **0:12–1:32 — Verify:** bấm đúng một lần, để UI tự cuộn và giữ progress/timestamp trên màn hình;
   chỉ kết luận khi bảng đủ 5/5.
3. **1:32–2:08 — Decision và handoff:** mở một hồ sơ synthetic ESCALATE đã chuẩn bị, chỉ facts/reason,
   xác nhận chuyển tiếp rồi chọn Đồng ý hoặc Từ chối ở tab quản lý.
4. **2:08–2:28 — Audit và Undo:** chỉ timeline trong cùng hồ sơ và hoàn tác quyết định gần nhất.
5. **2:28–2:42 — Kiến trúc ngay trên UI:** chỉ provider, status, facts và câu hỏi; giải thích SQL queue,
   extraction và policy mà không mở slide.
6. **2:42–2:58 — Giới hạn và chốt:** giữ AURA trên màn hình, nói rõ JPG/PNG một ảnh, chưa có RBAC/PDF
   production và fallback live tắt.

### Chuẩn bị trước demo

- Đóng các tab không cần thiết và không để lộ Pool Manager, API key hoặc receipt riêng tư.
- Kiểm `/healthz`: status ok, Qwen3.8 Flash, reasoning none, DB/storage/migration true, fallback false.
- Giữ một operator, một IIS worker và AI concurrency 1.
- Không chạy Verify song song upload và không bấm Verify hai lần.
- Dùng ảnh tổng hợp cho video công khai nếu không cần thiết phải hiện receipt thật.
- Video phải ghi cả timestamp/kết quả, không cắt để tạo cảm giác latency thấp hơn phép đo thật.

## Phần nói sau demo

“Wave năm người upload đồng thời hoàn tất 5 trên 5 và không mất hồ sơ. P95 của wave là 93,684 giây,
nhưng đây là concurrency gate, không phải Verify latency gate. Verify mới là yêu cầu năm ca dưới 90
giây và đã đạt 65,004 giây.

Hai lỗi over trong lượt wave giúp tách hai vấn đề. FAHASA lặp lại ổn định vì validator chưa công nhận
nhãn ‘Thanh toán’ là final-payable. GS25_2 trở lại đúng ở lượt tuần tự, cho thấy extraction variance
ở vùng header bị che. Chúng tôi không hard-code merchant và không nới validator ngay trước demo.”

## Kết luận 60 giây

“AURA không thay người duyệt. AURA giảm số hồ sơ phải đọc thủ công, biến ngoại lệ thành câu hỏi có
thể quyết định và giữ audit đầy đủ.

Candidate hiện đủ điều kiện cho supervised demo. Bước tiếp theo là pilot có người giám sát để đo
touchless rate và review time, đồng thời xây evidence overlay bằng OCR word boxes trên tập annotation
riêng. Policy hiện hành không phụ thuộc bounding box và không thay đổi khi bật hoặc tắt overlay.”

## Evidence overlay khi BGK hỏi

Trả lời ngắn:

“Prototype đã chạy bằng ảnh tổng hợp. Ảnh gốc giữ nguyên bytes, region JSON gắn SHA-256 và browser vẽ
SVG overlay. Field không có tọa độ sẽ hiện ‘không định vị được’ thay vì bịa box. Để đưa vào baseline,
chúng tôi còn cần OCR word boxes, annotation độc lập và benchmark region recall/false highlight. Vì
vậy đây là roadmap UX, chưa tham gia quyết định policy.”

Chỉ mở prototype trong Q&A nếu còn thời gian. Luôn gọi rõ đây là prototype tổng hợp, không phải tính
năng đã tích hợp production.
