# Live post-publish Verify/workflow — 07/10/2026

## Kết luận

Bản live `https://bondphupham-001-site1.ltempurl.com` đã đóng thêm gate vận hành sau lần
publish Test Kit v3.1. Đây là evidence cho **supervised MVP demo**, không phải production claim.

| Gate | Kết quả |
|---|---|
| Verify OpenRouter thật | 5/5 PASS: TC-01..03 AUTO, TC-04 FACT, TC-05 POLICY |
| System error | 0 |
| Workflow thật | TC-04 FACT → forward → `MANUAL_REVIEW_ACCEPTED` → UNDO → FACT |
| Reviewer/Audit | Request xuất hiện ở reviewer; Audit có request và `MANAGER_UNDO` |
| Desktop UI | Reviewer table, ảnh, lý do và action render đúng |
| Mobile 390x844 | Applicant/reviewer/audit chuyển thành card, không vỡ ngang |
| Form nhân viên | Giá trị mặc định `Gia Phú` hiển thị đúng |
| Browser console | 0 warning, 0 error trong lượt smoke |

Evidence local:

`D:\aura\demo_evidence\20_smarterasp_policy_v31_2026-10-07\04_verify_and_workflow`

- `verify-results.json`: raw response thật của đúng một lượt Verify 5 ca;
- `verify-workflow-summary.json`: expected/actual/latency từng ca và chuỗi workflow;
- `audit-after-undo.html`: snapshot Audit sau hoàn tác.

Request workflow: `4b72e5977dba416984b34eb22f3af7ec`. Sau UNDO, trạng thái cuối là
`ESCALATE_FACT`; thao tác duyệt và hoàn tác vẫn tồn tại trong Audit.

## Lưu ý về thời gian

Lượt POST Verify đã hoàn tất trước khi client PowerShell phát hiện lỗi bọc mảng 1 phần tử. Công
cụ sau đó resume từ `verify-results.json`, không gửi lại năm request. Vì vậy thời gian tổng của
lần resume được đặt `null`; latency từng ca 5.603/17.615/9.159/9.168/14.790 ms là số đo có giá
trị. Guard automated khóa hành vi resume để tránh ghi một con số vài mili-giây gây hiểu nhầm.

## Quyết định

- Không chạy lại official holdout 15 hoặc Test Kit v3.1: raw/hash đã khóa và safety gate đã đủ.
- Không cần publish lại vì thay đổi sau gate chỉ thuộc tools/tests/docs, không đổi runtime app.
- Gate còn mở là ba user session, ba rehearsal và benchmark Ollama 8B trên GPU BTC.
