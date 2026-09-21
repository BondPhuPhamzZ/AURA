# AURA — Checklist nộp bài

Cập nhật: 22/09/2026. Trạng thái này là nguồn đối chiếu cuối; không điền số liệu hoặc URL chưa được xác minh.

## 1. Sáu hạng mục bắt buộc

| Hạng mục | Trạng thái | File/bằng chứng |
|---|---|---|
| Live URL public, không cần đăng nhập | ☐ Chưa có | Điền sau khi hoàn tất `docs/DEPLOYMENT.md` |
| Verify Harness + runbook | ☑ Có | `docs/RUNBOOK.md`, 5 ca live, `test_kit/judge-manifest.json` đúng 15 ca |
| Public repository và lịch sử commit | ◐ Cần push/xác minh public | `https://github.com/BondPhuPhamzZ/AURA` |
| Video demo tối đa 3 phút | ☐ Chưa quay | `submission/VIDEO_DEMO_SCRIPT.md`; nộp link ngoài Git |
| Đúng 5 slide | ☑ Đã dựng | `submission/AURA_5_SLIDES.pptx` |
| Build Log một trang Word | ☑ Đã dựng | `submission/AURA_BUILD_LOG.docx` |

## 2. File nên có trong GitHub

- Mã nguồn ASP.NET Core, migrations, views, CSS/JS và tests.
- `README.md`, `BUSINESS_RULES.md`, `BUILD_LOG.md`, `PROJECT_AUDIT.md`.
- `docs/`: deployment, runbook, test cases, model selection, measurement plan.
- `submission/AURA_WORKFLOW_SPEC.md`: workflow đầy đủ và state transition.
- `submission/AURA_5_SLIDES.pptx`: đúng 5 slide; cập nhật Live URL trước bản nộp cuối nếu cần.
- `submission/AURA_BUILD_LOG.docx`: một trang.
- `submission/VIDEO_DEMO_SCRIPT.md`: kịch bản và vị trí điền link video.
- `submission/SUBMISSION_CHECKLIST.md`: manifest bàn giao.
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

Folder mẫu `D:\aura\VNG\artifact` là project tham khảo, không phải một archive nộp cứng. Nó có README, runbook, build log, slide outline và demo script; không có PPTX, DOCX hay video binary. AURA giữ cùng tinh thần nhưng cung cấp thêm artifact mở được ngay.

## 5. Việc còn phải làm trước hạn

1. Deploy, mở Live URL trong cửa sổ ẩn danh và kiểm `/healthz`.
2. Chạy một ảnh smoke rồi đúng một lượt Verify; ghi thời điểm và kết quả thật.
3. Điền Live URL đồng nhất vào README, slide, mô tả video và form nộp.
4. Quay video dưới 3 phút theo script; kiểm quyền “ai có link đều xem được”.
5. Thu phản hồi có đồng thuận từ ba người dùng nếu rubric chấm hạng mục này; hiện chưa có bằng chứng nên không được tự tạo quote.
6. Quét secret, build/test lần cuối, push nhánh chốt và xác minh repository public.
