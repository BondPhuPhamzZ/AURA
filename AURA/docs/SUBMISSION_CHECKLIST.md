# AURA — Checklist nộp bài

Cập nhật: 30/09/2026. Trạng thái này là nguồn đối chiếu cuối; không điền số liệu hoặc URL chưa được xác minh.

## 1. Hạng mục bàn giao

| Hạng mục | Trạng thái | File/bằng chứng |
|---|---|---|
| Frontend/backend clone và chạy localhost | ☑ Có | README root, .NET 8, LocalDB/SQL Server, user-secrets và migration |
| Verify Harness + runbook | ☑ Có | `docs/RUNBOOK.md`, 5 ca live, 15/30 ca chạy ngoài UI bằng evaluator |
| Public repository và lịch sử commit | ☑ Có | `https://github.com/BondPhuPhamzZ/AURA`; cần push commit chốt sau mỗi lần cập nhật hồ sơ |
| Public Live URL | ☐ Cần smoke lại | URL Sprint 1 đã có; phải xác minh bản hardening, migration, storage sau recycle và một upload thật trước khi điền trạng thái hoàn tất |
| Video demo tối đa 3 phút (BTC ghi Optional) | ☑ Đã liên kết | Link Google Drive ở README root |
| Đúng 5 slide | ☑ Đã dựng | `submission/AURA_5_SLIDES.pptx` |
| Build Log một trang Word | ☑ Đã dựng | `submission/AURA_BUILD_LOG.docx` |

## 2. File nên có trong GitHub

- Mã nguồn ASP.NET Core, migrations, views, CSS/JS và tests.
- `README.md`, `BUSINESS_RULES.md`, `ARCHITECTURE_AND_INTEGRATION_REPORT.md`.
- `docs/`: Build Log nguồn, deployment, runbook, test cases, model selection, measurement plan.
- `submission/AURA_WORKFLOW_SPEC.md`: workflow đầy đủ và state transition.
- `submission/AURA_5_SLIDES.pptx`: đúng 5 slide và phản ánh 93 test cùng workflow hiện tại.
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
