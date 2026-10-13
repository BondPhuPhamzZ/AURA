# Kịch bản thuyết trình và phản biện AURA

**Bản chốt:** 14/10/2026

**Mục tiêu:** trình bày trong 13 phút 30 giây, chừa 90 giây cho chuyển màn hình hoặc gián đoạn ngắn.

## Phân bổ thời gian

| Thời gian | Nội dung | Kết quả cần đạt |
|---:|---|---|
| 0:00-1:15 | Slide 1 và câu chuyện sản phẩm | Nêu đúng bài toán là ranh giới quyết định, không chỉ OCR |
| 1:15-2:45 | Slide 2 | Giải thích workflow và hai quyết định của con người |
| 2:45-4:25 | Slide 3 | Trình bày evidence trung thực và cách đo |
| 4:25-5:35 | Slide 4 | Kiến trúc thật, dữ liệu mô phỏng được công bố |
| 5:35-6:35 | Slide 5 | Failure behavior, giới hạn và roadmap |
| 6:35-9:33 | Video demo hoặc chuỗi live tương đương | Verify, escalation, quản lý, Audit và Undo |
| 9:33-11:20 | Diễn giải evidence | Over-escalation, concurrency và claim boundary |
| 11:20-12:30 | User evidence | Chỉ trình bày phiên đã thực sự hoàn tất |
| 12:30-13:30 | Kết luận | Go/no-go hiện tại và bước pilot có kiểm soát |
| 13:30-15:00 | Buffer | Chuyển màn hình hoặc tương tác ngắn với BGK |

## Câu chuyện mở đầu

“Khi bắt đầu AURA, em nghĩ phần khó nhất là đọc hóa đơn. Lượt holdout hóa đơn thật đầu tiên đã thay đổi cách em nhìn bài toán: model có thể trả JSON hợp lệ nhưng vẫn gán sai ý nghĩa cho một con số hoặc mã định danh. Vấn đề thật sự là biết khi nào bằng chứng đã đủ và khi nào tự động hóa phải dừng.”

“Từ thất bại đó, em tách vision extraction khỏi approval policy, bổ sung provenance và semantic validation, giới hạn repair còn một lần và giữ con người ở ranh giới ngoại lệ. AURA vì vậy không chỉ trình diễn một output AI đẹp; nó là một workflow có trách nhiệm và có thể kiểm toán.”

Không bổ sung câu chuyện doanh nghiệp hoặc lời người dùng chưa có biên bản thật.

## Lời thoại theo slide

### Slide 1

“Một hồ sơ hoàn ứng phải đi qua đọc hóa đơn, đối chiếu policy, hỏi quản lý và lưu vết. Nút thắt không chỉ là OCR, mà là thiếu một nguyên tắc nhất quán để xác định khi nào evidence đã đủ.”

“AURA chia trách nhiệm hẹp: Qwen đọc dữ kiện; policy C# tất định quyết định route; con người xử lý ngoại lệ; hệ thống giữ timeline.”

### Slide 2

“Upload kiểm file, lưu request PENDING và trả HTTP 202. Worker dựa trên SQL claim job bằng lease nên refresh hoặc recycle không xóa công việc. Model trả facts có cấu trúc, semantic validation có thể yêu cầu một repair, rồi policy tạo route cuối.”

“Quyết định người thứ nhất là nhân viên xác nhận chuyển tiếp. Quyết định thứ hai là quản lý trả lời câu hỏi cụ thể. Undo tạo event mới thay vì xóa lịch sử.”

### Slide 3

“Candidate hiện tại có Release build sạch và 163 trên 163 test offline. Đây là test code, không phải 163 hóa đơn. Verify Harness live đạt 5 trên 5 trong 65,004 giây.”

“Mini-holdout prospective 10 hóa đơn đạt 8 trên 10 quyết định chính xác, 0 missed escalation, 2 fail-safe over-escalation và không quan sát system/provider/429 error. Em không gọi đây là production accuracy. Lần lặp sau trên dữ liệu đã biết đạt 11 trên 12 và chỉ được ghi là stability evidence.”

### Slide 4

“AURA là ASP.NET Core vertical slice. SQL lưu request, job, result và audit. OpenRouter route Qwen3.8 Flash tới Alibaba để extraction. Policy engine không phụ thuộc model.”

“Năm ảnh Verify và test kit mở rộng là dữ liệu synthetic được công bố. Upload, provider call, validation, policy, review workflow và audit vẫn chạy qua application thật.”

### Slide 5

“Provider timeout hoặc 429 không bao giờ trở thành approval. Fallback chỉ chạy khi được bật và lỗi thuộc nhóm hạ tầng cho phép. Schema hoặc semantic failure chỉ được repair một lần, sau đó dừng ở FACT. Live host giữ fallback tắt.”

“Giới hạn hiện tại gồm một JPG/PNG, chưa có production RBAC, safe multipage PDF, managed object storage hoặc distributed provider limiter. Evidence localization vẫn là prototype cô lập.”

## Diễn giải sau demo

“Năm upload khác nhau cùng lúc hoàn tất 5 trên 5, không mất request. E2E P95 là 93,684 giây vì một AI permit tuần tự hóa inference. Đây là gate queue integrity, không phải Verify latency gate.”

“FAHASA là over-escalation còn ổn định. Model đọc đúng field trọng yếu nhưng validator chưa chấp nhận chắc chắn nhãn ‘Thanh toán’ là final-payable evidence. Nếu thêm keyword rộng, hệ thống có thể nhầm payment-method hoặc tiền khách đưa thành tổng cuối. Vì vậy em đóng băng candidate và chỉ thay guard sau khi có fixture dương và âm độc lập.”

