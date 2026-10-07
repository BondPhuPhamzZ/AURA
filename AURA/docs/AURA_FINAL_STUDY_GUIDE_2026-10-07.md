# AURA — Cẩm nang ôn source code, nghiệp vụ và Hackathon

Cập nhật: 07/10/2026. Đây là tài liệu bắt đầu cho tác giả dự án; đọc theo thứ tự và thực hành
teach-back, không học thuộc từng dòng code.

## 1. Câu mô tả phải nhớ

**AURA là trợ lý thẩm định hoàn ứng: VLM chỉ đọc dữ kiện có cấu trúc, policy C# quyết định theo
`FACT → POLICY → AUTHORITY`, con người xử lý ngoại lệ và mọi bước được audit.**

Không gọi AURA là OCR tự duyệt tiền hoặc hệ thống production tự động hoàn toàn.

## 2. Bốn lớp trách nhiệm

1. **Intake:** kiểm extension/MIME/magic bytes/kích thước, lưu file riêng tư và hồ sơ PENDING.
2. **AI extraction:** OpenRouter/Ollama đọc ảnh thành JSON theo evidence contract.
3. **Semantic + policy:** validator tìm mâu thuẫn, repair tối đa một lần; policy C# ra quyết định.
4. **Human workflow:** nhân viên chuyển tiếp, quản lý YES/NO, Undo và Audit.

Điểm bảo vệ quan trọng: model không có authority quyết định hoàn ứng.

## 3. Luồng một request từng bước

1. UI gửi một ảnh + claimed amount + metadata nhân viên.
2. `ApplicantController` kiểm file, lưu receipt và DB record, trả HTTP 202.
3. SQL giữ job thật; in-memory signal chỉ đánh thức worker.
4. `ReceiptProcessingWorker` claim bằng lease để sống qua refresh/restart.
5. Vision adapter gửi nguyên byte ảnh; ứng dụng không resize/crop/nén.
6. Contract yêu cầu facts, raw date, printed-total source/evidence và identifier có vai trò rõ.
7. `ReceiptSemanticValidator` tìm arithmetic, money-role, duplicate identifier, date/provenance lỗi.
8. Nếu cần, model repair đúng một lần; lỗi còn lại trở thành validation issue.
9. `PolicyDecisionEngine` áp dụng `FACT → POLICY → AUTHORITY`.
10. Kết quả/facts/provider/audit lưu DB; UI poll status rồi render.
11. Escalation được forward; manager quyết định và có thể Undo.

Hãy tự kể luồng này trong 60–90 giây mà không nhìn tài liệu.

## 4. Nghiệp vụ và trạng thái

| Trạng thái | Khi nào | Hành động tiếp theo |
|---|---|---|
| AUTO_APPROVE | Facts đủ, claim khớp, không cấm, không vượt quyền | Hồ sơ đi tiếp tự động trong demo |
| ESCALATE_FACT | Thiếu/mâu thuẫn evidence, ngày cuối tuần/quá hạn, amount/id không đáng tin | Con người kiểm ảnh/facts |
| ESCALATE_POLICY | Có nhóm chi bị cấm như bia, đồ cá nhân, vé xem phim | Quản lý xử lý ngoại lệ policy |
| ESCALATE_AUTHORITY | Facts hợp lệ nhưng tổng vượt 1.000.000 VND | Người có thẩm quyền duyệt |
| ESCALATE_SYSTEM_ERROR | Provider/hạ tầng/contract không xử lý được | Không giả quyết định nghiệp vụ; chẩn đoán hệ thống |

Vì FACT ưu tiên cao nhất, một receipt vừa có bia vừa thiếu evidence sẽ hiển thị FACT. Findings
policy vẫn nên được audit/hiển thị thứ cấp khi có thể.

## 5. Claimed amount, extracted total và evidence

- `claimedAmount`: số người nộp khai; không đưa vào prompt OCR để tránh model đọc theo đáp án.
- `totalAmount`: số model trích xuất.
- `totalAmountSource`: phải là `PRINTED_FINAL_TOTAL` để đi tiếp.
- `totalAmountEvidence`: dòng ảnh chứa đúng số total.
- Policy so claimed với extracted sau extraction; lệch hoặc provenance không đủ → FACT.

