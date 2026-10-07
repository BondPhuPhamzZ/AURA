# AURA — Checklist nộp bài

Cập nhật: 07/10/2026. Trạng thái chuẩn nằm tại `docs/CURRENT_PROJECT_STATUS_2026-10-07.md`; không điền số liệu hoặc URL chưa được xác minh. Hạn khóa link nộp do đội ghi nhận là 14/10/2026; mục tiêu nội bộ là chốt hồ sơ ngày 12/10 và kiểm link ngày 13/10.

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
- `submission/AURA_5_SLIDES.pptx`: đúng 5 slide nhưng phải audit/cập nhật số hiện hành trước 12/10: 142/142 code test; v3.1 13/15 exact, 0 missed/system error; v2 hậu-contract 14/15 với missed TK-12. Không biến các số này thành production accuracy claim.
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
   Post-policy/contract v2 ngày 06/10, Ollama 4B đạt 13/15, 56/75 field, P95 136,758 giây; vẫn missed TK-12 và thêm over TK-02. Đây là evidence mới nhất cho 4B và tiếp tục chặn auto-fallback mặc định.
7. Official holdout 15 ảnh thật đã hoàn tất đúng một lượt; mobile 4G/5G đã pass. Phản hồi ba người dùng và rehearsal vẫn là cổng chưa hoàn tất. Không thay chúng bằng số liệu fixture tổng hợp.
8. Post-fix gate ngày 02/10 đã xác minh Phê La bị che vùng món đi `ESCALATE_FACT`, Vinamilk đủ bảy dòng hàng vẫn `AUTO_APPROVE`, Official Verify 5/5, restart giữ status/ảnh/audit, manager YES/NO/UNDO có audit và concurrent smoke 5/5 với accepted P95 530 ms, end-to-end P95 39,380 giây.
9. Hướng dẫn BGK chạy local nằm tại `docs/JUDGE_LOCAL_SETUP.md`; báo cáo tiến độ, ranh giới claim và roadmap hiện hành nằm tại `docs/PROGRESS_REPORT_2026-10-07.md`, `docs/CURRENT_PROJECT_STATUS_2026-10-07.md` và `docs/NEXT_IMPLEMENTATION_ROADMAP_2026-10-07.md`.
10. Development validation bổ sung và fallback live regression nằm tại `docs/REAL_RECEIPT_VALIDATION_2026-10-02.md` và `docs/FALLBACK_REGRESSION_2026-10-02.md`. Ảnh thật/PII vẫn nằm ngoài Git; fallback baseline vẫn tắt.
11. Evidence live hậu publish đã đóng health, security, UI browser mobile 390 px, Verify 5/5, workflow/Audit và điện thoại thật 4G/5G. Còn một rehearsal sạch và kiểm link từ thiết bị khác trước khi public URL trong README.
12. Holdout 15 ca đã chạy một official run: raw 10/15, adjusted 11/15 do một nhãn người sai; raw bất biến. Tập ảnh từ đây chỉ là post-holdout regression, không còn blind.
13. Test Kit v3 synthetic chạy một live batch: raw 11/15, 142/145 field, 0 system error; raw bất biến. V3.1 ngày 07/10 chạy đúng một batch sau publish: 15/15 completed, 13/15 exact, 0 missed escalation, 0 system error, field UTF-8-safe 140/146; không chạy lại.
14. Test Kit v2 hậu-contract đạt 14/15, missed TK-12 blur cũ, 0 over/system error và field 69/75; không gọi là kết quả 15/15 lịch sử.
15. Checklist điện thoại thật, ba user session và rehearsal nằm tại `docs/MANUAL_FINAL_GATES_2026-10-06.md`.
16. Runbook GPU dùng đúng v3.1 tổng hợp nằm tại `docs/BTC_GPU_8B_RUNBOOK_2026-10-06.md`; protocol chạy lại receipt thật nằm tại `docs/REAL_RECEIPT_REGRESSION_PROTOCOL_2026-10-07.md`.
17. Kế hoạch nhận yêu cầu mới, hạn nộp 14/10, timebox bốn giờ và demo vòng bảng/Chung kết nằm tại `docs/ONSITE_FINAL_PLAYBOOK_2026-10-06.md`.
