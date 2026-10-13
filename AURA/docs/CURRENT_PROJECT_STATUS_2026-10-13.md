# AURA — Current project status 13/10/2026

## Kết luận điều hành

AURA vẫn là **supervised demo release candidate đã qua live technical gate**. Primary là OpenRouter/Qwen3.8 Flash qua Alibaba, `reasoning=none`, fallback tắt và AI concurrency 1. Không có thay đổi source, policy, prompt, threshold, schema hoặc kiến trúc trong lượt holdout ngày 13/10.

Prospective mini-holdout 10 hóa đơn thật mới đã hoàn tất đúng một lượt: **8/10 exact, 0 missed-escalation, 2 over-escalation, 0 system/provider/429 error, 28/30 field trọng yếu**. Hai mismatch đều fail-safe, không do provider. Vì vậy candidate đủ cơ sở cho demo có giám sát nhưng chưa đủ cơ sở cho accuracy production hoặc unattended rollout.

Hai receipt bổ sung được khóa và chạy riêng đúng một lượt như prospective addendum: **1/2 exact, 0 missed, 1 over, 0 system/provider/429 error**. Three O'Clock được trích xuất đúng toàn bộ dữ kiện cốt lõi nhưng warning tự do về dải che riêng tư kích hoạt FACT. Combined descriptive view là **9/12**, không phải một holdout 12 ca mới.

Post-holdout regression riêng sau đó gửi trực tiếp 12 source bytes, không snapshot/resize/transform,
đạt **10/12 exact, 0 missed, 2 over, 0 system/provider/429 error và 35/36 field trọng yếu**.
Wave năm receipt khác nhau hoàn tất 5/5 nhưng E2E P95 là 93,684 giây, nên chứng minh tính toàn vẹn
hàng đợi chứ chưa chứng minh đủ năm kết quả dưới 90 giây. Đây không thay số prospective 9/12.

## Baseline hiện hành

| Hạng mục | Kết quả và ranh giới |
|---|---|
| Live health | `status=ok`; OpenRouter/Qwen3.8 Flash; reasoning none; DB/migration/storage OK; concurrency 1; fallback false |
| Offline suite | Build 0 warning/error; 163/163 automated tests sau UX auto-scroll + prototype overlay cô lập; không gọi paid AI API |
| Live Verify | 5/5; 65,004 giây; 0 system/provider error; 5 client tạo 1×200 + 4×409 |
| Post-holdout regression cũ | 15/15 trên 15 receipt đã biết; 30/30 field; không phải blind accuracy |
| Mini-holdout mới | 8/10; 0 missed; 2 over; 0 system/provider/429; 28/30 field; không rerun |
| Mini-holdout latency | 5 request đầu terminal 64,763 giây; 10 request terminal 108,803 giây; AI concurrency 1 |
| Addendum 2 ca | 1/2 exact; 0 missed; 1 over; 0 system/provider/429; cả hai terminal trong 23,020 giây; không rerun |
| Regression 12 source bytes | 10/12; 0 missed; 2 over; 0 system/provider/429; 35/36 field; wave 5 client P95 93,684 giây; không phải holdout mới |
| Ollama local | Contingency có kiểm soát; không bật trên SmartASP vì latency/capacity và loopback host |

Chi tiết mini-holdout: [`OFFICIAL_HOLDOUT10_2026-10-13.md`](OFFICIAL_HOLDOUT10_2026-10-13.md).
Chi tiết regression source bytes: [`REGRESSION12_ORIGINAL_BYTES_RESULT_2026-10-13.md`](REGRESSION12_ORIGINAL_BYTES_RESULT_2026-10-13.md).

## Diễn giải hai over-escalation

- FAHASA: validator chưa nhận nhãn `Thanh toán` là bằng chứng tổng cuối đủ tin cậy dù số tiền đúng.
- GS25_2: model bỏ merchant name khi blackout an toàn nằm gần logo, dù các field ngày/ID/tổng đúng.

Hai lỗi cần regression-driven hardening sau khi freeze. Không sửa ngay rồi rerun cùng tập để biến kết quả thành 10/10.

Addendum bổ sung một backlog rõ hơn: warning chất lượng ảnh hiện là text tự do và policy dò keyword. Cùng khái niệm redaction có thể được model diễn đạt khác ngôn ngữ; câu tiếng Việt chứa `mờ` đã làm Three O'Clock over-escalate dù facts đầy đủ. Không whitelist mọi black box. Sau freeze cần chuyển sang quality issue có code + affected field và kiểm thử vùng che chạm/không chạm field trọng yếu.

## Quyết định demo

- Có thể quay/live demo bằng Verify 5 ca hoặc một upload có kiểm soát; giữ một operator và không chạy Verify song song với upload.
- Giữ video dự phòng vì OpenRouter/Alibaba là dịch vụ bên thứ ba.
- Durable queue đã nhận burst 10 ca; inference vẫn tuần tự để tránh tái tạo 429.
- Trước buổi chấm, chỉ làm rehearsal không inference hoặc đúng một smoke được lên lịch; không chạy lại holdout/Verify để săn số đẹp.

## Claim an toàn

Có thể nói AURA đạt 0 missed-escalation và 0 lỗi hệ thống/provider trong prospective mini-holdout 10 ca, nhưng chỉ đúng trong mẫu nhỏ đã khóa ngày 13/10. Phải công bố 2 over-escalation, 28/30 field trọng yếu, redaction confounder và ranh giới supervised demo.

Nếu báo combined descriptive view phải nói rõ 9/12 đến từ 10-case holdout và 2-case addendum chạy tuần tự. Không gọi đây là holdout 12 ca mới. Kế hoạch overlay evidence nằm tại `EVIDENCE_LOCALIZATION_PLAN_2026-10-13.md`; chưa được triển khai vào baseline.
