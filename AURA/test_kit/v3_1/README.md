# Test Kit v3.1 review guide

V3.1 là revision đã khóa của regression kit, không phải blind holdout. Không sửa ảnh,
ground truth hoặc expected decision sau khi đã chạy model. Hash khóa nằm trong
`SHA256SUMS.txt`.

## Đọc đúng ảnh

- `contact-sheet.jpg` chỉ là mục lục xem nhanh. Mỗi hóa đơn trong đó là thumbnail nên
  chữ nhỏ; tuyệt đối không upload contact sheet để đánh giá OCR/VLM.
- Runner dùng từng file riêng trong `images/` theo `judge-manifest.json`. Các ảnh riêng
  có kích thước khoảng 1.1k x 1.5k pixel và được AURA đọc nguyên byte, không resize ở
  phía ứng dụng.
- Khi human-review, mở file riêng ở 100% zoom rồi mới kết luận chữ mờ/nhỏ hoặc ground
  truth sai.

## 15 ảnh gốc dùng để review

1. [R3-01-auto-vat.jpg](images/R3-01-auto-vat.jpg)
2. [R3-02-auto-ptt.jpg](images/R3-02-auto-ptt.jpg)
3. [R3-03-auto-rrn.jpg](images/R3-03-auto-rrn.jpg)
4. [R3-04-auto-bia-ho-so.jpg](images/R3-04-auto-bia-ho-so.jpg)
5. [R3-05-auto-readable-fade.jpg](images/R3-05-auto-readable-fade.jpg)
6. [R3-06-fact-destroyed-total.jpg](images/R3-06-fact-destroyed-total.jpg)
7. [R3-07-fact-device-ids-only.jpg](images/R3-07-fact-device-ids-only.jpg)
8. [R3-08-fact-destroyed-receipt-number.jpg](images/R3-08-fact-destroyed-receipt-number.jpg)
9. [R3-09-fact-amount-mismatch.jpg](images/R3-09-fact-amount-mismatch.jpg)
10. [R3-10-policy-beer.jpg](images/R3-10-policy-beer.jpg)
11. [R3-11-policy-personal-care.jpg](images/R3-11-policy-personal-care.jpg)
12. [R3-12-policy-entertainment.jpg](images/R3-12-policy-entertainment.jpg)
13. [R3-13-authority-vat.jpg](images/R3-13-authority-vat.jpg)
14. [R3-14-authority-receipt.jpg](images/R3-14-authority-receipt.jpg)
15. [R3-15-authority-vat.jpg](images/R3-15-authority-vat.jpg)

Nếu cần tăng font hoặc thay layout, phải tạo v3.2, human-lock manifest/hash mới trước
request đầu tiên và giữ nguyên v3/v3.1 để không làm mất khả năng tái lập evidence.
