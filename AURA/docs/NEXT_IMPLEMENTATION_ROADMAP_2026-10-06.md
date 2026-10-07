# AURA — Roadmap sau live post-holdout

Cập nhật: 06/10/2026. Ưu tiên đóng rủi ro demo; không mở rộng tính năng phức tạp trước khi các gate hiện hành đạt.

## P0 technical baseline — đã đóng

Deployment `1f4b3c9` đã đạt health 5/5, Verify 5/5, security 6/6, workflow/undo 2/2, Audit giờ Việt Nam và viewport 390×844. Candidate logic `b69ff5d` sau Test Kit v3 đạt build sạch, 133/133 test; còn phải publish current master, recycle/health và chạy đúng một batch v3.1 trước khi freeze.

Gate điện thoại thật qua 4G/5G đã pass luồng chính, Verify, workflow, Audit, refresh và receipt persistence. Feedback P1 duy nhất là hai nút quản lý lệch cột; candidate đã sửa local và cần republish + phone regression trước khi đóng hoàn toàn.

Dừng freeze nếu health lỗi, Verify sai nhánh, Audit mất event, timestamp vẫn sai hoặc workflow approve/reject/undo sai.

## P1 bằng chứng người dùng và demo

1. Tổ chức ba phiên có giám sát với người dùng thực tế.
2. Mỗi phiên lưu consent, thiết bị/mạng, task, thời gian hoàn thành, lỗi quan sát được, feedback do người dùng xác nhận và action item.
3. Ưu tiên fixture tổng hợp hoặc ảnh đã che dữ liệu; không đưa dữ liệu nhạy cảm mới vào hệ thống khi chưa thống nhất cách xử lý.
4. Chạy ba rehearsal 8–10 phút trên cùng commit freeze. Lượt đầu sửa flow, lượt hai sửa timing, lượt ba không đổi code.
5. Chuẩn bị local OpenRouter baseline với database đã migrate và secret cục bộ. Ollama 4B post-policy chỉ đạt 13/15 nên không bật fallback mặc định.
6. Sau publish, chạy Test Kit v3.1 synthetic đúng một lượt; gate là 0 system error và 0 missed escalation. Không chạy lại v3 raw 11/15.
7. Thực hiện hai drill thay đổi có acceptance criteria/test/evidence và ba lượt rehearsal trên cùng commit freeze; tài liệu chiến lược cá nhân được giữ ngoài repository công khai.

## P2 sau khi P0 và P1 đạt

- Benchmark GPU 16 GB theo nguyên tắc synthetic-first: 8B Q4, context 16384/output 4096, ghi GPU/VRAM/driver/Ollama, warm-up, P50/P95, decision/field score, missed escalation và memory peak. Lệnh vận hành chi tiết được giữ ngoài repository công khai.
- PDF/nhiều trang: cần rasterization an toàn, page limit, malware/zip-bomb guard, provenance theo trang và benchmark lại accuracy.
- Auth/RBAC, object storage, retention/deletion, backup/restore, monitoring và rate limiting thuộc production backlog.

## Điều kiện GO Chung kết

Build/test/EF sạch; live health, Verify, Audit timezone và phone 4G đạt; local fallback chạy; không còn P0/P1 crash hoặc safety blocker; evidence nối được commit/provider/model/timestamp; hai onsite change drill và ba rehearsal hoàn tất. Ba user session cần hoàn tất hoặc dependency BTC phải được ghi trung thực.
