# Danh sách 15 Test Cases (Sprint 1)

## 5 Canonical Cases (Dùng cho Verify Harness)
1. **Case 1 (AUTO_APPROVE)**: Hóa đơn Grab/Ăn uống hợp lệ, dưới hạn mức, có mã số thuế.
2. **Case 2 (AUTO_APPROVE)**: Hóa đơn taxi, ngày thường, thông tin rõ ràng.
3. **Case 3 (AUTO_APPROVE)**: Hóa đơn văn phòng phẩm (VPP), dưới hạn mức.
4. **Case 4 (ESCALATE_FACT)**: Hóa đơn mờ, nhòe, AI trả về confidence < 0.5.
5. **Case 5 (ESCALATE_POLICY)**: Hóa đơn nhà hàng có chứa "Bia/Rượu", nhân viên bình thường khai báo.

## Các ca mở rộng
6. Missing Merchant Name (ESCALATE_FACT).
7. Số tiền tổng sai lệch so với hệ thống tính toán (ESCALATE_FACT).
8. Hóa đơn phát sinh vào ngày thứ 7/CN (ESCALATE_FACT).
9. Mua thiết bị IT > 1,000,000đ (ESCALATE_AUTHORITY).
10. Hóa đơn ngoại tệ (ESCALATE_POLICY).
11. Hóa đơn rách mất 1 nửa.
12. Hóa đơn có cồn nhưng do nhân viên Sales khai báo (Ngoại lệ - ESCALATE_POLICY nhưng câu hỏi mềm mỏng hơn).
13. Upload nhầm ảnh selfie.
14. Hóa đơn trùng lặp.
15. Hóa đơn quá cũ (hơn 90 ngày).
