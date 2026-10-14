# Protocol đánh giá AURA với người dùng thực tế

## Mục đích và điều kiện hoàn tất

Protocol này dùng để hoàn tất gate user evidence của OrganizationAI Sprint 2 mà không tạo người dùng hoặc feedback giả. Một gói hoàn chỉnh phải có ba người thật đang trực tiếp nộp hoàn ứng, kiểm tra kế toán/hành chính hoặc phê duyệt quản lý trong công việc. Mỗi record cần họ tên và chức danh thật, consent bằng văn bản, feedback nguyên văn được người tham gia xác nhận, một thay đổi sản phẩm truy vết tới commit hoặc evidence trước-sau và ít nhất một tác động tiêu cực cụ thể.

Chủ dự án không thể thay cho cả ba người tham gia. Không dùng persona giả, không trình bày câu diễn giải lại như trích dẫn nguyên văn và không xin chữ ký hồi tố cho nội dung người tham gia chưa đọc.

## Tiêu chí chọn người tham gia

Nếu có thể, mời ít nhất ba người đại diện cho các bước khác nhau trong workflow:

1. người nộp hồ sơ hoàn ứng;
2. nhân sự kế toán hoặc hành chính kiểm chứng từ;
3. quản lý hoặc người giữ ngân sách quyết định ngoại lệ.

Nếu hai người có cùng vai trò, ghi rõ vì sao mẫu vẫn phản ánh được góc nhìn vận hành có ý nghĩa. Không tính người chỉ xem buổi pitching mà chưa tự thao tác sản phẩm.

## Dữ liệu và quyền riêng tư

- Dùng hóa đơn tổng hợp và tên nhân viên giả trong phiên test.
- Không ghi hình API key, billing dashboard, user secret, database thật hoặc log provider riêng tư.
- Xin phép riêng cho việc ghi màn hình/âm thanh và cho việc công bố tên, vai trò, trích dẫn.
- Lưu form đã ký trong Team Drive riêng tư. Chỉ commit template trống và bản tổng hợp ẩn danh nếu consent không cho phép công bố danh tính.
- Dừng phiên nếu người tham gia vô tình hiển thị dữ liệu cá nhân hoặc dữ liệu tổ chức của bên thứ ba.

## Kịch bản cho mỗi người tham gia

Thời lượng mục tiêu: 20 đến 25 phút.

| Giai đoạn | Thời gian | Tác vụ của người tham gia | Evidence người điều phối ghi nhận |
|---|---:|---|---|
| Giới thiệu và consent | 3 phút | Đọc mục đích, cách xử lý dữ liệu và nguyên tắc tự nguyện | Consent đã ký và lựa chọn ghi hình |
| Ca thường quy | 4 phút | Upload receipt tổng hợp rõ, nhập claim và tìm kết quả | Hoàn tất hay không, thời gian, do dự, mức trợ giúp |
| Ca ngoại lệ | 5 phút | Xem một kết quả `ESCALATE_*` và giải thích câu hỏi bằng lời của mình | Mức hiểu, evidence đã dùng, ngữ cảnh còn thiếu |
| Human workflow | 5 phút | Chuyển tiếp, trả lời Yes/No như quản lý, xem Audit rồi Undo | Kết quả đúng và mức hiểu audit |
| Phản hồi mở | 5 phút | Nêu phần hữu ích nhất, khó hiểu nhất và một tác động tiêu cực | Câu nguyên văn do người tham gia viết hoặc xác nhận |
| Kết thúc | 3 phút | Xếp ưu tiên thay đổi và xác nhận phạm vi được công bố | Ưu tiên và consent công bố |

Không hướng dẫn thao tác trừ khi người tham gia bị chặn. Ghi nhận điểm bị chặn trước khi hỗ trợ.

## Chỉ số bắt buộc

Với mỗi tác vụ, ghi:

- hoàn tất không cần trợ giúp, có trợ giúp hoặc không hoàn tất;
- thời gian bắt đầu và kết thúc;
- số click sai hoặc lần quay lại điều hướng;
- người tham gia có hiểu ai ra quyết định hay không: model, policy, nhân viên hoặc quản lý;
- câu hỏi escalation có đủ để hành động mà không mở lại hóa đơn hay không;
- điểm tự tin từ 1 đến 5 và lý do.

