"""Generate AURA's deterministic synthetic regression Test Kit v3.

This generator intentionally leaves Test Kit v2 untouched.  It creates a
separate 15-case set under test_kit/v3 with explicit identifier labels,
controlled capture artefacts and locked SHA-256 values.  It never calls an AI
API and it never reads the private/real holdout dataset.

Run:
    python tools/generate_regression_receipts_v3.py --as-of-date 2026-10-06
"""

from __future__ import annotations

import argparse
import hashlib
import json
import random
from dataclasses import asdict, dataclass, field
from datetime import date, timedelta
from pathlib import Path

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont


ROOT = Path(__file__).resolve().parents[1]
KIT_ROOT = ROOT / "test_kit" / "v3"
IMAGE_ROOT = KIT_ROOT / "images"
SEED = 261006
GENERATOR_VERSION = "3.0.0"

FONT_CANDIDATES = [
    Path(r"C:\Windows\Fonts\arial.ttf"),
    Path(r"C:\Windows\Fonts\segoeui.ttf"),
    Path("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"),
]
BOLD_CANDIDATES = [
    Path(r"C:\Windows\Fonts\arialbd.ttf"),
    Path(r"C:\Windows\Fonts\segoeuib.ttf"),
    Path("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"),
]


@dataclass(frozen=True)
class Item:
    description: str
    quantity: int
    unit_price: int

    @property
    def amount(self) -> int:
        return self.quantity * self.unit_price


@dataclass
class Case:
    id: str
    file_name: str
    expected_status: str
    category: str
    claimed_amount: int
    document_type: str
    title: str
    merchant: str
    date_iso: str
    date_evidence: str
    items: list[Item]
    total: int
    identifier_label: str | None
    identifier_value: str | None
    identifier_field: str | None
    tax_id: str | None = None
    invoice_serial: str | None = None
    tax_authority_code: str | None = None
    pos_number: str | None = None
    terminal_id: str | None = None
    shop_id: str | None = None
    subtotal: int | None = None
    discount: int | None = None
    tax: int | None = None
    issue: str | None = None
    visual_condition: str = "clean-camera"
    additional_fields: list[tuple[str, str]] = field(default_factory=list)
    expected_facts: dict = field(default_factory=dict)
    ground_truth_rationale: str = ""
    sha256: str = ""


def resolve_font(candidates: list[Path]) -> Path:
    for candidate in candidates:
        if candidate.exists():
            return candidate
    raise FileNotFoundError("No Unicode font was found for Test Kit v3 generation.")


FONT_PATH = resolve_font(FONT_CANDIDATES)
BOLD_PATH = resolve_font(BOLD_CANDIDATES)


def font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(str(BOLD_PATH if bold else FONT_PATH), size)


def money(value: int) -> str:
    return f"{value:,}".replace(",", ".") + " đ"


def recent_weekday(as_of: date) -> date:
    value = as_of - timedelta(days=1)
    while value.weekday() >= 5:
        value -= timedelta(days=1)
    return value


