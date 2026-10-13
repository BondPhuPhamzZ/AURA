# AURA — Kế hoạch định vị evidence trên ảnh — 13/10/2026

## Kết luận

Tính năng cho người duyệt xem ảnh ngay trong AURA và bấm vào một field để thấy vùng evidence tương ứng là **khả thi** và phù hợp với mục tiêu minh bạch. Tuy nhiên baseline hiện tại chỉ có text facts/evidence; chưa có tọa độ, OCR token box hoặc ground truth vùng ảnh. Vì vậy có thể làm prototype sau submission freeze, nhưng chưa đủ dữ liệu để tuyên bố bounding-box accuracy hoặc đưa vùng khoanh vào quyết định tự động.

Không chỉnh, vẽ hoặc lưu đè lên ảnh gốc. Overlay phải là SVG/canvas trong trình duyệt, dựng từ JSON tọa độ riêng và gắn với SHA-256 của ảnh. Cách này giữ nguyên bytes phục vụ audit và tránh việc annotation/redaction mới làm thay đổi inference.

## Hiện trạng code

- `ReceiptExtractionDto` contract v2 lưu facts, raw date evidence và final-total evidence dạng text; không có region/coordinate.
- OpenRouter và Ollama dùng cùng JSON Schema; schema hiện không yêu cầu model trả bounding box.
- Ảnh gốc được lưu riêng, route `/Applicant/Receipt/{id}` trả ảnh với `NoStore` và UI đã có thumbnail/link.
- Panel `Nội dung AI` hiển thị facts nhưng chưa liên kết field với vị trí ảnh.
- Policy đang phân loại một số `warnings` tự do bằng keyword. Addendum Three O'Clock cho thấy câu `che mờ bằng dải đen` có thể kích hoạt FACT dù merchant/date/identifier/items/total đều đúng.

## Kiến trúc đề xuất

```text
Ảnh gốc bất biến + SHA-256
        |
        +--> Vision extraction contract v2: facts/decision hiện hành
        |
        +--> OCR word boxes hoặc evidence localizer độc lập
                  |
                  +--> match exact evidence text với token boxes
                  +--> EvidenceRegion JSON, tọa độ chuẩn hóa 0..1000
                                  |
                                  +--> SVG overlay trong browser
                                  +--> click field <-> highlight vùng ảnh
```

Đề xuất không yêu cầu model quyết định nghiệp vụ từ bounding box. Policy C# vẫn dùng facts đã kiểm định. Region chỉ hỗ trợ reviewer và audit cho tới khi có benchmark riêng.

### Contract vùng evidence tối thiểu

```json
{
  "imageSha256": "...",
  "coordinateSpace": { "width": 1000, "height": 1000, "orientationApplied": true },
  "regions": [
    {
      "field": "totalAmount",
      "text": "Tổng tiền: 58.000 đ",
      "box": { "x": 112, "y": 804, "width": 706, "height": 52 },
      "source": "OCR_TEXT_MATCH",
      "confidence": 0.97
    }
  ]
}
```

Mỗi region phải mang `imageSha256`; AURA từ chối render nếu hash không khớp. Tọa độ chuẩn hóa giúp overlay đúng khi ảnh co giãn. Cần xử lý EXIF orientation trước khi quy đổi; không dùng pixel CSS làm ground truth.

## Nguồn tọa độ nên chọn

1. **Ưu tiên OCR có word-level boxes rồi đối chiếu evidence text.** Đây là đường dễ kiểm tra và ít phụ thuộc cách model vision tự ước lượng tọa độ. Có thể đánh giá PaddleOCR/Tesseract local hoặc một dịch vụ document OCR, nhưng phải đo trên tiếng Việt và ảnh thật đã redaction.
2. **VLM trả bounding box chỉ là candidate.** Không giả định Qwen/OpenRouter sẽ trả tọa độ chính xác chỉ vì model hiểu ảnh. Phải khóa schema, test orientation/resize và đo IoU trước.
3. **Không dùng crop/annotation làm input thay thế ảnh gốc trong baseline.** Nếu cần crop cho reviewer, tạo client-side từ region đã lưu; không upload lại model và không lưu đè ảnh.

