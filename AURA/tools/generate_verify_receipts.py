"""Generate a deterministic 30-case AURA Test Kit and five Verify fixtures.

All output is synthetic. This script never calls an AI API.
Run: python tools/generate_verify_receipts.py --as-of-date 2026-09-21
"""

from __future__ import annotations

import argparse
import json
import random
import shutil
from dataclasses import asdict, dataclass, field
from datetime import date, timedelta
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parents[1]
KIT_ROOT = ROOT / "test_kit"
KIT_IMAGES = KIT_ROOT / "images"
VERIFY_IMAGES = ROOT / "wwwroot" / "test_data" / "images"
VERIFY_MANIFEST = ROOT / "wwwroot" / "test_data" / "expected-results.json"
SEED = 260921

FONT_CANDIDATES = [Path(r"C:\Windows\Fonts\arial.ttf"), Path(r"C:\Windows\Fonts\segoeui.ttf"),
                   Path("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf")]
BOLD_CANDIDATES = [Path(r"C:\Windows\Fonts\arialbd.ttf"), Path(r"C:\Windows\Fonts\segoeuib.ttf"),
                   Path("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf")]


@dataclass
class Case:
    id: str
    file_name: str
    expected_status: str
    claimed_amount: int
    template: str
    merchant: str
    total: int
    transaction_date: str
    items: list[tuple[str, int]]
    identifier: str | None = None
    tax_id: str | None = "0312345678"
    order_status: str | None = None
    issue: str | None = None
    tags: list[str] = field(default_factory=list)
    expected_facts: dict = field(default_factory=dict)


def resolve_font(candidates: list[Path]) -> Path:
    for candidate in candidates:
        if candidate.exists():
            return candidate
    raise FileNotFoundError("Không tìm thấy font Unicode phù hợp để sinh Test Kit.")


FONT_PATH = resolve_font(FONT_CANDIDATES)
BOLD_PATH = resolve_font(BOLD_CANDIDATES)


def font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(str(BOLD_PATH if bold else FONT_PATH), size)


def money(value: int) -> str:
    return f"{value:,}".replace(",", ".") + " đ"


def recent_weekday(as_of: date, days_back: int = 3) -> date:
    value = as_of - timedelta(days=days_back)
    while value.weekday() >= 5:
        value -= timedelta(days=1)
    return value


def previous_saturday(as_of: date) -> date:
    value = as_of
    while value.weekday() != 5:
        value -= timedelta(days=1)
    return value


