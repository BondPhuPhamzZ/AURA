# Kịch bản pitching và Q&A cốt lõi AURA

**Bản chốt:** 14/10/2026

**Mục tiêu:** hoàn tất phần trình bày trong khoảng 13 phút 30 giây, kể cả video dưới 3 phút, và giữ 90 giây dự phòng.

## Một câu phải nhớ

**AURA dùng AI để đọc dữ kiện, dùng policy C# tất định để quyết định hướng xử lý và giữ con người chịu trách nhiệm với ngoại lệ.**

## Phân bổ thời gian

| Thời gian | Nội dung | Điều BGK cần nhớ |
|---:|---|---|
| 0:00-1:10 | Câu chuyện mở đầu và Slide 1 | Bài toán khó là ranh giới quyết định, không chỉ OCR |
| 1:10-2:35 | Slide 2 | HTTP 202, SQL queue, hai điểm quyết định của con người |
| 2:35-4:00 | Slide 3 | Evidence được nói đúng ranh giới |
| 4:00-5:05 | Slide 4 | Model thay được, policy không đổi |
| 5:05-6:00 | Slide 5 | Fail-safe, giới hạn hiện tại |
| 6:00-9:00 | Video chính thức | Verify, escalation, quản lý, Audit và Undo |
| 9:00-11:10 | Giải thích evidence và concurrency | 8/10 prospective; 11/12 chỉ là stability; queue integrity khác SLA |
| 11:10-12:25 | User evidence hoặc kế hoạch pilot | Chỉ nói những phiên thực sự đã hoàn tất |
| 12:25-13:30 | Kết luận | Supervised MVP; bước tiếp theo là pilot có kiểm soát |
| 13:30-15:00 | Dự phòng | Chuyển màn hình hoặc xử lý sự cố |

## Câu chuyện mở đầu 45 giây

“Ban đầu em nghĩ phần khó nhất là đọc hóa đơn. Nhưng holdout hóa đơn thật đầu tiên cho thấy model có thể trả JSON hợp lệ mà vẫn gán sai ý nghĩa cho một con số hoặc mã định danh. Khi đó em nhận ra vấn đề chính không chỉ là OCR, mà là biết khi nào bằng chứng đã đủ và khi nào tự động hóa phải dừng.”

“Vì vậy em tách vision extraction khỏi approval policy, thêm semantic validation và provenance, chỉ cho repair một lần và giữ người quản lý ở ranh giới ngoại lệ. AURA không giao quyền duyệt cho AI; AURA biến hồ sơ thành dữ kiện có thể kiểm tra và câu hỏi có thể quyết định.”

## Lời thoại ngắn theo slide

### Slide 1

“Một hồ sơ hoàn ứng đi qua đọc chứng từ, đối chiếu policy, hỏi quản lý và lưu vết. AURA chia trách nhiệm rõ: Qwen đọc facts, C# policy quyết định route, con người xử lý ngoại lệ và Audit giữ timeline.”

### Slide 2

“Upload được kiểm tra và lưu PENDING vào SQL rồi server trả HTTP 202. Worker claim job bằng lease nên refresh hoặc recycle không làm mất công việc. Model trả facts, semantic validation kiểm tra, policy mới chọn AUTO hay ESCALATE. Nhân viên xác nhận chuyển tiếp; quản lý trả lời câu hỏi; Undo thêm event mới thay vì xóa lịch sử.”

### Slide 3

“Release build sạch và 163/163 test offline. Đây là test code, không phải 163 hóa đơn. Verify live đạt 5/5 trong 65,004 giây. Mini-holdout prospective 10 hóa đơn đạt 8/10, không có missed escalation, có hai fail-safe over. Em không gọi đây là production accuracy. Kết quả 11/12 về sau chỉ là stability trên known data.”

### Slide 4

“AURA là ASP.NET Core vertical slice. SQL giữ request, job, result và audit. OpenRouter route Qwen3.8 Flash tới Alibaba để extraction. Policy engine không phụ thuộc model nên đổi model không tự làm đổi luật duyệt.”

### Slide 5

“Timeout, 429 hoặc semantic failure không thể biến thành approval. Fallback chỉ chạy khi bật và lỗi thuộc allowlist hạ tầng. Baseline hiện tại chưa có production RBAC, PDF nhiều trang, object storage hay distributed provider limiter. Evidence localization mới là prototype cô lập.”

## Ba số liệu bắt buộc nói đúng

- `163/163`: test offline của code, không phải số hóa đơn.
- `8/10`: mini-holdout prospective, 0 missed, 2 fail-safe over; mẫu nhỏ, chưa phải production accuracy.
- `5/5 trong 65,004 giây`: Verify live đạt yêu cầu 5 ca dưới 90 giây.