def build_cases(as_of: date, revision: str = "3.0") -> list[Case]:
    tx_date = recent_weekday(as_of)
    iso = tx_date.isoformat()
    visible = tx_date.strftime("%d/%m/%Y")

    cases = [
        Case(
            "R3-01", "R3-01-vat-invoice-number.jpg", "AUTO_APPROVE", "routine", 241_920,
            "VAT_INVOICE", "HÓA ĐƠN GIÁ TRỊ GIA TĂNG", "CÔNG TY TNHH VĂN PHÒNG NAM VIỆT",
            iso, visible, [Item("Giấy in A4 80gsm", 2, 89_000), Item("Bút ký mực xanh", 2, 23_000)],
            241_920, "Số hóa đơn", "000142", "invoiceNumber", "0312468102", "1C26TNV",
            "00D7A4C921680A", subtotal=224_000, tax=17_920,
            visual_condition="flatbed-clean",
            ground_truth_rationale="Nhãn Số hóa đơn tách riêng Ký hiệu và Mã CQT; tổng cuối in rõ.",
        ),
        Case(
            "R3-02", "R3-02-restaurant-ptt.jpg", "AUTO_APPROVE", "routine", 90_000,
            "RESTAURANT_BILL", "PHIẾU TÍNH TIỀN", "BẾP NHỎ PHƯƠNG NAM", iso, visible,
            [Item("Cơm gà nướng", 1, 72_000), Item("Trà tắc", 1, 18_000)], 90_000,
            "PTT", "PTT-051026-1842", "receiptNumber", visual_condition="thermal-camera",
            ground_truth_rationale="PTT là số biên nhận in trên phiếu tính tiền, không phải mã giao dịch.",
        ),
        Case(
            "R3-03", "R3-03-pos-rrn.jpg", "AUTO_APPROVE", "routine", 158_000,
            "RETAIL_RECEIPT", "BIÊN NHẬN GIAO DỊCH", "CỬA HÀNG TIỆN LỢI MỞ 24H", iso, visible,
            [Item("Sổ tay công việc", 2, 63_000), Item("Nước suối", 2, 16_000)], 158_000,
            "RRN", "683104927615", "transactionReference", pos_number="POS-03", terminal_id="T03061",
            visual_condition="pos-camera",
            ground_truth_rationale="RRN định danh giao dịch; POS No và TID chỉ là định danh thiết bị.",
        ),
        Case(
            "R3-04", "R3-04-retail-bill-no.jpg", "AUTO_APPROVE", "routine", 185_000,
            "RETAIL_RECEIPT", "PHIẾU BÁN HÀNG", "NHÀ SÁCH HẢI ĐĂNG", iso, visible,
            [Item("Bìa hồ sơ", 5, 23_600), Item("Băng keo giấy", 2, 33_500)], 185_000,
            "Bill No", "BILL-261005-0417", "receiptNumber", tax_id="0313579246",
            visual_condition="paper-camera-shadow",
            ground_truth_rationale="Bill No là số biên nhận; mỗi nhãn chỉ gắn với một giá trị.",
        ),
        Case(
            "R3-05", "R3-05-readable-thermal-fade.jpg", "AUTO_APPROVE", "routine", 138_000,
            "RETAIL_RECEIPT", "HÓA ĐƠN BÁN LẺ", "CỬA HÀNG GIA DỤNG AN NHIÊN", iso, visible,
            [Item("Hộp lưu trữ", 1, 84_000), Item("Khăn lau bàn", 2, 18_000), Item("Túi giấy", 2, 9_000)],
            138_000, "Số biên nhận", "RCP-261005-0528", "receiptNumber",
            issue="readable-fade", visual_condition="thermal-faded-but-readable",
            ground_truth_rationale="Mực giảm tương phản nhưng toàn bộ nhãn và chữ số tổng vẫn đọc trực tiếp được.",
        ),
        Case(
            "R3-06", "R3-06-destroyed-final-total.jpg", "ESCALATE_FACT", "fact", 220_000,
            "RETAIL_RECEIPT", "PHIẾU THANH TOÁN", "TRUNG TÂM IN ẤN MINH KHÔI", iso, visible,
            [Item("In tài liệu màu", 1, 125_000), Item("Đóng gáy hồ sơ", 1, 83_000),
             Item("Giao tài liệu", 1, 26_000)], 220_000,
            "Số biên nhận", "RCP-261005-0619", "receiptNumber", subtotal=234_000, discount=14_000,
            issue="destroyed-total", visual_condition="thermal-ink-loss",
            ground_truth_rationale="Giá trị giảm giá và tổng cuối bị mất mực vật lý; không thể xác minh tổng bằng quan sát hoặc số học.",
        ),
        Case(
            "R3-07", "R3-07-device-ids-only.jpg", "ESCALATE_FACT", "fact", 268_000,
            "RETAIL_RECEIPT", "PHIẾU BÁN HÀNG", "ĐIỂM BÁN THIẾT BỊ THIÊN PHÚ", iso, visible,
            [Item("Cáp HDMI", 2, 89_000), Item("Pin AAA", 3, 30_000)], 268_000,
            None, None, None, pos_number="POS-07", terminal_id="T07009", shop_id="SHOP-204",
            issue="missing-traceable-identifier", visual_condition="pos-camera",
            ground_truth_rationale="Chỉ có Shop ID, POS No và TID; không có mã riêng của giao dịch.",
        ),
        Case(
            "R3-08", "R3-08-destroyed-receipt-number.jpg", "ESCALATE_FACT", "fact", 315_000,
            "RESTAURANT_BILL", "PHIẾU TÍNH TIỀN", "NHÀ HÀNG GÓC PHỐ XANH", iso, visible,
            [Item("Suất ăn làm việc", 3, 95_000), Item("Nước ép", 2, 15_000)], 315_000,
            "Số biên nhận", "RCP-261005-0836", "receiptNumber", pos_number="POS-08",
            issue="destroyed-identifier", visual_condition="creased-and-faded",
            ground_truth_rationale="Nhiều ký tự của số biên nhận bị mất nét; POS No không được dùng thay thế.",
        ),
        Case(
            "R3-09", "R3-09-claimed-amount-mismatch.jpg", "ESCALATE_FACT", "fact", 498_000,
            "VAT_INVOICE", "HÓA ĐƠN GIÁ TRỊ GIA TĂNG", "CÔNG TY DỊCH VỤ BẢO TÍN", iso, visible,
            [Item("Bảo trì thiết bị", 1, 430_000), Item("Vật tư thay thế", 1, 48_000)], 478_000,
            "Số hóa đơn", "000309", "invoiceNumber", tax_id="0314681357", invoice_serial="1C26TBT",
            issue="amount-mismatch", visual_condition="flatbed-clean",
            ground_truth_rationale="Tổng in 478.000 VND không khớp claimed amount 498.000 VND.",
        ),
        Case(
            "R3-10", "R3-10-policy-alcohol.jpg", "ESCALATE_POLICY", "policy", 617_000,
            "RESTAURANT_BILL", "HÓA ĐƠN NHÀ HÀNG", "NHÀ HÀNG BẾN SÔNG", iso, visible,
            [Item("Món ăn tiếp khách", 1, 425_000), Item("Bia lon", 4, 48_000)], 617_000,
            "Bill No", "BILL-261005-1034", "receiptNumber", tax_id="0315792468",
            visual_condition="restaurant-camera",
            ground_truth_rationale="Có hạng mục bia được in rõ; tổng dưới hạn mức.",
        ),
        Case(
            "R3-11", "R3-11-policy-personal-item.jpg", "ESCALATE_POLICY", "policy", 176_000,
            "RETAIL_RECEIPT", "BIÊN NHẬN GIAO DỊCH", "CỬA HÀNG CHĂM SÓC HẰNG NGÀY", iso, visible,
            [Item("Nước rửa tay văn phòng", 2, 58_000), Item("Khăn ướt chăm sóc da", 2, 30_000)], 176_000,
            "Mã giao dịch", "TXN-261005-1148", "transactionReference", pos_number="POS-11",
            visual_condition="pos-camera",
            ground_truth_rationale="Khăn ướt chăm sóc da thuộc nhóm đồ dùng cá nhân theo policy hiện hành.",
        ),
        Case(
            "R3-12", "R3-12-policy-entertainment.jpg", "ESCALATE_POLICY", "policy", 310_000,
            "RETAIL_RECEIPT", "PHIẾU THU DỊCH VỤ", "TRUNG TÂM GIẢI TRÍ ÁNH SAO", iso, visible,
            [Item("Vé xem phim", 2, 145_000), Item("Phí đặt chỗ", 2, 10_000)], 310_000,
            "Số chứng từ", "DV-261005-1207", "documentNumber", pos_number="POS-12",
            visual_condition="paper-camera-shadow",
            ground_truth_rationale="Vé xem phim là hạng mục policy; số chứng từ không giả làm mã giao dịch.",
        ),
        Case(
            "R3-13", "R3-13-authority-vat.jpg", "ESCALATE_AUTHORITY", "authority", 1_050_000,
            "VAT_INVOICE", "HÓA ĐƠN GIÁ TRỊ GIA TĂNG", "CÔNG TY THIẾT BỊ DỰ ÁN ĐÔNG NAM", iso, visible,
            [Item("Bộ trình chiếu di động", 1, 1_050_000)], 1_050_000,
            "Số hóa đơn", "000513", "invoiceNumber", tax_id="0316803579", invoice_serial="1C26TDN",
            visual_condition="flatbed-clean",
            ground_truth_rationale="Chứng từ hợp lệ nhưng tổng vượt hạn mức 1.000.000 VND.",
        ),
        Case(
            "R3-14", "R3-14-authority-receipt.jpg", "ESCALATE_AUTHORITY", "authority", 1_680_000,
            "RETAIL_RECEIPT", "PHIẾU BÁN HÀNG", "TRUNG TÂM ĐIỆN MÁY HÒA PHÁT", iso, visible,
            [Item("Màn hình trình bày", 1, 1_520_000), Item("Cáp kết nối", 2, 80_000)], 1_680_000,
            "Receipt No", "REC-261005-1455", "receiptNumber", pos_number="POS-14",
            visual_condition="wide-paper-camera",
            ground_truth_rationale="Receipt No tách khỏi POS No; tổng vượt hạn mức.",
        ),
        Case(
            "R3-15", "R3-15-authority-transaction.jpg", "ESCALATE_AUTHORITY", "authority", 2_350_000,
            "RETAIL_RECEIPT", "BIÊN NHẬN GIAO DỊCH", "CỬA HÀNG MÁY TÍNH THÀNH CÔNG", iso, visible,
            [Item("Thiết bị lưu trữ dự án", 2, 1_125_000), Item("Phí cài đặt", 1, 100_000)], 2_350_000,
            "Trace", "TRC-261005-1508", "transactionReference", pos_number="POS-15", terminal_id="T15008",
            visual_condition="pos-camera",
            ground_truth_rationale="Trace là mã giao dịch; tổng vượt hạn mức.",
        ),
    ]

    if revision == "3.1":
        # V3 raw evidence exposed an objective fixture error in R3-12: a supporting
        # Số chứng từ alone cannot satisfy the traceable paper-identifier rule, so
        # FACT would outrank POLICY.  V3.1 keeps that supporting field and adds a
        # distinct receipt number.  V3 remains immutable and reproducible.
        policy_case = next(case for case in cases if case.id == "R3-12")
        policy_case.identifier_label = "Số biên nhận"
        policy_case.identifier_value = "RCP-261005-1207"
        policy_case.identifier_field = "receiptNumber"
        policy_case.additional_fields = [("Số chứng từ", "DV-261005-1207")]
        policy_case.ground_truth_rationale = (
            "Vé xem phim là hạng mục policy; Số biên nhận và Số chứng từ được in thành hai trường riêng."
        )

    for case in cases:
        total_visible = case.issue != "destroyed-total"
        identifier_visible = case.identifier_value if case.issue != "destroyed-identifier" else None
        facts: dict[str, object | None] = {
            "evidenceContractVersion": 2,
            "documentType": case.document_type,
            "merchantName": case.merchant,
            "currency": "VND",
            "invoiceDate": case.date_iso,
            "invoiceDateEvidence": case.date_evidence,
            "totalAmount": case.total if total_visible else None,
            "totalAmountSource": "PRINTED_FINAL_TOTAL" if total_visible else "NOT_VISIBLE",
        }
        if case.identifier_field:
            facts[case.identifier_field] = identifier_visible
        if case.pos_number:
            facts["posNumber"] = case.pos_number
        if case.invoice_serial:
            facts["invoiceSerial"] = case.invoice_serial
        if case.tax_authority_code:
            facts["taxAuthorityCode"] = case.tax_authority_code
        for label, value in case.additional_fields:
            if label == "Số chứng từ":
                facts["documentNumber"] = value
        case.expected_facts = facts
    return cases