def build_cases(as_of: date) -> list[Case]:
    normal_date = recent_weekday(as_of).isoformat()
    second_date = recent_weekday(as_of, 5).isoformat()
    weekend = previous_saturday(as_of).isoformat()
    stale = (as_of - timedelta(days=120)).isoformat()
    future = (as_of + timedelta(days=2)).isoformat()
    cases = [
        Case("TK-01", "TK-01-ecommerce.jpg", "AUTO_APPROVE", 295_199, "mobile", "Double Fish Việt Nam", 295_199, normal_date,
             [("Vợt bóng bàn Double Fish 4A+", 292_199), ("Bảo hiểm người tiêu dùng", 3_000)],
             "SPX-VN2693231211394", None, "Đơn hàng đã hoàn thành", tags=["ecommerce", "shipping-code", "unicode"]),
        Case("TK-02", "TK-02-thermal.jpg", "AUTO_APPROVE", 88_000, "thermal", "Phở Hai Thiền", 88_000, normal_date,
             [("Phở bò tái", 75_000), ("Trà đá", 13_000)], "HD-260921-002", "0319132712", tags=["thermal", "restaurant", "camera"]),
        Case("TK-03", "TK-03-vat.jpg", "AUTO_APPROVE", 420_000, "vat", "Công ty Văn phòng phẩm Minh Anh", 420_000, second_date,
             [("Giấy in A4", 300_000), ("Bút bi xanh", 120_000)], "AA/26E-000103", "0314567890", tags=["vat", "unicode"]),
        Case("TK-04", "TK-04-missing-tax.jpg", "ESCALATE_FACT", 210_000, "thermal", "Quán Hải Sản Ven Sông", 210_000, normal_date,
             [("Bữa ăn công tác", 210_000)], "PT-104", None, issue="missing-tax-id", tags=["missing-field", "thermal"]),
        Case("TK-05", "TK-05-policy-beer.jpg", "ESCALATE_POLICY", 780_000, "restaurant", "Nhà hàng Sài Gòn Xưa", 780_000, normal_date,
             [("Món ăn", 480_000), ("Bia Tiger x 6", 300_000)], "NH-260921-105", "0319876543", tags=["policy", "alcohol"]),
    ]
    for index, template in enumerate(["pos", "ride", "vat", "restaurant", "mobile"], start=6):
        total = 100_000 + index * 27_000
        cases.append(Case(f"TK-{index:02d}", f"TK-{index:02d}-routine.jpg", "AUTO_APPROVE", total, template,
                          f"Đơn vị dịch vụ An Tâm {index}", total, normal_date, [("Chi phí công việc", total)],
                          f"RC-2609-{index:03d}", "0312345600", "Đã thanh toán" if template == "mobile" else None,
                          tags=["routine", template]))
    fact_specs = [
        (11, "missing-identifier", normal_date, "vat"), (12, "blurred-total", normal_date, "thermal"),
        (13, "stale-date", stale, "vat"), (14, "weekend", weekend, "restaurant"),
        (15, "amount-mismatch", normal_date, "pos"), (16, "draft", normal_date, "vat"),
        (17, "returned-order", normal_date, "mobile"), (18, "delivery-date-only", "", "mobile"),
        (19, "cropped", normal_date, "thermal"), (20, "future-date", future, "ride"),
    ]
    for index, issue, tx_date, template in fact_specs:
        total = 180_000 + index * 13_000
        identifier = None if issue == "missing-identifier" else f"FACT-2609-{index:03d}"
        status = "Đã trả hàng/hoàn tiền" if issue == "returned-order" else ("Bản nháp - chưa phát hành" if issue == "draft" else None)
        claim = total + 20_000 if issue == "amount-mismatch" else total
        cases.append(Case(f"TK-{index:02d}", f"TK-{index:02d}-{issue}.jpg", "ESCALATE_FACT", claim, template,
                          f"Cửa hàng Kiểm Chứng {index}", total, tx_date, [("Chi phí cần xác minh", total)], identifier,
                          None if template == "mobile" else "0312345699", status, issue=issue,
                          tags=["fact", issue, template]))
    for offset, prohibited in enumerate(["Thuốc lá", "Vé karaoke", "Vé xem phim", "Dịch vụ massage", "Đồ dùng cá nhân"], start=21):
        total = 350_000 + (offset - 20) * 50_000
        cases.append(Case(f"TK-{offset:02d}", f"TK-{offset:02d}-policy.jpg", "ESCALATE_POLICY", total, "restaurant",
                          f"Dịch vụ Cuối Tuần {offset}", total, normal_date,
                          [("Chi phí công việc", total - 100_000), (prohibited, 100_000)], f"PL-2609-{offset:03d}",
                          "0314455667", tags=["policy", prohibited.lower()]))
    for index in range(26, 31):
        total = 1_050_000 + (index - 26) * 325_000
        cases.append(Case(f"TK-{index:02d}", f"TK-{index:02d}-authority.jpg", "ESCALATE_AUTHORITY", total, "vat",
                          f"Công ty Thiết bị Dự án {index}", total, normal_date,
                          [("Thiết bị phục vụ dự án", total)], f"AUTH-2609-{index:03d}", "0317788990",
                          tags=["authority", "over-limit"]))
    for case in cases:
        date_field = "transactionDate" if case.template in {"mobile", "ride"} else "invoiceDate"
        identifier_field = "shippingTrackingCode" if case.template == "mobile" else (
            "bookingId" if case.template == "ride" else "invoiceNumber")
        document_type = {"mobile": "ECOMMERCE", "ride": "RIDE_HAILING", "restaurant": "RESTAURANT_BILL",
                         "vat": "VAT_INVOICE", "thermal": "RETAIL_RECEIPT", "pos": "RETAIL_RECEIPT"}.get(
            case.template, "OTHER")
        case.expected_facts = {"documentType": document_type, "merchantName": case.merchant,
                               "totalAmount": case.total, date_field: case.transaction_date or None,
                               identifier_field: case.identifier}
    return cases