Mỗi phiên phải có một quan sát tích cực và một tác động tiêu cực. Tác động tiêu cực hợp lệ có thể là thời gian chờ AI, công việc review dồn theo đợt, thêm bước xác nhận, automation bias, giảm trao đổi trực tiếp hoặc tăng nhu cầu đào tạo. Câu “không có tác động tiêu cực” không đáp ứng yêu cầu evidence của track.

## Quy tắc feedback nguyên văn

Người tham gia phải tự viết câu trích dẫn hoặc đọc chính xác câu đó và đánh dấu đã xác nhận. Giữ nguyên cách nói tiếng Việt, kể cả cách diễn đạt đời thường. Có thể thêm bản dịch tiếng Anh bên dưới nhưng phải ghi rõ là bản dịch.

Dùng câu xác nhận sau:

> Tôi xác nhận câu trích dẫn bên dưới phản ánh đúng phản hồi của tôi sau khi sử dụng AURA trong phiên này. Tôi hiểu nội dung sẽ được chia sẻ ở đâu và có thể rút lại quyền cho phép trước hạn nộp bài.

## Chuyển feedback thành một thay đổi truy vết được

Sau ba phiên:

1. nhóm quan sát theo từng bước workflow;
2. chọn một thay đổi được ít nhất một quan sát trực tiếp hỗ trợ và an toàn trước khi freeze;
3. viết acceptance criterion trước khi triển khai;
4. giữ evidence trước thay đổi;
5. triển khai và test mà không sửa ground truth hoặc dữ liệu đánh giá model;
6. ghi commit SHA và evidence sau thay đổi;
7. nhờ người đã nêu vấn đề xem lại thay đổi nếu khả thi.

Không nói auto-scroll hoặc evidence-overlay prototype hiện tại xuất phát từ user study trừ khi có record người tham gia kèm ngày chứng minh quan hệ nhân quả đó.

## Cấu trúc gói evidence riêng tư

```text
USER_STUDY_EVIDENCE/
  00_PROTOCOL_AND_CONSENT/
  01_PARTICIPANT_01/
  02_PARTICIPANT_02/
  03_PARTICIPANT_03/
  04_AGGREGATE_METRICS/
  05_FEEDBACK_TO_CHANGE/
    before/
    after/
    commit.txt
  06_NEGATIVE_IMPACT/
  07_INTEGRITY/
    manifest.sha256
```

Mỗi thư mục người tham gia cần có form đã ký, bảng thời gian tác vụ, câu trích dẫn đã xác nhận, ghi chú quan sát và bản ghi hình nếu consent cho phép. Giữ recording ở chế độ riêng tư trừ khi người tham gia cho phép chia sẻ rộng hơn.

## Báo cáo tổng hợp

Báo cáo công khai phải nêu quy mô mẫu và vai trò, không trình bày mẫu nhỏ như đại diện thống kê. Chỉ báo cáo median task time khi cả ba phiên đo cùng một tác vụ. Ghi cả thất bại, trợ giúp và phản hồi tiêu cực. Tách quan sát trực tiếp khỏi diễn giải của người điều phối.

## Checklist hoàn tất

- [ ] Ba người tham gia đủ điều kiện, có danh tính và vai trò thật.
- [ ] Consent bằng văn bản và phạm vi công bố của từng người.
- [ ] Cùng một core task script và dữ liệu tổng hợp cho cả ba phiên.
- [ ] Ghi task success, thời gian, trợ giúp, mức hiểu và độ tự tin.
- [ ] Một trích dẫn nguyên văn được xác nhận từ mỗi người.
- [ ] Ít nhất một tác động tiêu cực cụ thể.
- [ ] Một thay đổi truy vết từ feedback tới acceptance criterion, commit và evidence trước-sau.
- [ ] Manifest toàn vẹn cho gói evidence riêng tư.
- [ ] Chỉ đồng bộ proposal, Slide 3, Slide 5, Build Log, README và pitch sau khi evidence thực sự tồn tại.