BH-07 chứng minh lý do: model từng suy 21.000 từ subtotal khi dòng total bị rách. Contract v2
hiện chặn fail-open đó.

## 6. Bản đồ source code

| Câu hỏi | File bắt đầu |
|---|---|
| DI, config, health | `Program.cs`, `Options/` |
| Upload/status/receipt | `Controllers/ApplicantController.cs` |
| Verify 5 ca | `Controllers/VerifyController.cs` |
| OpenRouter/Ollama | `Services/OpenRouterVisionExtractorService.cs`, `OllamaVisionExtractorService.cs` |
| Prompt/schema/parse | `Services/ReceiptExtractionContract.cs` |
| Mâu thuẫn/repair | `Services/ReceiptSemanticValidator.cs` |
| Rule quyết định | `Services/PolicyDecisionEngine.cs`, `BUSINESS_RULES.md` |
| Queue/restart | `ReceiptProcessingQueue.cs`, `ReceiptProcessingWorker.cs`, backoff |
| Human decision | `Services/EscalationWorkflow.cs`, reviewer controller |
| Audit | `AuditLogger.cs`, `AuditTrailProjector.cs`, Audit ViewComponent |
| UI/responsive | `Views/Home/Index.cshtml`, `wwwroot/css/site.css`, JavaScript liên quan |
| Tests | `tests/AURA.Tests/` |

Phương pháp học: với mỗi thay đổi, trả lời “vì sao đổi – sửa ở đâu – test nào chứng minh”.

## 7. Queue, persistence và concurrency

- Upload trả 202 vì AI chạy nền.
- SQL là durable queue; signal memory có thể mất mà job không mất.
- Worker dùng lease/reclaim cho app restart.
- RowVersion chống ghi đè cùng hồ sơ.
- Refresh chỉ mất pointer UI trong một số tình huống, không xóa record DB.
- Concurrent smoke 2/5 request là smoke demo, không phải production load test.

## 8. Provider và fallback

- Live primary: OpenRouter/Qwen3-VL-8B.
- Fallback live: tắt.
- `configuredFallbackProvider=Ollama` chỉ là cấu hình tên; không có nghĩa SmarterASP đang chạy Ollama.
- Fallback chỉ cho lỗi hạ tầng allowlist, không cho lỗi schema/semantic/data.
- Ollama 4B chưa đạt safety gate; GPU BTC sẽ đo 8B bằng v3.1 tổng hợp.

## 9. Evidence hiện hành phải nói đúng

- Code tests: 142/142 offline; không phải 142 hóa đơn.
- Verify: 5/5 regression fixture.
- Official real holdout raw: 10/15; adjudicated 11/15; raw không đổi.
- Real-receipt regression hiện tại: 11/15, 0 missed, 4 over, 0 system error.
- V3 raw: 11/15, 142/145 field, 0 system error.
- V3.1: 13/15 exact, 0 missed/system error, field 140/146.
- V2 hậu-contract: 14/15, missed TK-12 blur cũ, field 69/75.

Kết luận đúng: hệ thống hiện fail-safe tốt hơn nhưng còn over-escalation và chưa có production
accuracy claim.

## 10. Test nào chứng minh điều gì

- Unit/integration code tests: policy, contract, validator, queue, fallback, Audit, UI contract.
- Verify 5: demo regression nhanh.
- Synthetic v3.1: coverage tất định và safety regression.
- Real holdout: khả năng tổng quát trên dữ liệu unseen tại thời điểm lượt đầu.
- Post-holdout regression: before/after trên cùng ảnh, không còn blind.
- User session: usability, không phải model accuracy.
- Rehearsal: độ ổn định demo, không phải production load.

## 11. Giới hạn phải công bố

Chưa có auth/RBAC production, PDF/multi-page, antivirus, object storage/retention, tax lookup,
rate limiting/monitoring production, immutable audit ở DB, multi-instance benchmark hoặc dataset
lớn. Live URL chỉ nên ghi supervised MVP/demo.