def draw_receipt(case: Case) -> Image.Image:
    size = (860, 1280) if case.template in {"thermal", "pos", "ride"} else (1400, 980)
    image = Image.new("RGB", size, "#f7f5ee")
    draw = ImageDraw.Draw(image)
    width, height = image.size
    margin = 54
    draw.rounded_rectangle((25, 25, width - 25, height - 25), radius=10, outline="#262626", width=3, fill="#fffef9")
    titles = {"thermal": "PHIẾU THANH TOÁN", "pos": "BIÊN NHẬN GIAO DỊCH", "ride": "BIÊN NHẬN CHUYẾN ĐI",
              "restaurant": "HÓA ĐƠN NHÀ HÀNG", "vat": "HÓA ĐƠN GIÁ TRỊ GIA TĂNG"}
    draw.text((width // 2, 65), titles.get(case.template, "HÓA ĐƠN / BIÊN NHẬN"),
              font=font(34 if width < 1000 else 40, True), fill="#111", anchor="ma")
    draw.text((margin, 135), case.merchant, font=font(28, True), fill="#161616")
    y = 190
    date_label = "Ngày giao dịch" if case.template == "ride" else "Ngày hóa đơn"
    fields = [("Mã số thuế", case.tax_id), ("Số hóa đơn/biên nhận", case.identifier),
              (date_label, case.transaction_date or None), ("Trạng thái", case.order_status), ("Tiền tệ", "VND")]
    for label, value in fields:
        if value is None:
            y += 42
            continue
        draw.text((margin, y), f"{label}: {value}", font=font(23), fill="#202020")
        y += 42
    draw.line((margin, y + 12, width - margin, y + 12), fill="#555", width=2)
    y += 45
    draw.text((margin, y), "HÀNG HÓA / DỊCH VỤ", font=font(23, True), fill="#111")
    draw.text((width - margin, y), "THÀNH TIỀN", font=font(23, True), fill="#111", anchor="ra")
    y += 52
    for description, amount in case.items:
        draw.text((margin, y), description, font=font(23), fill="#222")
        draw.text((width - margin, y), money(amount), font=font(23), fill="#222", anchor="ra")
        y += 48
    total_y = height - 165
    draw.line((margin, total_y - 25, width - margin, total_y - 25), fill="#555", width=2)
    draw.text((margin, total_y), "TỔNG THANH TOÁN", font=font(29, True), fill="#111")
    draw.text((width - margin, total_y), money(case.total), font=font(31, True), fill="#aaa" if case.issue == "blurred-total" else "#111", anchor="ra")
    draw.text((margin, height - 80), "Dữ liệu tổng hợp phục vụ kiểm thử AURA", font=font(17), fill="#666")
    if case.issue == "cropped":
        image = image.crop((0, 0, width, height - 120)).resize((width, height))
    if case.issue == "blurred-total":
        region = image.crop((width // 2, total_y - 30, width - 35, total_y + 55)).filter(ImageFilter.GaussianBlur(4.0))
        image.paste(region, (width // 2, total_y - 30))
    return image


def draw_mobile(case: Case) -> Image.Image:
    width, height = 720, 1440
    image = Image.new("RGB", (width, height), "#e7e7e7")
    draw = ImageDraw.Draw(image)
    draw.rectangle((0, 0, width, 115), fill="#fff")
    draw.text((42, 50), "‹", font=font(55), fill="#b91c1c", anchor="lm")
    draw.text((110, 55), "Thông tin đơn hàng", font=font(31, True), fill="#161616", anchor="lm")
    draw.rounded_rectangle((20, 135, width - 20, 430), radius=20, fill="#fff")
    draw.rectangle((20, 135, width - 20, 205), fill="#075c42")
    visible_status = case.order_status or ("Giao hàng thành công" if case.issue == "delivery-date-only" else "Đơn hàng đã hoàn thành")
    draw.text((45, 170), visible_status, font=font(26, True), fill="#fff", anchor="lm")
    draw.text((45, 245), "Thông tin vận chuyển", font=font(25, True), fill="#181818")
    if case.identifier:
        draw.text((45, 295), f"SPX Instant: {case.identifier}", font=font(24), fill="#181818")
    if case.transaction_date:
        draw.text((45, 345), f"Thanh toán: {case.transaction_date} 09:45", font=font(22), fill="#164e3a")
    draw.text((45, 385), "Giao hàng thành công: 2026-09-18 10:30", font=font(22), fill="#164e3a")
    draw.rounded_rectangle((20, 455, width - 20, 1060), radius=20, fill="#fff")
    draw.text((45, 510), case.merchant, font=font(27, True), fill="#151515")
    y = 585
    for description, amount in case.items:
        draw.rounded_rectangle((45, y - 10, 145, y + 90), radius=12, fill="#f1f5f9", outline="#cbd5e1")
        draw.text((165, y), description[:34], font=font(22), fill="#151515")
        draw.text((width - 50, y + 55), money(amount), font=font(23, True), fill="#151515", anchor="ra")
        y += 140
    draw.line((45, 950, width - 45, 950), fill="#d1d5db", width=2)
    draw.text((45, 985), "Thành tiền", font=font(26, True), fill="#111")
    draw.text((width - 45, 985), money(case.total), font=font(30, True), fill="#111", anchor="ra")
    draw.text((35, 1340), "Ảnh mô phỏng - không chứa dữ liệu cá nhân thật", font=font(17), fill="#555")
    return image


def photograph_effect(image: Image.Image, rng: random.Random, case: Case) -> Image.Image:
    rotated = image.rotate(rng.uniform(-1.8, 1.8), resample=Image.Resampling.BICUBIC, expand=True, fillcolor="#30343b")
    canvas = Image.new("RGB", (rotated.width + 90, rotated.height + 90), "#252932")
    shadow = Image.new("RGBA", rotated.size, (0, 0, 0, 0))
    ImageDraw.Draw(shadow).rounded_rectangle((18, 18, rotated.width - 2, rotated.height - 2), radius=18, fill=(0, 0, 0, 115))
    canvas.paste(shadow, (45, 45), shadow)
    canvas.paste(rotated, (25, 25))
    if "camera" in case.tags or case.template in {"thermal", "pos"}:
        canvas = canvas.filter(ImageFilter.GaussianBlur(rng.uniform(0.15, 0.45)))
    return canvas


def save_case(case: Case, rng: random.Random) -> None:
    image = draw_mobile(case) if case.template == "mobile" else draw_receipt(case)
    if case.template != "mobile":
        image = photograph_effect(image, rng, case)
    image.save(KIT_IMAGES / case.file_name, format="JPEG", quality=86, optimize=True, progressive=True)


def write_manifests(cases: list[Case], as_of: date) -> None:
    manifest = {"version": "2.0", "source": "synthetic-deterministic", "seed": SEED,
                "asOfDate": as_of.isoformat(),
                "notes": ["Không chứa dữ liệu cá nhân thật.", "Review ground truth trước khi công bố accuracy.",
                          "Không tự động chạy toàn bộ 30 ca trên free-tier API."],
                "cases": [asdict(case) for case in cases]}
    (KIT_ROOT / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    verify_names = ["HoaDon1.jpg", "HoaDon2.jpg", "HoaDon3.jpg", "HoaDon4.jpg", "HoaDon5.jpg"]
    verify_rows = []
    for position, (case, verify_name) in enumerate(zip(cases[:5], verify_names, strict=True), start=1):
        shutil.copy2(KIT_IMAGES / case.file_name, VERIFY_IMAGES / verify_name)
        verify_rows.append({"id": f"TC-{position:02d}", "expectedStatus": case.expected_status,
                            "imageName": verify_name, "claimedAmount": case.claimed_amount})
    VERIFY_MANIFEST.write_text(json.dumps(verify_rows, ensure_ascii=False, indent=2), encoding="utf-8")
    for old_fixture in VERIFY_IMAGES.iterdir():
        if old_fixture.is_file() and old_fixture.name not in set(verify_names):
            old_fixture.unlink()


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--as-of-date", type=date.fromisoformat, default=date.today(),
                        help="Ngày đánh giá YYYY-MM-DD dùng để sinh ngày giao dịch.")
    args = parser.parse_args()
    KIT_IMAGES.mkdir(parents=True, exist_ok=True)
    VERIFY_IMAGES.mkdir(parents=True, exist_ok=True)
    rng = random.Random(SEED)
    cases = build_cases(args.as_of_date)
    for case in cases:
        save_case(case, rng)
    write_manifests(cases, args.as_of_date)
    print(f"Generated {len(cases)} benchmark images and 5 curated Verify fixtures.")


if __name__ == "__main__":
    main()
