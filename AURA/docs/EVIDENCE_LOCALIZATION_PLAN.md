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

> **Đã triển khai trong source 13/11/2026, chưa publish.** `wwwroot/js/receipt-viewer.js` mở
> `<dialog>` ngay trên trang; dialog nằm ngoài vùng fragment được poll. Ảnh hiển thị vừa khung
> hoặc kích thước gốc, có nút mở tab mới và đóng (Esc, click nền hoặc nút Đóng); focus trả về
> link đã bấm. Dialog dùng lại route `NoStore` hiện có và chỉ nhận URL ảnh cùng origin; ảnh
> thiếu hoặc sai SHA-256 hiện thông báo. Click có phím bổ trợ và trình duyệt không hỗ trợ
> dialog vẫn mở tab như cũ. Kiểm tra node + UI contract, cùng browser desktop/mobile trên
> LocalDB tạm với fixture R3-01. Không vẽ vùng, không đổi extraction/policy.

- Hiển thị ảnh lớn ngay trong modal/panel, không mở tab mới; vẫn dùng route ảnh hiện tại.
- Giữ thumbnail/link dự phòng và không đổi extraction/policy.
- Bổ sung test responsive, keyboard, screen-reader label và `NoStore`.

### P1 — prototype evidence overlay

> **Đã triển khai trong source 14/11/2026, local và chưa publish.**
>
> - **Bật/tắt:** `EvidenceLocalization:Enabled` mặc định `false` và chỉ có hiệu lực trên
>   host Development. Khi tắt, `GET /Evidence/Regions/{id}` trả 404 và DLL Tesseract
>   không được nạp.
> - **OCR:** dùng Tesseract 5.2.0 trong process, dữ liệu ngôn ngữ `vie+eng`
>   (`tessdata_fast`, ghim commit và SHA-256 qua `tools/Get-TesseractData.ps1`). OCR chạy
>   theo yêu cầu khi người duyệt mở ảnh, mỗi lần một trang, có timeout.
> - **Không lưu xuống DB:** kết quả chỉ cache trong bộ nhớ theo hash ảnh, hash facts và
>   phiên bản localizer. Không có migration, không đổi worker/prompt/facts/quyết định.
>   Mục "JSON phụ" bên dưới vì vậy chưa cần.
> - **Kiểm tra trước khi OCR:** ảnh phải khớp kích thước/SHA-256 đã ghi; hồ sơ không có
>   facts AI thì bỏ qua OCR; ảnh có EXIF xoay thì không định vị. Log chỉ ghi loại lỗi.
> - **Ghép chữ tất định, fail-closed:**
>   - giá trị phải xuất hiện đúng một lần;
>   - tổng tiền phải nằm cùng hàng với nhãn tổng in nguyên văn, nên dòng tổng bị phá
>     không bị chuyển sang dòng tạm tính;
>   - hàng được hiệu chỉnh theo độ nghiêng ảnh; so khớp không dấu để chịu được OCR đọc
>     sai dấu.
> - **Giao diện:** SVG overlay trong dialog xem ảnh, có bật/tắt, đúng ở cả chế độ vừa
>   khung/kích thước gốc và trên mobile, bấm hai chiều giữa trường và khung, liệt kê
>   lý do các trường "Không định vị được".
> - **Smoke trên tập phát triển** (`AURA_RUN_OCR_INTEGRATION=1`, OCR thật trên 15 ảnh
>   synthetic v3.1, facts lấy từ manifest): **57/57** trường có thể định vị đã được vẽ.
>   R3-06 (dòng tổng bị phá), R3-08 (số biên nhận bị phá) và R3-07 (không có mã) đều
>   **không** bị vẽ.
>   - Hai lỗi phát hiện trên tập này đã được sửa: ngày bị OCR tách `05/1 0/2026`, và ảnh
>     nghiêng ~2° (R3-12).
>   - Vì đã nhìn tập này khi phát triển, đây **không phải** số liệu độ chính xác; gate P3
>     bên dưới vẫn mở.
>
> Kiểm tra bằng mắt trên LocalDB tạm (R3-01 thẳng, R3-12 nghiêng, một ca privacy không có
> facts): khung trùng đúng vị trí chữ, kể cả khi đổi chế độ xem và trên mobile.

- Thêm `EvidenceRegion` dạng JSON phụ, chưa migration nếu lưu cùng facts versioned; tốt hơn là cột/bảng riêng khi cần query/audit.
- Chỉ định vị merchant, ngày, canonical identifier và final total.
- Dùng OCR word boxes + deterministic text matching; vùng không chắc chắn thì không vẽ thay vì bịa vị trí.
- SVG overlay hỗ trợ bật/tắt, zoom và click hai chiều field–region.

### P2 — hardening

- Thay warning text bằng `qualityIssues` có `code`, `affectedFields`, `severity`, `region` và `declaredRedaction`.
- Redaction nằm ngoài field trọng yếu có thể là thông tin tham khảo; redaction chạm/che date, identifier, items hoặc total vẫn fail-safe.
- Thêm audit cho version localizer, image hash và confidence; không dùng region để tự sửa facts.

### P3 — evaluation