## Câu hỏi phản biện cốt lõi

### Vì sao cần AI nếu C# policy mới quyết định

Layout và cách in trên hóa đơn biến thiên. Vision model chuyển input thị giác thành facts và evidence; C# áp dụng rule ổn định, test được. AI xử lý perception, code và con người giữ authority.

### Vì sao không để model approve trực tiếp

Schema-valid vẫn có thể sai nghĩa. AURA kiểm role của identifier, evidence, money và date bằng lớp độc lập. Trao quyền approve cho chính thành phần có thể đọc sai sẽ loại bỏ kiểm soát quan trọng nhất.

### Vì sao FACT ưu tiên hơn POLICY hoặc AUTHORITY

Kết luận policy và authority phụ thuộc facts đáng tin. Khi total, date, merchant hoặc identifier chưa chắc chắn, AURA hỏi con người xác lập facts trước.

### 163 test có phải 163 hóa đơn không

Không. Đó là unit/integration test offline cho policy, contract, queue, fallback, audit và UI contract. Receipt accuracy được báo riêng trên dataset đã khóa.

### Vì sao 8 trên 10 vẫn có ý nghĩa

Chưa đủ cho production không giám sát. Nó có giá trị cho supervised MVP vì không có missed escalation hoặc system error và hai mismatch đều fail-safe. Hai over vẫn là chi phí vận hành phải giảm bằng dữ liệu unseen.

### Có chạy lại holdout để săn số đẹp không

Không. Package 10 ca được khóa nhãn và hash trước request đầu rồi chạy một lần. Các lượt sau trên bytes đã biết được ghi rõ là regression hoặc stability và không thay kết quả prospective.

### Vì sao không vá FAHASA trước chung kết

Keyword patch đơn giản có thể nhầm tender, tiền khách đưa hoặc tiền thối thành final total. Thay đổi an toàn cần fixture final-payable dương và fixture tender/change/subtotal âm. Giữ một over minh bạch an toàn hơn tạo fail-open.

### Khi OpenRouter trả 429 thì sao

Lỗi được phân loại là hạ tầng. Shared AI permit và cooldown ngăn request amplification. Khi live fallback tắt, request dừng bằng system escalation, không bịa business decision.

### Năm giám khảo upload cùng lúc có được không

Năm upload khác nhau được nhận vào SQL và hoàn tất không mất job. Inference chạy concurrency một nên request sau phải chờ. Kết quả chứng minh queue integrity trên một process, chưa phải production SLA hoặc multi-instance capacity.

### Vì sao Verify thứ hai nhận 409

Verify là benchmark operation độc quyền. Gate trong process cho một batch thật chạy và từ chối click trùng để năm giám khảo không nhân năm ca thành 25 AI call.

### SemaphoreSlim còn là queue chính không

Không. SQL row là nguồn job bền vững; bounded channel chỉ đánh thức worker. Gate trong process vẫn dùng để giới hạn Verify và provider capacity, nhưng khi scale nhiều process phải có distributed limiter.

### Ollama có sẵn sàng làm live fallback không

Ollama là contingency local có giám sát. Profile 4B khóa đã pass gate quality synthetic nhưng latency cao; SmarterASP không thể gọi loopback laptop. Vì vậy public live fallback vẫn tắt.

### HTTP 202 nghĩa là gì

Server đã validate và persist request, chưa hoàn tất AI processing. Client poll status URL trong lúc worker xử lý SQL job.

### Refresh hoặc recycle có làm mất claim không

Signal RAM có thể mất nhưng PENDING row vẫn còn. Lease hết hạn và worker cycle sau có thể reclaim. Đây là phần đã có test và persistence check.

### Audit có bất biến không

Application append event và Undo không xóa lịch sử, nhưng chưa phải immutable compliance store. Production cần restricted append permission, retention bên ngoài và WORM hoặc hash chain.

### SHA-256 có chứng minh hóa đơn thật không

Không. SHA-256 chứng minh byte identity trong evidence package, không xác minh nhà phát hành và không nhận diện nội dung sau resize hoặc chụp lại.

### Vì sao không gửi claimed amount vào prompt

Để tránh model bị neo theo câu trả lời của nhân viên. Model đọc tổng in độc lập, backend so sánh hai giá trị sau.

### Có thể hỗ trợ PDF chỉ bằng cách thêm extension không

Không. Cần MIME/signature check, page/size limit, malware scan, sandboxed rasterization, page provenance, aggregation rule, retention và benchmark mới.

### Evidence overlay đã là production explainability chưa

Chưa. Prototype mới chứng minh interaction design và hash-bound region contract. Nó còn thiếu word-level OCR, independent annotation, unseen set, region recall và false-highlight measurement.

### Tính năng lớn nhất đã cắt là gì

Production evidence localization. Nếu highlight nhầm amount, reviewer có thể tự tin sai. Prototype được giữ cô lập cho tới khi có evaluation gate riêng.

### Rủi ro nộp bài lớn nhất hiện tại là gì

Ba phiên user evidence. Technical candidate và artifact đã sẵn sàng, nhưng track yêu cầu danh tính/vai trò thật, feedback nguyên văn, một thay đổi truy vết được và một tác động tiêu cực. Chỉ được claim sau khi thu thật.

## Kết luận

“AURA không cố thay người phê duyệt. Nó biến hóa đơn thành facts có thể truy vết, dùng policy tất định để route, dừng khi không chắc chắn và giữ quyết định con người trong audit. Evidence hiện tại đủ cho supervised demo; bước tiếp theo là pilot có kiểm soát, không phải vận hành production không giám sát.”
