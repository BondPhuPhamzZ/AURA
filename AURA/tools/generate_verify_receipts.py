"""Generate the five synthetic, non-personal Verify Harness receipt fixtures."""

from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "wwwroot" / "test_data" / "images"
FONT_PATH = Path(r"C:\Windows\Fonts\arial.ttf")
BOLD_PATH = Path(r"C:\Windows\Fonts\arialbd.ttf")


def font(size: int, bold: bool = False):
    return ImageFont.truetype(str(BOLD_PATH if bold else FONT_PATH), size)


def receipt(filename: str, title: str, fields: list[tuple[str, str]], items: list[tuple[str, str]], total: str):
    image = Image.new("RGB", (1200, 850), "white")
    draw = ImageDraw.Draw(image)
    draw.rectangle((30, 30, 1170, 820), outline="#222222", width=3)
    draw.text((600, 70), title, font=font(38, True), fill="#111111", anchor="ma")
    y = 135
    for label, value in fields:
        draw.text((80, y), f"{label}: {value}", font=font(25), fill="#111111")
        y += 42
    y += 20
    draw.line((70, y, 1130, y), fill="#333333", width=2)
    y += 25
    draw.text((90, y), "HANG HOA / DICH VU", font=font(24, True), fill="#111111")
    draw.text((1050, y), "THANH TIEN", font=font(24, True), fill="#111111", anchor="ra")
    y += 50
    for description, amount in items:
        draw.text((90, y), description, font=font(25), fill="#111111")
        draw.text((1050, y), amount, font=font(25), fill="#111111", anchor="ra")
        y += 48
    y = 705
    draw.line((70, y, 1130, y), fill="#333333", width=2)
    draw.text((740, y + 35), "TONG THANH TOAN (VND):", font=font(28, True), fill="#111111", anchor="ra")
    draw.text((1080, y + 35), total, font=font(32, True), fill="#111111", anchor="ra")
    image.save(OUTPUT / filename, optimize=True)


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    receipt("HoaDon1.png", "GRAB E-RECEIPT", [
        ("Merchant", "GRAB VIETNAM"), ("Tax ID", "0312650437"), ("Receipt No", "GRAB-0001"),
        ("Trip ID", "ADR-260918-001"), ("Invoice date", "2026-09-18"),
        ("Invoice time", "09:15"), ("Currency", "VND")],
        [("Business taxi trip", "150,000")], "150,000")
    receipt("HoaDon2.jpg", "HOA DON GTGT - PHO 24", [
        ("Don vi ban", "CONG TY PHO 24"), ("Ma so thue", "0309132712"),
        ("So hoa don", "AA/26E-000002"), ("Ngay hoa don", "2026-09-18"),
        ("Gio", "12:30"), ("Tien te", "VND")],
        [("Pho bo", "70,000"), ("Nuoc suoi", "10,000")], "80,000")
    receipt("HoaDon3.png", "HOA DON VAN PHONG PHAM", [
        ("Don vi ban", "AURA STATIONERY"), ("Ma so thue", "0312345678"),
        ("So hoa don", "AA/26E-000003"), ("Ngay hoa don", "2026-09-17"),
        ("Gio", "14:00"), ("Tien te", "VND")],
        [("Giay in A4", "250,000"), ("But viet", "100,000")], "350,000")
    receipt("HoaDon4.jpg", "PHIEU THU VIET TAY", [
        ("Nguoi ban", "QUAN HAI SAN NHO"), ("Ma so thue", "KHONG CO"),
        ("So phieu", "PT-004"), ("Ngay", "2026-09-18"), ("Tien te", "VND")],
        [("Bua an", "200,000")], "200,000")
    receipt("HoaDon5.jpg", "HOA DON NHA HANG", [
        ("Don vi ban", "AURA RESTAURANT"), ("Ma so thue", "0312345679"),
        ("So hoa don", "AA/26E-000005"), ("Ngay hoa don", "2026-09-18"),
        ("Gio", "19:00"), ("Tien te", "VND")],
        [("Mon an", "450,000"), ("Tiger Beer x 10", "400,000")], "850,000")


if __name__ == "__main__":
    main()
