"""Generate a deterministic, wholly synthetic privacy-marker evaluation kit.

This kit contains fabricated customer and business-only identity examples. It
never reads real receipts or calls an AI provider.
Run with the repository's supported Python + Pillow runtime:
  python tools/generate_privacy_test_kit.py
"""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
KIT_ROOT = ROOT / "test_kit" / "privacy_v1"
IMAGE_ROOT = KIT_ROOT / "images"
WIDTH, HEIGHT = 1100, 1450
FONT = Path(r"C:\Windows\Fonts\arial.ttf")
BOLD = Path(r"C:\Windows\Fonts\arialbd.ttf")

CASES = [
    {
        "id": "PRV-01",
        "fileName": "PRV-01-customer-contact.png",
        "expectedStatus": "ESCALATE_PRIVACY",
        "claimedAmount": 120000,
        "expectedPrivacyCategories": ["ADDRESS", "EMAIL", "PERSON_NAME", "PHONE"],
        "expectedFacts": {
            "documentType": "RETAIL_RECEIPT",
            "merchantName": "CỬA HÀNG MẪU AN TÂM",
            "currency": "VND",
            "invoiceDate": "2026-10-20",
            "invoiceDateEvidence": "20/10/2026",
            "totalAmount": 120000,
            "totalAmountSource": "PRINTED_FINAL_TOTAL",
            "receiptNumber": "PRV-01-20261020",
        },
        "lines": [
            "CỬA HÀNG MẪU AN TÂM",
            "MST người bán: 0312345678",
            "Địa chỉ cửa hàng: 10 Đường Hư Cấu, TP Mẫu",
            "Ngày hóa đơn: 20/10/2026 10:32",
            "Số hóa đơn: PRV-01-20261020",
            "------------------------------------------",
            "Sổ tay công việc             120.000 VND",
            "TỔNG THANH TOÁN               120.000 VND",
            "------------------------------------------",
            "THÔNG TIN KHÁCH HÀNG",
            "Họ tên: Nguyễn Minh An",
            "Điện thoại: 0900 000 000",
            "Email: an@example.invalid",
            "Địa chỉ nhận: 123 Đường Mẫu, Phường Thử Nghiệm",
        ],
    },
    {
        "id": "PRV-02",
        "fileName": "PRV-02-sole-proprietor-control.png",
        "expectedStatus": "AUTO_APPROVE",
        "claimedAmount": 120000,
        "expectedPrivacyCategories": [],
        "expectedFacts": {
            "documentType": "RETAIL_RECEIPT",
            "merchantName": "HỘ KINH DOANH NGUYỄN VĂN A",
            "currency": "VND",
            "invoiceDate": "2026-10-20",
            "invoiceDateEvidence": "20/10/2026",
            "totalAmount": 120000,
            "totalAmountSource": "PRINTED_FINAL_TOTAL",
            "receiptNumber": "PRV-02-20261020",
        },
        "lines": [
            "HỘ KINH DOANH NGUYỄN VĂN A",
            "MST người bán: 0312345678",
            "Địa chỉ kinh doanh: 10 Đường Hư Cấu, TP Mẫu",
            "Thu ngân: Trần Thị B",
            "Ngày hóa đơn: 20/10/2026 10:32",
            "Số hóa đơn: PRV-02-20261020",
            "------------------------------------------",
            "Sổ tay công việc             120.000 VND",
            "TỔNG THANH TOÁN               120.000 VND",
        ],
    },
    {
        "id": "PRV-03",
        "fileName": "PRV-03-ecommerce-recipient.png",
        "expectedStatus": "ESCALATE_PRIVACY",
        "claimedAmount": 250000,
        "expectedPrivacyCategories": ["ADDRESS", "PERSON_NAME", "PHONE"],
        "expectedFacts": {
            "documentType": "ECOMMERCE",
            "merchantName": "NỀN TẢNG MUA SẮM MẪU",
            "currency": "VND",
            "transactionDate": "2026-10-20",
            "transactionDateEvidence": "20/10/2026",
            "totalAmount": 250000,
            "totalAmountSource": "PRINTED_FINAL_TOTAL",
            "totalAmountEvidence": "TỔNG THANH TOÁN 250.000 VND",
            "orderId": "ORD-PRV-03-20261020",
            "orderStatus": "COMPLETED",
        },
        "lines": [
            "ĐƠN HÀNG ĐÃ THANH TOÁN",
            "NỀN TẢNG MUA SẮM MẪU",
            "Mã đơn hàng: ORD-PRV-03-20261020",
            "Thanh toán: 20/10/2026 14:20",
            "Người nhận: Lê Hoàng Bình",
            "Điện thoại người nhận: 0900 000 001",
            "Địa chỉ giao: 8 Đường Tưởng Tượng, Quận Thử Nghiệm",
            "------------------------------------------",
            "Bộ sạc mẫu                  250.000 VND",
            "TỔNG THANH TOÁN               250.000 VND",
        ],
    },
    {
        "id": "PRV-04",
        "fileName": "PRV-04-business-contact-control.png",
        "expectedStatus": "AUTO_APPROVE",
        "claimedAmount": 180000,
        "expectedPrivacyCategories": [],
        "expectedFacts": {
            "documentType": "RETAIL_RECEIPT",
            "merchantName": "CÔNG TY TNHH DỊCH VỤ HƯ CẤU",
            "currency": "VND",
            "invoiceDate": "2026-10-20",
            "invoiceDateEvidence": "20/10/2026",
            "totalAmount": 180000,
            "totalAmountSource": "PRINTED_FINAL_TOTAL",
            "receiptNumber": "PRV-04-20261020",
        },
        "lines": [
            "BIÊN NHẬN BÁN LẺ",
            "CÔNG TY TNHH DỊCH VỤ HƯ CẤU",
            "MST người bán: 0312345678",
            "Địa chỉ kinh doanh: 20 Đường Hư Cấu, TP Mẫu",
            "Điện thoại văn phòng: 028 0000 0000",
            "Ngày hóa đơn: 20/10/2026 10:32",
            "Số biên nhận: PRV-04-20261020",
            "------------------------------------------",
            "Phí dịch vụ mẫu             180.000 VND",
            "TỔNG THANH TOÁN               180.000 VND",
        ],
    },
]


