from pathlib import Path

from docx import Document
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_CELL_VERTICAL_ALIGNMENT
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Cm, Pt, RGBColor


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "submission" / "AURA_BUILD_LOG.docx"


def set_cell_shading(cell, fill: str) -> None:
    tc_pr = cell._tc.get_or_add_tcPr()
    shd = OxmlElement("w:shd")
    shd.set(qn("w:fill"), fill)
    tc_pr.append(shd)


def set_cell_margins(cell, top=90, start=120, bottom=90, end=120) -> None:
    tc = cell._tc
    tc_pr = tc.get_or_add_tcPr()
    tc_mar = tc_pr.first_child_found_in("w:tcMar")
    if tc_mar is None:
        tc_mar = OxmlElement("w:tcMar")
        tc_pr.append(tc_mar)
    for margin, value in (("top", top), ("start", start), ("bottom", bottom), ("end", end)):
        node = tc_mar.find(qn(f"w:{margin}"))
        if node is None:
            node = OxmlElement(f"w:{margin}")
            tc_mar.append(node)
        node.set(qn("w:w"), str(value))
        node.set(qn("w:type"), "dxa")


def add_bullet(document: Document, text: str) -> None:
    paragraph = document.add_paragraph(style="List Bullet")
    paragraph.paragraph_format.space_after = Pt(2)
    paragraph.paragraph_format.line_spacing = 1.0
    run = paragraph.add_run(text)
    run.font.size = Pt(10)


document = Document()
section = document.sections[0]
section.page_width = Cm(21)
section.page_height = Cm(29.7)
section.top_margin = Cm(1.15)
section.bottom_margin = Cm(1.05)
section.left_margin = Cm(1.35)
section.right_margin = Cm(1.35)

styles = document.styles
styles["Normal"].font.name = "Arial"
styles["Normal"].font.size = Pt(10)
styles["Normal"].paragraph_format.space_after = Pt(2)

title = document.add_paragraph()
title.alignment = WD_ALIGN_PARAGRAPH.CENTER
title.paragraph_format.space_after = Pt(2)
run = title.add_run("AURA — BUILD LOG SPRINT 1")
run.bold = True
run.font.name = "Arial"
run.font.size = Pt(18)
run.font.color.rgb = RGBColor(25, 68, 145)

subtitle = document.add_paragraph()
subtitle.alignment = WD_ALIGN_PARAGRAPH.CENTER
subtitle.paragraph_format.space_after = Pt(7)
run = subtitle.add_run("Track A: The Escalation Referee  •  22/09/2026  •  1 trang")
run.italic = True
run.font.size = Pt(8.5)
run.font.color.rgb = RGBColor(88, 100, 120)

summary = document.add_table(rows=1, cols=3)
summary.autofit = False
summary.columns[0].width = Cm(5.8)
summary.columns[1].width = Cm(5.8)
summary.columns[2].width = Cm(5.8)
for cell, heading, value in zip(
    summary.rows[0].cells,
    ("OFFLINE TEST", "VERIFY V2 LOCAL", "QUYẾT ĐỊNH KIẾN TRÚC"),
    ("54/54 PASS", "5/5 — người dùng xác nhận", "AI đọc • C# quyết định"),
):
    set_cell_shading(cell, "EAF1FF")
    set_cell_margins(cell)
    cell.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
    p = cell.paragraphs[0]
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p.paragraph_format.space_after = Pt(1)
    r = p.add_run(heading + "\n")
    r.bold = True
    r.font.size = Pt(7.5)
    r.font.color.rgb = RGBColor(72, 89, 116)
    r = p.add_run(value)
    r.bold = True
    r.font.size = Pt(10)
    r.font.color.rgb = RGBColor(25, 68, 145)


def heading(text: str) -> None:
    paragraph = document.add_paragraph()
    paragraph.paragraph_format.space_before = Pt(6)
    paragraph.paragraph_format.space_after = Pt(2)
    run = paragraph.add_run(text)
    run.bold = True
    run.font.size = Pt(11.5)
    run.font.color.rgb = RGBColor(25, 68, 145)


heading("1. Công cụ AI và cách dùng")
add_bullet(document, "Qwen3-VL-8B-Instruct qua OpenRouter trích xuất dữ kiện hóa đơn theo JSON Schema; model không được quyền duyệt hoặc từ chối hồ sơ.")
add_bullet(document, "Codex và Antigravity hỗ trợ đọc code, truy route, refactor, viết test và tài liệu. Mọi đề xuất vẫn được kiểm bằng build, test và thao tác UI trước khi chấp nhận.")

heading("2. Điều tạo ra giá trị")
add_bullet(document, "Tách Vision khỏi PolicyDecisionEngine giúp quyết định FACT → POLICY → AUTHORITY giải thích được, kiểm thử được và không đổi theo cảm hứng của model.")
add_bullet(document, "54/54 test offline khóa policy, workflow, audit projection, hợp đồng OpenRouter và tính toàn vẹn Test Kit; không tốn request AI.")
add_bullet(document, "Verify v2 gồm 5 ảnh tổng hợp đa layout; người dùng xác nhận local đạt đúng 3 AUTO_APPROVE + 2 ESCALATE ngày 22/09/2026. Gói BGK có đúng 15 ca trong ngân hàng 30 ảnh.")
add_bullet(document, "UI cập nhật bằng fetch/fragment thay vì reload; Audit giữ event gốc nhưng chỉ hiện một hồ sơ với timeline mở rộng, tránh cảm giác lặp ba dòng.")

heading("3. Khi AI sai hoặc làm tốn công")
add_bullet(document, "Gemini Free Tier chạm quota; endpoint/model Qwen cấu hình sai từng trả 404; output JSON có lúc lệch schema. Hệ thống phải map lỗi rõ và fail-safe sang kiểm tra thủ công.")
add_bullet(document, "Fixture từng chứa câu mô tả tổng hợp bị model xem là prompt injection. Bộ ảnh và prompt đã được chỉnh để test đúng nghiệp vụ thay vì vô tình test watermark hướng dẫn.")
add_bullet(document, "AURA không tuyên bố exponential backoff hay circuit breaker: mã hiện chỉ retry tối đa một lần cho 5xx, không retry 429 và dừng phần Verify còn lại khi gặp lỗi provider mang tính hệ thống.")

heading("4. Cắt giảm lớn nhất và lý do")
add_bullet(document, "Không self-host Qwen 4B và không dùng model 125B sát deadline. Bản demo dùng hosted 8B; 4B là hướng benchmark khi doanh nghiệp có phần cứng hạn chế.")
add_bullet(document, "Hoãn PDF/nhiều trang, tax/e-invoice lookup, ngoại tệ, antivirus và authentication theo role để bảo đảm một vertical slice chạy thật: upload → extract → policy → human decision → audit/undo.")

note = document.add_paragraph()
note.paragraph_format.space_before = Pt(5)
note.paragraph_format.space_after = Pt(0)
note.alignment = WD_ALIGN_PARAGRAPH.CENTER
run = note.add_run("Minh bạch: Test Kit là dữ liệu tổng hợp; 5/5 không phải độ chính xác tổng quát. Ảnh upload được gửi qua OpenRouter/provider Qwen.")
run.bold = True
run.font.size = Pt(8)
run.font.color.rgb = RGBColor(144, 82, 20)

OUTPUT.parent.mkdir(parents=True, exist_ok=True)
document.save(OUTPUT)
print(OUTPUT)