## Dữ liệu hiện có và phần còn thiếu

Đã có đủ để bắt đầu prototype:

- ảnh gốc/provider-ready có hash;
- facts text và các dòng evidence quan trọng;
- route ảnh và panel facts;
- 12 ca đã biết để nghiên cứu lỗi giao diện và matching.

Chưa đủ để đánh giá khách quan:

- chưa có ground-truth boxes cho merchant, date, identifier, final total và line items;
- 12 ảnh hiện đã được nhìn/chạy, nên chỉ là development set;
- chưa có unseen evaluation set cho localization;
- chưa có metric IoU/region recall/false highlight hoặc thời gian review của con người.

Tối thiểu nên annotate bốn field quan trọng trên tập development hiện có, sau đó khóa một tập unseen riêng. Chỉ khi region recall của field trọng yếu đạt gate đã định và không highlight nhầm số tiền mới đưa vào demo chính.

## Lộ trình an toàn

### P0 — sau submission freeze

- Hiển thị ảnh lớn ngay trong modal/panel, không mở tab mới; vẫn dùng route ảnh hiện tại.
- Giữ thumbnail/link dự phòng và không đổi extraction/policy.
- Bổ sung test responsive, keyboard, screen-reader label và `NoStore`.

### P1 — prototype evidence overlay

- Thêm `EvidenceRegion` dạng JSON phụ, chưa migration nếu lưu cùng facts versioned; tốt hơn là cột/bảng riêng khi cần query/audit.
- Chỉ định vị merchant, ngày, canonical identifier và final total.
- Dùng OCR word boxes + deterministic text matching; vùng không chắc chắn thì không vẽ thay vì bịa vị trí.
- SVG overlay hỗ trợ bật/tắt, zoom và click hai chiều field–region.

### P2 — hardening

- Thay warning text bằng `qualityIssues` có `code`, `affectedFields`, `severity`, `region` và `declaredRedaction`.
- Redaction nằm ngoài field trọng yếu có thể là thông tin tham khảo; redaction chạm/che date, identifier, items hoặc total vẫn fail-safe.
- Thêm audit cho version localizer, image hash và confidence; không dùng region để tự sửa facts.

### P3 — evaluation

- Khóa annotation trước chạy; một reviewer khác kiểm box/field.
- Đo region recall theo field, IoU, false-highlight rate, missing-region rate và thời gian reviewer.
- Dùng ảnh xoay, scale, nhiều tỷ lệ, receipt dài, chữ nhỏ và vùng che làm negative controls.
- Tập unseen localization phải tách khỏi 12 ảnh đã biết.

## Gate đề xuất

- Không thay đổi bytes/SHA-256 ảnh gốc.
- 100% region hiển thị đúng trên desktop và mobile sau resize/orientation.
- Không có false highlight sang một số tiền khác trên field `totalAmount`.
- Region không chắc chắn phải hiện “không định vị được”, không tự suy đoán.
- Policy/decision của baseline không thay đổi khi bật hoặc tắt overlay.
- Route ảnh/overlay phải có authorization trước khi xem xét pilot nhiều người; `NoStore` một mình chưa thay authentication/RBAC.

## Quyết định hiện tại

- **GO** cho thiết kế/prototype sau khi freeze submission.
- **NO-GO** cho việc sửa pipeline hoặc prompt ngay trước demo.
- **NO-GO** cho claim explainability bằng bounding box cho đến khi có annotation và benchmark riêng.
- Addendum Three O'Clock được giữ làm evidence cho backlog typed-quality-issue; không rerun và không nới warning guard trực tiếp trên ca đó.