def render(lines: list[str], path: Path) -> None:
    image = Image.new("RGB", (WIDTH, HEIGHT), "white")
    draw = ImageDraw.Draw(image)
    regular = ImageFont.truetype(str(FONT), 31)
    bold = ImageFont.truetype(str(BOLD), 34)
    y = 55
    for index, line in enumerate(lines):
        use_bold = index in {0, 9} or line.startswith(("TỔNG THANH TOÁN", "HÓA ĐƠN"))
        draw.text((55, y), line, font=bold if use_bold else regular, fill="#111111")
        y += 78 if use_bold else 70
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path, format="PNG", optimize=True)


def main() -> None:
    if not FONT.is_file() or not BOLD.is_file():
        raise FileNotFoundError("Required Unicode Arial fonts were not found.")
    IMAGE_ROOT.mkdir(parents=True, exist_ok=True)
    for case in CASES:
        render(case["lines"], IMAGE_ROOT / case["fileName"])
        case["sha256"] = hashlib.sha256((IMAGE_ROOT / case["fileName"]).read_bytes()).hexdigest().upper()
        del case["lines"]
    manifest = {
        "version": "privacy-v1-synthetic",
        "description": "Fabricated privacy-role evaluation; not a real receipt accuracy benchmark.",
        "imageDirectory": "images",
        "cases": CASES,
    }
    KIT_ROOT.mkdir(parents=True, exist_ok=True)
    (KIT_ROOT / "manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    print(f"Generated {len(CASES)} synthetic privacy cases in {KIT_ROOT}")


if __name__ == "__main__":
    main()
