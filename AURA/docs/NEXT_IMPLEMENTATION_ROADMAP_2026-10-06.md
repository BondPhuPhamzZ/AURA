# AURA — Roadmap sau live post-holdout

Cập nhật: 06/10/2026. Ưu tiên đóng rủi ro demo; không mở rộng tính năng phức tạp trước khi các gate hiện hành đạt.

## P0 trước khi freeze

1. Publish revision mới được báo ở lần bàn giao: recycle trước, Web Deploy Release, chờ `Publish succeeded`, rồi recycle lần nữa.
2. `/healthz` phải trả `status=ok`, database/storage true, pending migration 0 và fallback false.
3. Chạy Verify một lượt; kỳ vọng 3 AUTO, 1 FACT và 1 POLICY.
4. Mở Audit; giờ dòng tổng hợp và timeline phải trùng giờ Việt Nam thực tế, không còn lệch 14 giờ.
5. Chạy public endpoint/security smoke; không upload lại ảnh holdout nếu chưa có quyền mới.
6. Kiểm tra trên điện thoại thật qua 4G/5G: ba tab, form, kết quả, manager và Audit không tràn ngang hoặc nháy sai tab.

Dừng freeze nếu health lỗi, Verify sai nhánh, Audit mất event, timestamp vẫn sai hoặc workflow approve/reject/undo sai.

## P1 bằng chứng người dùng và demo

1. Tổ chức ba phiên có giám sát với người dùng thực tế.
2. Mỗi phiên lưu consent, thiết bị/mạng, task, thời gian hoàn thành, lỗi quan sát được, feedback do người dùng xác nhận và action item.
3. Ưu tiên fixture tổng hợp hoặc ảnh đã che dữ liệu; không đưa dữ liệu nhạy cảm mới vào hệ thống khi chưa thống nhất cách xử lý.
4. Chạy ba rehearsal 8–10 phút trên cùng commit freeze. Lượt đầu sửa flow, lượt hai sửa timing, lượt ba không đổi code.
5. Chuẩn bị local fallback với database đã migrate và secret cục bộ; không bật Ollama fallback mặc định.

## P2 sau khi P0 và P1 đạt

- Benchmark GPU 16 GB: ghi GPU, VRAM, driver, OS, Ollama, model/quant/context, warm-up, concurrency, P50/P95, decision/field score, missed escalation và memory peak.
- PDF/nhiều trang: cần rasterization an toàn, page limit, malware/zip-bomb guard, provenance theo trang và benchmark lại accuracy.
- Auth/RBAC, object storage, retention/deletion, backup/restore, monitoring và rate limiting thuộc production backlog.

## Điều kiện GO Chung kết

Build/test/EF sạch; live health, Verify, Audit timezone và phone 4G đạt; không còn P0/P1 crash hoặc safety blocker; evidence nối được commit/provider/model/timestamp; ba user session và ba rehearsal hoàn tất, hoặc dependency BTC được ghi trung thực.