## 12. Cách ôn mỗi ngày

Mỗi buổi 45–60 phút:

1. 10 phút: kể business flow và năm trạng thái.
2. 15 phút: trace một request qua code bằng IDE search.
3. 10 phút: trả lời ba Judge Q&A không nhìn tài liệu.
4. 15 phút: mini change drill hoặc demo flow.
5. 5 phút: ghi ba điểm còn vấp và ôn lại hôm sau.

Tỷ lệ tổng: 60% nghiệp vụ/evidence/pitch, 40% code navigation/test.

## 13. Change drill 60–90 phút

1. Chép yêu cầu nguyên văn.
2. Chia Must/Should/Cut.
3. Viết 3–5 acceptance criteria đo được.
4. Chỉ ra authority/lớp code bị tác động.
5. Viết test fail trước hoặc fixture rõ expected.
6. Làm vertical slice nhỏ nhất.
7. Chạy targeted test rồi full suite.
8. Lưu commit/health/screenshot/output.
9. Giải thích trong hai phút: yêu cầu, trade-off, evidence, phần cắt.

Drill gợi ý: thêm policy term; thêm field có provenance; export Audit; input PDF một trang có
giới hạn an toàn; thêm role demo nhưng không claim RBAC production.

## 14. Quy trình bốn giờ onsite

- 0–20 phút: hiểu đề, hỏi lại, Must/Should/Cut và acceptance criteria.
- 20–40 phút: checkpoint Git, chọn lớp, test/fixture fail đầu tiên.
- 40–120 phút: vertical slice core.
- 120–160 phút: UI + validation + error path.
- 160–195 phút: targeted/full test, build/EF, negative/recovery case.
- 195–215 phút: smoke/evidence.
- 215–240 phút: freeze và rehearsal; không polish phút cuối.

## 15. Demo recovery

- Live chậm quá 30–45 giây: chuyển localhost/video/screenshot đã chuẩn bị, không debug hosting trước BGK.
- Provider lỗi: nói đúng error/fail-safe; không bật fallback tùy tiện.
- UI lỗi: trình bày architecture/evidence và dùng recording ngắn.
- Không mở dashboard chứa secret, environment variables, billing hay receipt thật.
- Chuẩn bị laptop, sạc, hotspot, adapter, slide/PDF offline và commit đã restore package.

## 16. Checklist trước hạn 14/10

1. Audit/cập nhật README, 5 slide, Build Log và workflow spec theo số hiện hành.
2. Một rehearsal sạch; kiểm repo/video/live link bằng thiết bị khác.
3. Test 142/142, EF clean, health, Verify 5/5, workflow/Audit smoke.
4. Chốt commit và nộp trước hạn; giữ ảnh receipt thật ngoài Git.
5. Sau khi nộp chỉ luyện onsite/change drill hoặc sửa blocker được BTC cho phép.

## 17. Teach-back tự kiểm tra

Phải trả lời trôi chảy:

1. Vì sao AI không được duyệt tiền?
2. Vì sao FACT đứng trước POLICY/AUTHORITY?
3. 202 + worker + SQL lease giải quyết vấn đề gì?
4. Vì sao claimed amount không đi vào OCR prompt?
5. Contract v2 sửa BH-07 ra sao?
6. Vì sao 11/15 regression hiện tại vẫn là cải thiện safety?
7. Vì sao 142 tests không phải accuracy?
8. Khi nào fallback được phép chạy?
9. Nếu đề yêu cầu PDF, minimum safe scope là gì?
10. Nếu chỉ còn 30 phút, phần nào phải cắt?

## 18. Answer key khi cần đối chiếu

Sau khi tự trả lời, đối chiếu với `AURA_FINAL_DEFENSE_SOURCE_CODE_QA_2026-10-07.md`. File đó
ghi theo từng module: trách nhiệm, happy/failure path, fail-safe, test/evidence, giới hạn và mẫu
trả lời BGK. Không đọc answer key trước khi tự teach-back vì mục tiêu là luyện khả năng lập luận.