Nếu cần nói concurrency: năm upload khác nhau hoàn tất 5/5 với E2E P95 93,684 giây ở AI concurrency 1. Đây là queue-integrity evidence, không phải production SLA cho năm người.

## Mười hai câu Q&A ưu tiên

### 1. Vì sao cần AI nếu C# policy mới quyết định

Layout hóa đơn thay đổi nhiều. Vision model xử lý perception để tạo facts và evidence; C# áp rule ổn định, test được. AI đọc, code quyết định route, con người giữ authority.

### 2. Vì sao không để model approve trực tiếp

Output đúng schema vẫn có thể sai nghĩa. Nếu thành phần có thể đọc sai cũng có quyền duyệt thì hệ thống mất lớp kiểm soát quan trọng nhất.

### 3. Vì sao FACT ưu tiên POLICY và AUTHORITY

Policy và authority đều phụ thuộc dữ kiện đáng tin. Khi total, date, merchant hoặc identifier chưa chắc, AURA phải xác lập facts trước.

### 4. Vì sao 8/10 vẫn có ý nghĩa

Kết quả chưa đủ cho production không giám sát. Nó có giá trị cho supervised MVP vì không có missed escalation hoặc system error; hai mismatch đều fail-safe và được công bố như chi phí vận hành.

### 5. Có chạy lại holdout để lấy số đẹp không

Không. Tập 10 ca được khóa nhãn và hash trước request đầu, rồi chạy một lần. Các lượt trên bytes đã biết được ghi là regression hoặc stability, không thay số prospective.

### 6. Vì sao không vá FAHASA ngay

Mở rộng keyword `Thanh toán` có thể nhầm payment method, tiền khách đưa hoặc tiền thối thành tổng cuối. Thay đổi an toàn cần fixture dương và âm độc lập. Giữ một over minh bạch an toàn hơn tạo fail-open.

### 7. HTTP 202 và polling nghĩa là gì

HTTP 202 cho biết hồ sơ đã được kiểm tra đầu vào và lưu bền vững, chưa có nghĩa AI đã xử lý xong. UI poll `statusUrl` để hiện PENDING/PROCESSING rồi cập nhật kết quả cuối; request upload không phải mở chờ AI.

### 8. Refresh hoặc recycle có làm mất job không

Signal RAM có thể mất nhưng row PENDING vẫn ở SQL. Worker có thể đọc lại hoặc reclaim khi lease hết hạn.

### 9. Năm giám khảo upload cùng lúc có được không

Năm upload khác nhau được nhận vào SQL và đã hoàn tất không mất job. AI concurrency 1 khiến request sau chờ lâu hơn. Kết quả này chứng minh queue integrity trên một process, chưa chứng minh multi-instance production capacity.

### 10. Vì sao Verify thứ hai nhận 409

Verify là benchmark độc quyền. Gate trong process chỉ cho một batch thật chạy, nên nhiều click không biến 5 ca thành 25 AI call.

### 11. Khi provider trả 429 hoặc timeout thì sao

Shared permit và cooldown giảm request amplification. Khi live fallback tắt, hồ sơ dừng bằng system escalation thay vì tạo business decision giả.

### 12. Evidence overlay đã là production explainability chưa

Chưa. Prototype mới chứng minh interaction và contract region gắn với hash ảnh. Production còn cần word-level OCR, annotation độc lập, unseen set, region recall và false-highlight benchmark.

## Câu hỏi dự phòng một dòng

- **Claimed amount có vào prompt không?** Không; tránh anchoring. Backend so sánh sau khi model đọc độc lập.
- **Audit có bất biến tuyệt đối không?** Ứng dụng append event và Undo không xóa lịch sử; production vẫn cần quyền append hạn chế, retention và WORM/hash chain.
- **SHA-256 có chứng minh hóa đơn thật không?** Không; hash chỉ chứng minh byte identity trong package.
- **Ollama có phải live fallback không?** Không; là contingency local có giám sát và latency cao. SmarterASP không gọi được loopback trên laptop.
- **Thêm PDF có đơn giản không?** Không; cần signature check, page limit, malware scan, rasterization cô lập, page provenance, retention và benchmark mới.
- **Rủi ro lớn nhất còn lại?** Ba phiên user evidence và giới hạn capacity một process; chỉ claim sau khi có bằng chứng thật.

## Kết luận 25 giây

“AURA không cố thay người phê duyệt. Hệ thống biến hóa đơn thành facts có thể truy vết, dùng policy tất định để route, dừng khi không chắc chắn và giữ quyết định con người trong Audit. Evidence hiện tại đủ cho supervised demo. Bước tiếp theo là pilot có kiểm soát cùng ba người dùng thật, không phải rollout production không giám sát.”
