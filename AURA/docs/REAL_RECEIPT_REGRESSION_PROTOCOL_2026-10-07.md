# AURA — Protocol chạy lại 15 hóa đơn thật sau holdout

Cập nhật: 07/10/2026. Tài liệu này phân biệt official blind holdout cũ, regression trên cùng
15 ảnh và một blind holdout mới bằng receipt chưa từng đưa qua model.

## 1. Có được chạy lại 15 ảnh cũ không?

**Có**, nhưng từ request thứ hai chúng chỉ là **post-holdout real-receipt regression**. Lượt
chạy lại có giá trị để kiểm tra bản hardening hiện tại có sửa đúng failure mode hay gây regression,
nhưng không còn là phép đo blind/unseen và không được thay raw official 10/15.

Được phép so sánh ba view, luôn ghi rõ tên:

1. `official_raw`: 10/15, ground truth khóa trước request đầu tiên;
2. `official_adjudicated`: 11/15, chỉ sửa BH-04 do nhãn người sai;
3. `post_holdout_regression`: output của build hiện tại trên cùng 15 ảnh.

Không lấy view 3 thay view 1, không chạy nhiều lần rồi chọn batch tốt nhất và không gọi chênh
lệch là production accuracy.

## 2. Điều kiện trước khi upload lại

Quyền đã cấp trước đây ghi rõ “một lần official holdout duy nhất”, nên phải có **xác nhận mới**
cho lần re-upload sang SmarterASP/OpenRouter. Xác nhận cần nêu đúng 15 ảnh, mục đích regression,
provider và thư mục evidence. Không upload receipt thật lên máy GPU BTC.

Trước request đầu tiên:

- chọn ground truth regression là adjudicated v1 và ghi rõ BH-04 đã sửa trước lượt chạy;
- giữ nguyên claimed amount, expected facts và image SHA-256;
- ghi commit local, live health, model/provider và fallback state;
- tạo output directory mới, tuyệt đối không ghi đè official evidence;
- xác nhận ảnh vẫn là bản đã redaction review.

## 3. Cấu trúc evidence đề xuất

```text
22_real_receipt_regression_2026-10-DD/
  00_authorization_and_scope/
  01_locked_manifest_and_hashes/
  02_live_health_and_commit/
  03_raw_results/
  04_comparison_official_vs_current/
  05_conclusion_and_limits/
```

Raw tối thiểu gồm metadata, manifest/hash, results JSON/CSV, summary, provider/fallback fields,
latency và mọi error. Báo cáo phải liệt kê từng ca đổi từ pass→fail hoặc fail→pass và root cause;
không chỉ ghi aggregate score.

## 4. Receipt mới phải giữ riêng

Receipt mới chưa từng gửi model là tài sản đánh giá quý hơn. Không trộn chúng vào regression cũ
và không chạy thử từng ảnh trước khi khóa nhãn. Nếu muốn tạo blind holdout v2:

1. kiểm quyền sử dụng/redaction;
2. chọn coverage trước khi xem output;
3. khóa claimed amount, expected decision/facts và SHA-256;
4. commit/đóng manifest private trước request;
5. chạy đúng một official batch;
6. giữ raw kể cả khi kết quả xấu.

Nếu chưa đủ số lượng/độ đa dạng, gọi là `prospective mini-holdout`, không gọi là benchmark 15 ca
hay production accuracy. Nên giữ receipt mới unseen cho tới khi code/submission baseline đã freeze.

## 5. Thứ tự trước hạn nộp 14/10

1. Đặt lịch GPU BTC ngay; GPU chỉ dùng v3.1 tổng hợp và Ollama 8B.
2. Chốt/audit tài liệu, slide, Build Log, README, video/live link trước 12/10.
3. Nếu vẫn cần đo real receipt, xin lại consent rồi chạy đúng một batch 15 ảnh cũ vào 09–10/10.
4. Chỉ sửa blocker có test; code freeze cho hồ sơ nộp ngày 12/10.
5. Rehearsal và kiểm link trên thiết bị khác ngày 13/10; nộp/xác minh trước hạn ngày 14/10.
6. Receipt mới giữ unseen cho blind holdout v2 sau khi submission baseline được khóa, trừ khi có
   đủ thời gian để khóa/run/review trung thực trước code freeze.

## 6. Tiêu chí dừng

- Dừng nếu health không `ok`, fallback không đúng trạng thái, manifest/hash lệch hoặc consent chưa rõ.
- Một system error không được âm thầm rerun; giữ raw rồi phân loại lỗi hạ tầng/model/app.
- Không đổi expected theo output.
- Không publish code mới chỉ để tăng score nếu chưa có regression test và đủ thời gian rehearsal.
