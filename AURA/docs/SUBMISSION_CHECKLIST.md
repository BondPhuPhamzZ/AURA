# AURA — Checklist nộp bài

Cập nhật: 05/10/2026. Trạng thái chuẩn nằm tại `docs/CURRENT_PROJECT_STATUS_2026-10-05.md`; không điền số liệu hoặc URL chưa được xác minh. Audit live chi tiết nằm tại `docs/LIVE_DEPLOYMENT_EVIDENCE_AUDIT_2026-10-04.md`.

## 1. Hạng mục bàn giao

| Hạng mục | Trạng thái | File/bằng chứng |
|---|---|---|
| Frontend/backend clone và chạy localhost | ☑ Có | README root, .NET 8, LocalDB/SQL Server, user-secrets và migration |
| Verify Harness + runbook | ☑ Có | `docs/RUNBOOK.md`, 5 ca live, 15/30 ca chạy ngoài UI bằng evaluator |
| Public repository và lịch sử commit | ☑ Có | `https://github.com/BondPhuPhamzZ/AURA`; cần push commit chốt sau mỗi lần cập nhật hồ sơ |
| Public Live URL | ◐ Technical pass, chưa public | Live `c42c5ef` pass core gates; responsive `e94af11` pass local, còn publish/recycle + điện thoại thật 4G/no-flash, holdout, users và rehearsal |
| Video demo tối đa 3 phút (BTC ghi Optional) | ☑ Đã liên kết | Link Google Drive ở README root |
| Đúng 5 slide | ☑ Đã dựng | `submission/AURA_5_SLIDES.pptx` |
| Build Log một trang Word | ☑ Đã dựng | `submission/AURA_BUILD_LOG.docx` |

## 2. File nên có trong GitHub

- Mã nguồn ASP.NET Core, migrations, views, CSS/JS và tests.
- `README.md`, `BUSINESS_RULES.md`, `ARCHITECTURE_AND_INTEGRATION_REPORT.md`.
- `docs/`: Build Log nguồn, deployment, runbook, test cases, model selection, measurement plan.
- `submission/AURA_WORKFLOW_SPEC.md`: workflow đầy đủ và state transition.
- `submission/AURA_5_SLIDES.pptx`: đúng 5 slide; phải refresh mốc test/evidence một lần sau feature freeze (candidate hiện tại 108/108).
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
7. Benchmark dữ liệu thật độc lập 10–15 ảnh, phản hồi ba người dùng và smoke Live URL sau hardening vẫn là các cổng chưa hoàn tất; không thay chúng bằng số liệu fixture tổng hợp.
8. Post-fix gate ngày 02/10 đã xác minh Phê La bị che vùng món đi `ESCALATE_FACT`, Vinamilk đủ bảy dòng hàng vẫn `AUTO_APPROVE`, Official Verify 5/5, restart giữ status/ảnh/audit, manager YES/NO/UNDO có audit và concurrent smoke 5/5 với accepted P95 530 ms, end-to-end P95 39,380 giây.
9. Hướng dẫn BGK chạy local nằm tại `docs/JUDGE_LOCAL_SETUP.md`; báo cáo tiến độ và ranh giới claim hiện hành nằm tại `docs/PROGRESS_REPORT_2026-10-05.md` và `docs/CURRENT_PROJECT_STATUS_2026-10-05.md`.
10. Development validation bổ sung và fallback live regression nằm tại `docs/REAL_RECEIPT_VALIDATION_2026-10-02.md` và `docs/FALLBACK_REGRESSION_2026-10-02.md`. Ảnh thật/PII vẫn nằm ngoài Git; fallback baseline vẫn tắt.
11. Evidence postfix ngày 04/10 trên `c42c5ef` đã đóng initial-tab, AUTO/FACT độc lập, Verify, workflow, persistence và concurrency. Responsive `e94af11` đã pass browser local 360/390/1440; manual phone/4G/no-flash sau publish còn cần xác nhận.
12. Holdout folder hiện có 12 candidate nhưng inventory cũ không còn khớp. `NewVinamilk.jpg`/`NewKatinat.jpg` được giữ nếu là giao dịch mới chưa từng gửi model; trùng merchant không tự làm mất tính blind. Tạo lại inventory và chỉ khóa manifest khi đủ đúng 15 ảnh eligible theo 5 AUTO/4 FACT/3 POLICY/3 AUTHORITY, consent/redaction/ground truth và SHA-256.