def draw_wrapped(draw: ImageDraw.ImageDraw, xy: tuple[int, int], text: str, width: int, *, size: int = 27,
                 bold: bool = False, fill: str = "#181818") -> None:
    value = text if len(text) <= width else text[: width - 1] + "…"
    draw.text(xy, value, font=font(size, bold), fill=fill)


def paint_thermal_loss(image: Image.Image, box: tuple[int, int, int, int], rng: random.Random,
                       *, coverage: float) -> None:
    """Erase irregular horizontal/vertical fragments instead of applying cosmetic blur."""
    draw = ImageDraw.Draw(image)
    left, top, right, bottom = box
    draw.rectangle(box, fill="#fffef9")
    # Keep a few pale, disconnected ink fragments.  They are intentionally too
    # incomplete to reconstruct a glyph in the destroyed cases.
    fragments = max(3, int((right - left) * (1.0 - coverage) / 10))
    for _ in range(fragments):
        x = rng.randint(left, max(left, right - 9))
        y = rng.randint(top, max(top, bottom - 3))
        draw.rectangle((x, y, min(right, x + rng.randint(3, 10)), min(bottom, y + rng.randint(1, 3))),
                       fill=rng.choice(["#c8c6bf", "#d7d5ce", "#e2e0da"]))


def apply_readable_fade(image: Image.Image, box: tuple[int, int, int, int]) -> None:
    layer = Image.new("RGBA", image.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer)
    left, top, right, bottom = box
    for x in range(left + 3, right, 17):
        draw.line((x, top, x, bottom), fill=(255, 254, 249, 105), width=2)
    for y in range(top + 5, bottom, 13):
        draw.line((left, y, right, y), fill=(255, 254, 249, 48), width=1)
    image.alpha_composite(layer)


