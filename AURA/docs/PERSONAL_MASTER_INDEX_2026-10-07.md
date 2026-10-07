# AURA — Personal master index

Cập nhật: 07/10/2026. Dùng file này làm điểm bắt đầu; tài liệu có ngày cũ hơn được giữ để hiểu lịch sử, không dùng làm baseline hiện hành.

## Đọc theo thứ tự

1. `03_CURRENT_PROJECT_STATUS_2026-10-07.md` — trạng thái live v3.1, 142 test và gate còn mở.
2. `BAO_CAO_TIEN_DO_2026-10-07.md` — nội dung điền form check-in.
3. `01_NEXT_IMPLEMENTATION_ROADMAP_2026-10-07.md` — việc phải làm theo thứ tự, có tiêu chí dừng.
4. `10_VISION_INPUT_AND_OFFLOAD_GUIDE_2026-10-07.md` — phân biệt ảnh, context, output cap và CPU/GPU off-load.
5. `11_LIVE_REGRESSION_V31_RESULT_2026-10-07.md` — live 13/15 exact, 0 missed/system error, field 140/146 và phân tích mismatch.
6. `12_LIVE_POSTPUBLISH_VERIFY_WORKFLOW_2026-10-07.md` — Verify 5/5, workflow forward/accept/undo, UI smoke và vị trí evidence.
7. `13_OPENROUTER_POSTCONTRACT_V2_RESULT_2026-10-07.md` — v2 hậu-contract 14/15, TK-12 blur và vì sao v3.1 là gate hiện hành.
8. `07_ONSITE_FINAL_PLAYBOOK_2026-10-06.md` — lịch 17/10, chiến lược bốn giờ, bản đồ code, kế hoạch ôn và demo 15 phút.
9. `06_MANUAL_FINAL_GATES_2026-10-06.md` — checklist điện thoại thật, user session, rehearsal và readiness onsite.
10. `demo_setup/DEMO_COMMANDS_AND_TEST_PLAN_2026-10-06.md` — publish, smoke, rehearsal, local fallback và checklist setup onsite.
11. `presentation/AURA_FINAL_7_MINUTE_PITCH_AND_JUDGE_QA_2026-10-06.md` — script vòng bảng, bản nén Chung kết và câu hỏi BGK.
12. `02_BLIND_HOLDOUT_LOCKING_AND_GROUND_TRUTH_2026-10-05.md` — phương pháp khóa nhãn/hash; dùng để giải thích, vì official holdout đã chạy xong.
13. `09_REGRESSION_TEST_KIT_V3_LIVE_RESULT_2026-10-06.md` — raw v3 11/15, phân tích bốn mismatch và ranh giới claim.
14. `demo_setup/REGRESSION_TEST_KIT_V3_RUNBOOK_2026-10-06.md` — nguồn gốc ảnh, hash lock, v3 bất biến và v3.1.
15. `explain/AURA_PROJECT_ARCHITECTURE_GUIDE.md` và `explain/AURA_TESTING_DATA_AND_IMPLEMENTATION_WORKFLOW_GUIDE.md` — tài liệu học sâu; số baseline cũ trong đó là lịch sử.
16. `08_OLLAMA_POST_POLICY_REGRESSION_2026-10-06.md` — kết quả mới nhất 4B 13/15 và lý do chưa bật fallback.
17. `demo_setup/BTC_GPU_8B_RUNBOOK_2026-10-06.md` — lệnh setup và agenda một giờ trên máy BTC.
18. `14_REAL_RECEIPT_REGRESSION_PROTOCOL_2026-10-07.md` — cách chạy lại 15 receipt cũ mà không làm sai claim và cách giữ receipt mới unseen.
19. `15_REAL_RECEIPT_REGRESSION_RESULT_2026-10-07.md` — kết quả 11/15, 0 missed, 4 over và phân tích safety trade-off.
20. `16_AURA_FINAL_STUDY_GUIDE_2026-10-07.md` — cẩm nang source code, nghiệp vụ, evidence, onsite drill và lịch ôn.
21. `17_AURA_FINAL_PITCH_AND_JUDGE_QA_2026-10-07.md` — pitch 7–8 phút, bản nén 5 phút và Q&A theo số hiện hành.

## Baseline nhớ khi trình bày

| Nội dung | Giá trị hiện hành |
|---|---|
| Deployed live | Health 5/5 `ok`; R3-04/R3-08/R3-12 xác nhận hành vi hardening mới đang live; server chưa expose commit attestation |
| Candidate hiện hành | policy entertainment + Vietnamese diacritic guard + token/off-load observability + UTF-8 runner fix |
| Offline gate | Release publish sạch, 142/142 test, EF clean |
| Live gate | health 5/5, security 4/4; v3.1 15/15 completed, 13/15 exact, 0 missed/system error, field 140/146; Verify 5/5 + workflow undo pass |
| Holdout raw | 10/15; missed 1; over 3; system error 0 |
| Holdout adjusted | 11/15 vì BH-04 có nhãn người sai; raw không đổi |
| Provider | OpenRouter/Qwen3-VL-8B primary; Ollama 4B post-policy 13/15, fallback tắt |
| Test Kit v3 raw | 11/15 decision, 142/145 field, 0 system error; raw bất biến |
| Test Kit v2 hậu-contract | 14/15 decision, missed TK-12, 0 over/system error, field 69/75; raw bất biến |
| Real receipt regression | 11/15 exact adjudicated, 0 missed, 4 over, 0 system error; official raw 10/15 bất biến |
| Chưa đóng | 3 users, 3 rehearsals, GPU 8B benchmark cùng v3.1; fallback vẫn tắt |

## Một câu mô tả sản phẩm

AURA là trợ lý thẩm định hoàn ứng: VLM chỉ đọc dữ kiện có cấu trúc, policy C# quyết định theo `FACT → POLICY → AUTHORITY`, còn con người xử lý ngoại lệ và mọi bước được audit.

## Ranh giới claim

- Không nói “142 test = 142 hóa đơn”. Đây là code tests offline.
- Không nói “holdout 73,33% = production accuracy”. Mẫu chỉ có 15 ca và adjusted view không thay raw.
- Không gọi 15 ảnh cũ là blind nếu chạy lại; chúng chỉ còn là regression set.
- Không nói fallback Ollama đang bật hoặc an toàn tương đương OpenRouter.
- Không nói OpenRouter hậu-contract v2 vẫn 15/15; kết quả mới là 14/15 do TK-12.
- Không nói hệ thống đã có PDF, RBAC production, antivirus, retention hoặc object storage.

## Khi nào được quay video/public live URL

Technical baseline, phone 4G/5G, live v3.1, Verify/workflow và mobile viewport safety gate đã đạt.
Trước khi public URL trong README: hoàn tất ít nhất một rehearsal sạch. Ba user session nên hoàn
tất trước pitch để có development story; nếu BTC chưa kết nối kịp, công bố là dependency đang
chờ và không bịa user evidence.

## Ưu tiên ôn trước 17/10

Ưu tiên tham khảo 60% nghiệp vụ/luồng quyết định và 40% code navigation/test. Không học thuộc code: với mỗi yêu cầu phải trả lời được “vì sao đổi, sửa ở đâu, test nào chứng minh”. Trước onsite cần hai change drill, một script vòng bảng 8 phút và một bản nén Chung kết 5 phút.