> **Công cụ đã sẵn sàng (14/11/2026); phép đo chính thức chưa chạy.**
>
> - **Công cụ đánh dấu** [`tools/evidence_annotation`](../tools/evidence_annotation/README.md):
>   - chạy offline, có CSP chặn mọi kết nối mạng; ảnh không rời trình duyệt;
>   - người đánh dấu vẽ khung hoặc chọn "không có / không đọc được" cho 4 trường, và nhập
>     giá trị in trên ảnh;
>   - xuất `annotations.json` (`aura-evidence-annotations-v1`) kèm SHA-256 từng ảnh, và hiện
>     SHA-256 của chính file để khóa;
>   - trường chưa trả lời được xuất là `unset`, nên không thể bị tính nhầm thành ca đối chứng âm.
> - **Bộ đo opt-in** (`AURA_RUN_LOCALIZATION_EVAL=1`):
>   - kiểm định file annotation trước khi OCR, kiểm SHA-256 từng ảnh và bỏ qua ảnh có EXIF xoay;
>   - chạy OCR thật và phân loại từng trường: đúng (IoU ≥ 0,5), đặt nhầm, bỏ sót, bỏ qua đúng,
>     hoặc vẽ khi không có;
>   - tính recall, tỉ lệ khoanh nhầm và IoU trung bình; ghi báo cáo kèm SHA-256 annotation vào
>     `test_kit/results/` (Git-ignored).
> - **Gate đề xuất** (chủ dự án xác nhận trước lần chạy khóa):
>   - ít nhất 30 ảnh chưa từng dùng khi phát triển, và ít nhất 5 ca đối chứng âm;
>   - không có khung đặt nhầm ở `totalAmount`;
>   - tỉ lệ khoanh nhầm ≤ 2% và recall ≥ 80% cho mỗi trường.
> - **Đã kiểm tra đường ống:** một bộ 2 ảnh (R3-01, R3-06) với khung do trợ lý ước lượng bằng
>   mắt chạy trọn vòng. Bộ này **không phải ground truth** và không được lưu.
> - **Còn thiếu để kết luận:** một bộ ảnh mới chưa từng chạy, và một người khác người phát
>   triển đánh dấu, khóa file rồi chạy bộ đo.

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

> **Cập nhật 14/11/2026:**
> - P0 (xem ảnh trong trang) và P1 (overlay OCR local) đã có trong source, chưa publish.
> - P1 chỉ dùng **local trên Development với ảnh synthetic**; hosting giữ tắt.
> - P2 (typed quality issues) vẫn mở.
> - P3 đã có công cụ đánh dấu, bộ đo và gate đề xuất, nhưng **chưa có lần đo khóa** trên bộ
>   ảnh chưa từng chạy với người đánh dấu độc lập. Chưa đủ cơ sở để bật overlay khi demo chính
>   thức hay claim explainability.

- **GO** cho thiết kế/prototype sau khi freeze submission.
- **NO-GO** cho việc sửa pipeline hoặc prompt ngay trước demo.
- **NO-GO** cho claim explainability bằng bounding box cho đến khi có annotation và benchmark riêng.
- Addendum Three O'Clock được giữ làm evidence cho backlog typed-quality-issue; không rerun và không nới warning guard trực tiếp trên ca đó.

### Quy mô và ảnh hưởng kiến trúc

- **P0, phạm vi nhỏ:** mở ảnh lớn trong modal/panel bằng route hiện có. Đây là thay đổi reviewer UX,
  không đổi extraction, policy, queue hoặc database.
- **P1, phạm vi vừa:** thêm OCR/localizer sidecar, region contract gắn image hash, storage versioned và
  SVG overlay. Luồng quyết định vẫn giữ nguyên; localizer có thể chạy bất đồng bộ sau extraction và
  không được block AUTO/ESCALATE.
- **P2/P3, phạm vi lớn hơn:** annotation, benchmark unseen, authorization/RBAC, audit version và typed
  quality issue. Chỉ giai đoạn này mới có đủ cơ sở cân nhắc region như một quality signal.

Sequential repeat 12 ảnh ngày 13/10 không tạo word boxes hoặc ground-truth region nên không thay đổi
readiness của overlay. Nó chỉ xác nhận hai lỗi quyết định hiện tại nằm ở final-total label taxonomy và
extraction variance, không phải do thiếu lớp vẽ bounding box.

Khuyến nghị trước Chung kết: giữ prototype cô lập để trình bày roadmap hoặc Q&A; không nối production
và không dùng overlay để sửa facts/policy. Sau Chung kết, triển khai P0 trước rồi mới đo P1 trên tập
annotation riêng.

## Prototype cô lập ngày 13/10

Prototype tổng hợp, không có PII và không gọi AI đã được dựng tại
[`tools/evidence_overlay_prototype`](../tools/evidence_overlay_prototype/README.md). Prototype giữ ảnh SVG
nguồn bất biến, kiểm schema + SHA-256 trước khi render, dùng tọa độ chuẩn hóa `0..1000`, hỗ trợ click
hai chiều field–region, bàn phím, responsive và bật/tắt overlay. Negative control `tax` không có region
hiển thị “không định vị được” và không tạo hộp suy đoán. Fixture cố ý chứa cả `TAM TINH 74.000`
và `TONG THANH TOAN 58.000 VND`; region `totalAmount` chỉ trỏ dòng tổng cuối.

QA browser đã phát hiện và sửa riêng một lỗi thuộc tính `hidden` trên SVG; bản sau sửa xác nhận
`display:none` khi tắt và `display:block` khi bật. Build sạch và toàn bộ **163/163** automated test pass.
Prototype này không nối vào route ảnh, database, extraction hoặc policy; vì vậy không làm thay đổi
baseline nộp bài và chưa phải bằng chứng accuracy localization.
