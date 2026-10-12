# AURA — Checklist nộp bài

Cập nhật: 12/10/2026. Trạng thái lịch sử nằm tại `docs/CURRENT_PROJECT_STATUS_2026-10-07.md`; forensic hardening mới nhất ở `docs/REAL_RECEIPT_OVER_ESCALATION_ANALYSIS_2026-10-12.md`. Không điền số liệu hoặc URL chưa được xác minh. Hạn khóa link nộp do đội ghi nhận là 14/10/2026; kiểm link và rehearsal ngày 13/10.

## 1. Hạng mục bàn giao

| Hạng mục | Trạng thái | File/bằng chứng |
|---|---|---|
| Frontend/backend clone và chạy localhost | ☑ Có | README root, .NET 8, LocalDB/SQL Server, user-secrets và migration |
| Verify Harness + runbook | ☑ Có | `docs/RUNBOOK.md`, 5 ca live, 15/30 ca chạy ngoài UI bằng evaluator |
| Public repository và lịch sử commit | ☑ Có | `https://github.com/BondPhuPhamzZ/AURA`; cần push commit chốt sau mỗi lần cập nhật hồ sơ |
| Public Live URL | ◐ Technical baseline pass, chưa public trong README | Health/security/Verify/workflow, mobile 390 px và điện thoại thật 4G/5G đã pass; còn một rehearsal sạch và kiểm link từ thiết bị khác |
| Video demo tối đa 3 phút (BTC ghi Optional) | ☑ Đã liên kết | Link Google Drive ở README root |
| Đúng 5 slide | ☑ Đã dựng | `submission/AURA_5_SLIDES.pptx` |
| Build Log một trang Word | ☑ Đã dựng | `submission/AURA_BUILD_LOG.docx` |

## 2. File nên có trong GitHub

- Mã nguồn ASP.NET Core, migrations, views, CSS/JS và tests.
- `README.md`, `BUSINESS_RULES.md`, `ARCHITECTURE_AND_INTEGRATION_REPORT.md`.
- `docs/`: Build Log nguồn, deployment, runbook, test cases, model selection, measurement plan.
- `submission/AURA_WORKFLOW_SPEC.md`: workflow đầy đủ và state transition.
- `submission/AURA_5_SLIDES.pptx`: đúng 5 slide; ứng viên capacity-hardening hiện hành đạt 159/159 code test và Release build sạch, còn số receipt thật công khai vẫn phải giữ đúng ranh giới regression/holdout. Không biến các số fixture hoặc batch bị HTTP 429 thành production accuracy claim.
- `submission/AURA_BUILD_LOG.docx`: một trang.
- `docs/SUBMISSION_CHECKLIST.md`: manifest bàn giao nội bộ.
- 5 ảnh Verify trong `wwwroot/test_data/images`, manifest Verify và gói 15 ca BGK.
- `Dockerfile`, `.dockerignore`, `.gitignore`.

## 3. File không đưa vào GitHub

- API key, connection string production, publish profile chứa mật khẩu, `.env` hoặc user-secrets.
- `.vs/`, `bin/`, `obj/`, `publish/`, `.tmp/`, `TestResults/`, `*.user`, log và database local.
- `App_Data/receipts/*` và mọi hóa đơn cá nhân thật.
- `test_kit/local_real/*` ngoài file hướng dẫn.
- Video `.mp4/.mov/.mkv`: tải lên Google Drive hoặc YouTube Unlisted rồi điền link; repo chỉ giữ script/link để tránh phình lịch sử Git.
- Ảnh chụp dashboard billing/API key, dữ liệu cá nhân và file render QA tạm.

## 4. Đối chiếu mẫu VNG

Folder mẫu `D:\aura\VNG\artifact` là project tham khảo, không phải một archive nộp cứng. AURA đã bổ sung `ARCHITECTURE_AND_INTEGRATION_REPORT.md` tương đương tài liệu kiến trúc của mẫu, đồng thời cung cấp PPTX, DOCX và workflow mở được ngay.

## 5. Trạng thái bàn giao và việc theo dõi

1. Source, 5 ca Verify, gói 15 ca tham chiếu, video, slide và Build Log đã có đường dẫn từ README root.
2. API key đánh giá chỉ gửi riêng cho BTC; không đặt key thật trong Git, README, ảnh hoặc video.
3. Trước mỗi bản bàn giao mới: quét secret, build/test, push commit chốt và xác minh repository public.
4. Nếu có thay đổi đáng kể trong lúc chấm, tạo ticket/báo cáo tiến độ cho BTC; sửa tài liệu hoặc độ ổn định nhỏ có thể báo theo commit.
5. Baseline lặp ngày 27/09/2026 được giữ làm lịch sử: Ollama đạt 25/25 trên 5 fixture qua năm batch; OpenRouter đạt 15/15 qua ba batch và upload `HoaDon1.jpg` 3/3.
6. Judge set ngày 29/09/2026 đã chạy thật với fallback tắt: OpenRouter 15/15 quyết định, 70/75 field, P95 16,459 giây; Ollama 14/15, 73/75 field, P95 58,318 giây và bỏ sót TK-12. OpenRouter concurrency smoke đạt 5/5, P95 17,072 giây. Xem `docs/LIVE_VALIDATION_2026-09-29.md`.
   Post-policy/contract v2 ngày 06/10, Ollama 4B profile 8K đạt 13/15. Benchmark cô lập 16K ngày 12/10 đạt 15/15 decision, 69/75 field, 0 missed/over/system error nhưng processing P95 213,429 giây. Đây là fallback candidate về chất lượng; auto-fallback vẫn tắt vì latency/capacity và endpoint chưa đạt gate vận hành.