def draw_receipt(case: Case, rng: random.Random) -> Image.Image:
    width, height = (980, 1380)
    paper = Image.new("RGBA", (width, height), "#fffef9")
    draw = ImageDraw.Draw(paper)
    margin = 62
    draw.rounded_rectangle((22, 22, width - 22, height - 22), radius=9, outline="#292929", width=3,
                           fill="#fffef9")

    draw.text((width // 2, 70), case.title, font=font(35, True), fill="#111", anchor="ma")
    draw_wrapped(draw, (margin, 133), case.merchant, 47, size=27, bold=True)
    y = 184

    fields: list[tuple[str, str | None]] = []
    if case.tax_id:
        fields.append(("Mã số thuế", case.tax_id))
    if case.invoice_serial:
        fields.append(("Ký hiệu", case.invoice_serial))
    if case.tax_authority_code:
        fields.append(("Mã CQT", case.tax_authority_code))
    if case.identifier_label and case.identifier_value:
        fields.append((case.identifier_label, case.identifier_value))
    fields.extend(case.additional_fields)
    if case.shop_id:
        fields.append(("Shop ID", case.shop_id))
    if case.pos_number:
        fields.append(("POS No", case.pos_number))
    if case.terminal_id:
        fields.append(("TID", case.terminal_id))
    fields.extend([("Ngày hóa đơn", case.date_evidence), ("Giờ", "18:42"), ("Tiền tệ", "VND")])

    identifier_box: tuple[int, int, int, int] | None = None
    discount_box: tuple[int, int, int, int] | None = None
    for label, value in fields:
        if value is None:
            continue
        draw.text((margin, y), f"{label}: {value}", font=font(22), fill="#202020")
        if label == case.identifier_label:
            label_width = int(draw.textlength(f"{label}: ", font=font(22)))
            identifier_box = (margin + label_width, y - 2, width - margin, y + 29)
        y += 37

    table_top = max(y + 17, 405)
    draw.line((margin, table_top, width - margin, table_top), fill="#555", width=2)
    y = table_top + 25
    draw.text((margin, y), "HÀNG HÓA / DỊCH VỤ", font=font(21, True), fill="#111")
    draw.text((600, y), "SL", font=font(21, True), fill="#111", anchor="ra")
    draw.text((745, y), "ĐƠN GIÁ", font=font(21, True), fill="#111", anchor="ra")
    draw.text((width - margin, y), "THÀNH TIỀN", font=font(21, True), fill="#111", anchor="ra")
    y += 45
    for item in case.items:
        draw_wrapped(draw, (margin, y), item.description, 29, size=21)
        draw.text((600, y), str(item.quantity), font=font(21), fill="#222", anchor="ra")
        draw.text((745, y), money(item.unit_price), font=font(21), fill="#222", anchor="ra")
        draw.text((width - margin, y), money(item.amount), font=font(21), fill="#222", anchor="ra")
        y += 43

    summary_y = max(y + 24, height - 285)
    draw.line((margin, summary_y - 17, width - margin, summary_y - 17), fill="#555", width=2)
    if case.subtotal is not None:
        draw.text((margin, summary_y), "TẠM TÍNH", font=font(22), fill="#222")
        draw.text((width - margin, summary_y), money(case.subtotal), font=font(22), fill="#222", anchor="ra")
        summary_y += 39
    if case.discount is not None:
        draw.text((margin, summary_y), "GIẢM GIÁ", font=font(22), fill="#222")
        draw.text((width - margin, summary_y), "-" + money(case.discount), font=font(22), fill="#222", anchor="ra")
        discount_box = (width - 300, summary_y - 3, width - margin + 5, summary_y + 31)
        summary_y += 39
    if case.tax is not None:
        draw.text((margin, summary_y), "THUẾ GTGT", font=font(22), fill="#222")
        draw.text((width - margin, summary_y), money(case.tax), font=font(22), fill="#222", anchor="ra")
        summary_y += 39

    total_y = summary_y + 9
    draw.line((margin, total_y - 16, width - margin, total_y - 16), fill="#555", width=2)
    draw.text((margin, total_y), "TỔNG THANH TOÁN", font=font(29, True), fill="#111")
    total_fill = "#696969" if case.issue == "readable-fade" else "#111"
    draw.text((width - margin, total_y), money(case.total), font=font(31, True), fill=total_fill, anchor="ra")
    total_box = (width - 345, total_y - 5, width - margin + 7, total_y + 39)
    draw.text((margin, height - 68), "Cảm ơn quý khách!", font=font(17), fill="#666")

    if case.issue == "readable-fade":
        apply_readable_fade(paper, total_box)
    elif case.issue == "destroyed-total":
        paint_thermal_loss(paper, total_box, rng, coverage=0.91)
        if discount_box:
            paint_thermal_loss(paper, discount_box, rng, coverage=0.88)
    elif case.issue == "destroyed-identifier" and identifier_box:
        left, top, right, bottom = identifier_box
        # Preserve only the first prefix fragment and a few disconnected marks.
        paint_thermal_loss(paper, (left + 38, top, right, bottom), rng, coverage=0.90)

    return paper.convert("RGB")


def add_capture_effect(receipt: Image.Image, rng: random.Random, condition: str) -> Image.Image:
    angle = rng.uniform(-1.7, 1.7)
    rotated = receipt.rotate(angle, resample=Image.Resampling.BICUBIC, expand=True, fillcolor="#f2f0e8")
    canvas = Image.new("RGB", (rotated.width + 150, rotated.height + 140), "#30343b")
    shadow = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    shadow_draw = ImageDraw.Draw(shadow)
    shadow_draw.rounded_rectangle((91, 82, 91 + rotated.width, 82 + rotated.height), radius=18,
                                  fill=(0, 0, 0, 105))
    shadow = shadow.filter(ImageFilter.GaussianBlur(13))
    canvas.paste(shadow.convert("RGB"), (0, 0))
    canvas.paste(rotated, (48, 37))

    # Deterministic lighting gradient and a faint fold/thermal band.  These are
    # deliberately bounded so clean facts remain readable.
    overlay = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    overlay_draw = ImageDraw.Draw(overlay)
    if "shadow" in condition or "restaurant" in condition:
        for x in range(0, 190):
            alpha = int(42 * (1 - x / 190))
            overlay_draw.line((x, 0, x, canvas.height), fill=(20, 24, 31, alpha))
    if "thermal" in condition or "pos" in condition:
        band_y = rng.randint(570, 930)
        overlay_draw.rectangle((45, band_y, canvas.width - 50, band_y + 3), fill=(120, 120, 115, 18))
    canvas = Image.alpha_composite(canvas.convert("RGBA"), overlay).convert("RGB")
    canvas = ImageEnhance.Contrast(canvas).enhance(0.98)
    return canvas.filter(ImageFilter.GaussianBlur(rng.uniform(0.08, 0.28)))


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def save_case(case: Case, rng: random.Random) -> None:
    image = draw_receipt(case, rng)
    image = add_capture_effect(image, rng, case.visual_condition)
    image.save(IMAGE_ROOT / case.file_name, "JPEG", quality=88, optimize=True, progressive=True)


def case_for_manifest(case: Case) -> dict:
    value = asdict(case)
    if not value["additional_fields"]:
        value.pop("additional_fields")
    value["items"] = [
        {"description": item.description, "quantity": item.quantity, "unitPrice": item.unit_price,
         "amount": item.amount}
        for item in case.items
    ]
    return value


def write_manifests(cases: list[Case], as_of: date, revision: str = "3.0") -> None:
    is_revision = revision == "3.1"
    manifest = {
        "version": "regression-15-v3.1" if is_revision else "regression-15-v3",
        "generatorVersion": "3.1.0" if is_revision else GENERATOR_VERSION,
        "source": "synthetic-deterministic-pillow",
        "seed": SEED,
        "asOfDate": as_of.isoformat(),
        "datasetRole": "post-holdout-regression-alternative",
        "privacy": "No real receipt pixels, merchant identities or personal transaction data.",
        "limitations": [
            "Synthetic regression data is not production accuracy evidence.",
            "Test Kit v2 and its raw benchmark remain unchanged.",
            "A future unseen real dataset is required for any new blind claim.",
        ],
        "cases": [case_for_manifest(case) for case in cases],
    }
    manifest_path = KIT_ROOT / "manifest.json"
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    judge = {
        "version": "judge-15-v3.1" if is_revision else "judge-15-v3",
        "description": "Locked synthetic post-holdout regression set; not a blind real-world accuracy claim.",
        "sourceManifest": "manifest.json",
        "imageDirectory": "images",
        "cases": [
            {
                "id": case.id,
                "fileName": case.file_name,
                "sha256": case.sha256,
                "expectedStatus": case.expected_status,
                "claimedAmount": case.claimed_amount,
                "category": case.category,
                "expectedFacts": case.expected_facts,
                "groundTruthRationale": case.ground_truth_rationale,
            }
            for case in cases
        ],
    }
    judge_path = KIT_ROOT / "judge-manifest.json"
    judge_path.write_text(json.dumps(judge, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    hash_lines = [
        f"{sha256(manifest_path)}  manifest.json",
        f"{sha256(judge_path)}  judge-manifest.json",
    ]
    hash_lines.extend(f"{case.sha256}  images/{case.file_name}" for case in cases)
    (KIT_ROOT / "SHA256SUMS.txt").write_text("\n".join(hash_lines) + "\n", encoding="ascii")


def write_contact_sheet(cases: list[Case]) -> None:
    thumb_w, thumb_h = 260, 365
    sheet = Image.new("RGB", (thumb_w * 5, (thumb_h + 44) * 3), "#20242b")
    draw = ImageDraw.Draw(sheet)
    for index, case in enumerate(cases):
        row, column = divmod(index, 5)
        image = Image.open(IMAGE_ROOT / case.file_name).convert("RGB")
        image.thumbnail((thumb_w - 18, thumb_h - 15), Image.Resampling.LANCZOS)
        x = column * thumb_w + (thumb_w - image.width) // 2
        y = row * (thumb_h + 44) + 5
        sheet.paste(image, (x, y))
        draw.text((column * thumb_w + 10, y + thumb_h),
                  f"{case.id}  {case.expected_status}", font=font(15, True), fill="#f4f4f5")
    sheet.save(KIT_ROOT / "contact-sheet.jpg", "JPEG", quality=90, optimize=True)


def main() -> None:
    global KIT_ROOT, IMAGE_ROOT
    parser = argparse.ArgumentParser()
    parser.add_argument("--as-of-date", type=date.fromisoformat, required=True)
    parser.add_argument("--revision", choices=["3.0", "3.1"], default="3.0")
    args = parser.parse_args()

    if args.revision == "3.1":
        KIT_ROOT = ROOT / "test_kit" / "v3_1"
        IMAGE_ROOT = KIT_ROOT / "images"

    KIT_ROOT.mkdir(parents=True, exist_ok=True)
    IMAGE_ROOT.mkdir(parents=True, exist_ok=True)
    for stale in IMAGE_ROOT.glob("R3-*.jpg"):
        stale.unlink()

    cases = build_cases(args.as_of_date, args.revision)
    if len(cases) != 15:
        raise RuntimeError("Test Kit v3 must contain exactly 15 cases.")

    rng = random.Random(SEED)
    for case in cases:
        save_case(case, rng)
        case.sha256 = sha256(IMAGE_ROOT / case.file_name)

    write_manifests(cases, args.as_of_date, args.revision)
    write_contact_sheet(cases)
    print(f"Generated {len(cases)} locked synthetic regression cases under {KIT_ROOT}")


if __name__ == "__main__":
    main()
