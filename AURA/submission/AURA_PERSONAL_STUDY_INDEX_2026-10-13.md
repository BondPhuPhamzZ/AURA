# Chỉ mục ôn tập cá nhân AURA

**Bản chốt:** 14/10/2026

**Đối tượng:** Phạm Gia Phú

**Mục đích:** ôn kiến trúc, luồng nghiệp vụ, bằng chứng và phản biện trước Demo Day.

## Thứ tự đọc hiện hành

1. `01_SO_TAY_AURA_HIEN_HANH_2026-10-14.md` - bức tranh tổng thể, số liệu được phép nói và ranh giới claim.
2. `02_LUONG_XU_LY_VA_MA_NGUON_2026-10-14.md` - luồng request, queue, extraction, validation, policy, người duyệt và audit.
3. `03_VAN_HANH_DEMO_VA_XU_LY_SU_CO_2026-10-14.md` - checklist trước khi quay, trước khi thi và phương án khi live/provider lỗi.
4. `00_KICH_BAN_THUYET_TRINH_VA_QA_VI_2026-10-14.md` - lời thoại, user story và câu trả lời BGK.
5. `00_KICH_BAN_VIDEO_3_PHUT_VI_2026-10-14.md` - kịch bản quay video sản phẩm.

Các file mang ngày 06/10, 07/10, 12/10 hoặc 13/10 trong thư mục Evidence là bằng chứng lịch sử đúng thời điểm phát sinh. Không dùng chúng làm nguồn trạng thái hiện hành nếu số liệu khác tài liệu ngày 14/10.

## Mười ý phải nhớ

1. Model chỉ trích xuất dữ kiện; `PolicyDecisionEngine` C# mới quyết định route.
2. Thứ tự fail-safe là `FACT -> POLICY -> AUTHORITY`.
3. Claimed amount không đi vào prompt trích xuất; backend so sánh sau.
4. HTTP 202 nghĩa là request đã được kiểm tra đầu vào và lưu bền vững, chưa có nghĩa AI đã xử lý xong.
5. SQL job là nguồn bền vững; bounded channel chỉ đánh thức worker.
6. `VisionInvocationCoordinator` giới hạn AI concurrency trong một process; đây không phải distributed limiter.
7. Verify có operation gate riêng nên năm click đồng thời cho một batch tạo 1 HTTP 200 và 4 HTTP 409.
8. OpenRouter/Qwen3.8 Flash qua Alibaba, `reasoning=none`, là primary; live fallback vẫn tắt.
9. Holdout prospective 10 ca đạt 8/10, 0 missed, 2 over; 11/12 chỉ là lần lặp stability trên dữ liệu đã biết.
10. Evidence localization mới là prototype cô lập; chưa được trình bày như capability production.

## Số liệu được phép nói

- Release build: 0 warning, 0 error.
- Offline tests: 163/163 pass; đây là test code, không phải 163 hóa đơn.
- Live Verify: 5/5 trong 65,004 giây.
- Prospective mini-holdout: 8/10, 0 missed, 2 fail-safe over, 0 system/provider/429 error, 28/30 field trọng yếu.
- Regression 12 source bytes: 10/12; sequential repeat: 11/12, P95 32,903 giây. Hai kết quả này không phải holdout mới.
- Năm upload khác nhau đồng thời: 5/5 hoàn tất, E2E P95 93,684 giây; đây là queue-integrity gate, không phải Verify SLA.

## Điều tuyệt đối không được nói quá

- Không claim production accuracy, model superiority nhân quả hoặc tự động duyệt không giám sát.
- Không nói 163 test tương đương 163 hóa đơn.
- Không claim multi-instance rate limiting, production RBAC, safe PDF, malware scanning, object storage hoặc immutable compliance audit.
- Không nói ba phiên người dùng đã hoàn tất khi chưa có biểu mẫu và quote được xác nhận.
- Không sửa prompt, policy, threshold, schema hoặc model sát Demo Day nếu chưa tái chạy đầy đủ gate.