7. Official holdout 15 ảnh thật đã hoàn tất đúng một lượt; mobile 4G/5G đã pass. Phản hồi ba người dùng và rehearsal vẫn là cổng chưa hoàn tất. Không thay chúng bằng số liệu fixture tổng hợp.
8. Post-fix gate ngày 02/10 đã xác minh Phê La bị che vùng món đi `ESCALATE_FACT`, Vinamilk đủ bảy dòng hàng vẫn `AUTO_APPROVE`, Official Verify 5/5, restart giữ status/ảnh/audit, manager YES/NO/UNDO có audit và concurrent smoke 5/5 với accepted P95 530 ms, end-to-end P95 39,380 giây.
9. Hướng dẫn BGK chạy local nằm tại `docs/JUDGE_LOCAL_SETUP.md`; trạng thái và ranh giới claim hiện hành nằm tại `docs/CURRENT_PROJECT_STATUS_2026-10-12.md`; roadmap/học liệu lịch sử vẫn giữ theo ngày để audit.
10. Development validation bổ sung và fallback live regression nằm tại `docs/REAL_RECEIPT_VALIDATION_2026-10-02.md` và `docs/FALLBACK_REGRESSION_2026-10-02.md`. Ảnh thật/PII vẫn nằm ngoài Git; fallback baseline vẫn tắt.
11. Evidence live hậu publish đã đóng health, security, UI browser mobile 390 px, Verify 5/5, workflow/Audit và điện thoại thật 4G/5G. Còn một rehearsal sạch và kiểm link từ thiết bị khác trước khi public URL trong README.
12. Holdout 15 ca đã chạy một official run: raw 10/15, adjusted 11/15 do một nhãn người sai; raw bất biến. Tập ảnh từ đây chỉ là post-holdout regression, không còn blind.
13. Test Kit v3 synthetic chạy một live batch: raw 11/15, 142/145 field, 0 system error; raw bất biến. V3.1 ngày 07/10 chạy đúng một batch sau publish: 15/15 completed, 13/15 exact, 0 missed escalation, 0 system error, field UTF-8-safe 140/146; không chạy lại.
14. Test Kit v2 hậu-contract đạt 14/15, missed TK-12 blur cũ, 0 over/system error và field 69/75; không gọi là kết quả 15/15 lịch sử.
15. Post-holdout real-receipt regression ngày 07/10 chạy đúng 15 request độc lập trên OpenRouter 8B: 11/15 exact, 0 missed, 4 over, 0 system error. Đây là regression an toàn, không phải blind holdout mới; xem `docs/REAL_RECEIPT_REGRESSION_RESULT_2026-10-07.md`.
16. Checklist điện thoại thật, ba user session và rehearsal nằm tại `docs/MANUAL_FINAL_GATES_2026-10-06.md`.
17. Benchmark GPU chỉ dùng v3.1 tổng hợp; protocol chạy lại receipt thật nằm tại `docs/REAL_RECEIPT_REGRESSION_PROTOCOL_2026-10-07.md`. Runbook phiên máy mượn và secret vận hành không thuộc submission công khai.
18. Repository công khai chỉ giữ gate kỹ thuật và evidence kiểm chứng; kế hoạch học, pitch, Q&A và chiến lược onsite nằm ngoài Git.
19. Forensic hardening 12/10 tại `d0440f2`: 149/149 test và Release build sạch. API thật xác nhận BH-03/BH-14/BH-15 hết over; BH-01 còn fail-safe. Batch 15 ca có 5 `AI_RATE_LIMIT`, vì vậy không thay số 11/15 ngày 07/10 và phải chạy live post-publish gate có kiểm soát trước khi freeze.
20. Live post-policy gate 12/10 tại `2b75f3f`: health/security/upload-negative/UI và offline pass; Verify và regression tuần tự đều ghi nhận HTTP 429. Regression dừng đúng tại BH-10 sau 10/15; không công bố 8/10 như accuracy. Demo dùng một operator, không chạy Verify song song upload và có video/evidence dự phòng.
21. OpenRouter Upstream Logs xác nhận lỗi 429 đến từ provider Parasail và key vẫn còn budget/credit. Candidate capacity-hardening thêm shared AI gate, provider cooldown, dừng worker drain và sanitized diagnostics; offline đạt 159/159, Release 0 warning/error. Chưa gọi đây là live pass trước khi publish/recycle/smoke; xem `docs/PROVIDER_CAPACITY_HARDENING_2026-10-12.md`.
22. Ollama 4B digest `ee4b975b58c1` dùng context 16K/output 4K, `Think=false`, fallback tắt đã đạt 15/15 Test Kit v2 synthetic với 0 missed/over/system error và field 69/75. Evidence được phục hồi không replay qua durable queue và SHA-256; xem `docs/OLLAMA_4B_16K_RECOVERY_BENCHMARK_2026-10-12.md`. Không đổi primary/fallback production từ kết quả synthetic này.
