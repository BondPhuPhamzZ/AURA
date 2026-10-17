import fs from "node:fs/promises";
import path from "node:path";
import { pathToFileURL } from "node:url";

const { SKILL_DIR, TMP_DIR, WORKSPACE_DIR, FINAL_PPTX, RUNTIME_PYTHON, NODE_MODULES_DIR } = process.env;
for (const [name, value] of Object.entries({ SKILL_DIR, TMP_DIR, WORKSPACE_DIR, FINAL_PPTX, RUNTIME_PYTHON, NODE_MODULES_DIR })) {
  if (!value || !path.isAbsolute(value)) throw new Error(`${name} must be an absolute path`);
}

const { Presentation, PresentationFile } = await import(
  pathToFileURL(path.join(NODE_MODULES_DIR, "@oai/artifact-tool/dist/artifact_tool.mjs")).href,
);
const { finalizePresentation } = await import(
  pathToFileURL(path.join(SKILL_DIR, "container_tools/artifact_tool_utils.mjs")).href,
);

await fs.mkdir(TMP_DIR, { recursive: true });
await fs.mkdir(path.dirname(FINAL_PPTX), { recursive: true });

const deck = Presentation.create({ slideSize: { width: 1280, height: 720 } });
const FONT = "Segoe UI";
deck.theme.defaultFont = FONT;
deck.theme.colorScheme = {
  name: "AURA restrained navy",
  themeColors: {
    accent1: "#5F8FD8",
    accent2: "#45A9BE",
    accent3: "#C59A4A",
    accent4: "#4FAF91",
    accent5: "#162A42",
    accent6: "#31445C",
    bg1: "#091522",
    bg2: "#112338",
    tx1: "#F4F7FB",
    tx2: "#A6B3C4",
    dk1: "#091522",
    dk2: "#162A42",
    lt1: "#F4F7FB",
    lt2: "#A6B3C4",
    hlink: "#5F8FD8",
    folHlink: "#45A9BE",
  },
};

const C = { bg: "#091522", card: "#112338", cardAccent: "#162A42", line: "#31445C", text: "#F4F7FB", muted: "#A6B3C4", blue: "#5F8FD8", teal: "#45A9BE", amber: "#C59A4A", green: "#4FAF91" };
const PAGE_W = 1280;
const M = 58;
const CONTENT_W = PAGE_W - M * 2;

function nameOf(element, name) { element.name = name; return element; }

function addShape(surface, name, x, y, w, h, fill = C.card, line = C.line, radius = 6, lineStyle = "solid") {
  return nameOf(surface.shapes.add({ geometry: "rect", position: { left: x, top: y, width: w, height: h }, fill, line: { style: lineStyle, fill: line, width: 1 }, borderRadius: radius }), name);
}

function addText(surface, name, value, x, y, w, h, size = 16, color = C.text, opts = {}) {
  const box = nameOf(surface.shapes.add({ geometry: "textbox", position: { left: x, top: y, width: w, height: h }, fill: "none", line: { fill: "none", width: 0 } }), name);
  box.text = value;
  box.text.style = { typeface: FONT, fontSize: size, bold: Boolean(opts.bold), color, alignment: opts.align ?? "left", verticalAlignment: opts.valign ?? "top", autoFit: "none", insets: opts.insets ?? { top: 0, right: 0, bottom: 0, left: 0 } };
  return box;
}

function addLine(surface, name, x, y, w, h, color = C.line, width = 1) {
  return nameOf(surface.shapes.add({ geometry: "line", position: { left: x, top: y, width: w, height: h }, fill: "none", line: { style: "solid", fill: color, width } }), name);
}

function addPill(surface, name, label, x, y, w, fill, textColor = C.bg, outline = fill) {
  const pill = addShape(surface, name, x, y, w, 28, fill, outline, 6);
  pill.text = label;
  pill.text.style = { typeface: FONT, fontSize: 12, bold: true, color: textColor, alignment: "center", verticalAlignment: "middle", autoFit: "none", insets: { top: 0, right: 5, bottom: 0, left: 5 } };
  return pill;
}

function addIndexIcon(surface, name, label, x, y, color) {
  const ring = nameOf(surface.shapes.add({ geometry: "ellipse", position: { left: x, top: y, width: 34, height: 34 }, fill: "none", line: { style: "solid", fill: color, width: 1.5 } }), name);
  ring.text = label;
  ring.text.style = { typeface: FONT, fontSize: 12, bold: true, color, alignment: "center", verticalAlignment: "middle", autoFit: "none", insets: { top: 0, right: 0, bottom: 0, left: 0 } };
  return ring;
}

function addPersonIcon(surface, name, x, y, color) {
  nameOf(surface.shapes.add({ geometry: "ellipse", position: { left: x + 11, top: y + 3, width: 10, height: 10 }, fill: "none", line: { style: "solid", fill: color, width: 1.5 } }), `${name}_head`);
  addLine(surface, `${name}_body`, x + 16, y + 14, 0, 13, color, 1.5);
  addLine(surface, `${name}_arms`, x + 7, y + 19, 18, 0, color, 1.5);
  addLine(surface, `${name}_left_leg`, x + 9, y + 27, 7, 8, color, 1.5);
  addLine(surface, `${name}_right_leg`, x + 16, y + 27, 7, 8, color, 1.5);
}

function createLayout(pageNumber, specialFooter = false) {
  const layout = deck.layouts.add(`AURA_CONTENT_${pageNumber}`);
  addLine(layout, "Layout_Divider", M, 116, CONTENT_W, 0, C.line, 1);
  addText(layout, "Layout_Footer", "AURA • Track VNG • 17/10/2026 • AFTER FEEDBACK", M, 686, 620, 16, 10, C.muted);
  if (specialFooter) addText(layout, "Layout_LimitFooter", "Giới hạn hiện tại, không phải lời hứa marketing", 320, 686, 640, 16, 10, C.muted, { align: "center" });
  addText(layout, "Layout_PageNumber", String(pageNumber), 1160, 684, 62, 18, 10, C.muted, { bold: true, align: "right" });
  return layout;
}

const layouts = [1, 2, 3, 4, 5].map((n) => createLayout(n, n === 5));

function addSlide(pageNumber, titleText, subtitleText) {
  const slide = deck.slides.add();
  slide.setLayout(layouts[pageNumber - 1]);
  slide.background.fill = C.bg;
  addText(slide, "Slide_Title", titleText, M, 28, CONTENT_W, 42, 32, C.text, { bold: true });
  addText(slide, "Slide_Subtitle", subtitleText, M, 76, CONTENT_W, 28, 17, C.muted);
  return slide;
}

function addRightArrow(surface, name, x, y, w = 22, h = 18, color = C.line) {
  return nameOf(surface.shapes.add({ geometry: "rightArrow", position: { left: x, top: y, width: w, height: h }, fill: color, line: { fill: color, width: 0 } }), name);
}

{
  const s = addSlide(1, "Quy trình hoàn ứng hiện tại: năm điểm chờ thiếu bằng chứng", "Khoảng trống nằm ở ngoại lệ: người duyệt phải tự ghép dữ kiện, quy định và lịch sử.");
  const steps = [
    ["Gửi chứng từ", "Ảnh, số tiền và metadata nằm rời nhau."],
    ["Đọc thủ công", "Tên, ngày, mã và tổng tiền dễ bị bỏ sót."],
    ["Đối chiếu quy định", "FACT và POLICY dễ bị trộn khi giải thích."],
    ["Hỏi cấp trên", "Câu hỏi thiếu dữ kiện để trả lời CÓ / KHÔNG."],
    ["Ghi lịch sử", "Khó nối input, facts, route và người quyết định."],
  ];
  const stepW = 216;
  const gap = 18;
  steps.forEach(([label, pain], i) => {
    const x = M + i * (stepW + gap);
    const accent = i >= 3 ? C.amber : C.teal;
    addShape(s, `S1_Flow_${i + 1}`, x, 150, stepW, 230, C.card, i >= 3 ? C.amber : C.line);
    addIndexIcon(s, `S1_FlowIcon_${i + 1}`, String(i + 1).padStart(2, "0"), x + 16, 170, accent);
    addText(s, `S1_FlowLabel_${i + 1}`, label, x + 16, 220, stepW - 32, 48, 18, C.text, { bold: true, valign: "middle" });
    addLine(s, `S1_FlowRule_${i + 1}`, x + 16, 282, stepW - 32, 0, C.line, 1);
    addText(s, `S1_FlowPain_${i + 1}`, pain, x + 16, 302, stepW - 32, 60, 15, i >= 3 ? C.amber : C.muted, { bold: i >= 3, valign: "middle" });
    if (i < steps.length - 1) addRightArrow(s, `S1_Arrow_${i + 1}`, x + stepW + 1, 254, 16, 22);
  });
  addShape(s, "S1_GapBand", M, 414, CONTENT_W, 112, C.cardAccent, C.amber);
  addText(s, "S1_GapLabel", "KHOẢNG TRỐNG CẦN GIẢI QUYẾT", M + 24, 434, 310, 22, 14, C.amber, { bold: true });
  addText(s, "S1_GapText", "Ngoại lệ chưa được đóng gói thành câu hỏi có đủ dữ kiện, quy định và audit để quản lý quyết định nhanh.", M + 24, 468, CONTENT_W - 48, 38, 20, C.text, { bold: true, valign: "middle" });
  addLine(s, "S1_BridgeRule", M, 560, CONTENT_W, 0, C.line, 1);
  addText(s, "S1_Bridge", "AURA gom hồ sơ, giảm nhập tay, privacy triage và giữ quyền quyết định ngoại lệ cho con người.", M, 586, CONTENT_W, 42, 19, C.green, { bold: true, align: "center", valign: "middle" });
  s.speakerNotes.text = ["MỞ: Quy trình hoàn ứng hiện tại có năm điểm chờ. Vấn đề không chỉ là đọc chữ, mà là người duyệt phải tự ghép dữ kiện, quy định và lịch sử trước khi quyết định.", "Ảnh, số tiền và metadata nằm rời nhau. Việc đọc thủ công dễ bỏ sót trường trọng yếu. Khi đối chiếu, FACT và POLICY dễ bị trộn. Câu hỏi chuyển cấp thường thiếu ngữ cảnh, còn lịch sử khó nối từ đầu vào tới người chịu trách nhiệm.", "Feedback doanh nghiệp ngày 17/10 tập trung đúng vào khoảng trống này: tiếp nhận nhiều hóa đơn, giảm nhập tay, có visibility cho quản lý và xử lý dữ liệu cá nhân an toàn hơn.", "AURA vẫn giữ boundary cốt lõi: AI đọc facts, C# áp policy và con người chịu trách nhiệm ngoại lệ.", "Nguồn: D:/aura/demo_Day/final_feedback_trienkhai_ngay/main_feedback/final feedback need to upgrade.txt; submission/AURA_PROPOSAL_VI.md; BUSINESS_RULES.md; submission/AURA_WORKFLOW_SPEC.md.", "CHUYỂN: Slide tiếp theo cho thấy feedback đã đi vào pipeline mà không tạo một đường xử lý thứ hai."].join("\n\n");
}

{
  const s = addSlide(2, "Pipeline E2E và ranh giới quyết định", "Ba vùng Đầu vào, Xử lý, Đầu ra; hai làn cho Hệ thống và Con người.");
  const labelW = 126;
  const colW = 326;
  const colGap = 14;
  const colX = [M + labelW + 14, M + labelW + 14 + colW + colGap, M + labelW + 14 + (colW + colGap) * 2];
  ["ĐẦU VÀO", "XỬ LÝ", "ĐẦU RA"].forEach((label, i) => addText(s, `S2_Column_${i + 1}`, label, colX[i], 140, colW, 24, 15, C.muted, { bold: true, align: "center" }));
  addShape(s, "S2_SystemLabel", M, 176, labelW, 244, C.cardAccent, C.teal);
  addText(s, "S2_SystemLabelText", "HỆ THỐNG", M + 12, 176, labelW - 24, 244, 16, C.teal, { bold: true, align: "center", valign: "middle" });
  addShape(s, "S2_HumanLabel", M, 438, labelW, 142, C.cardAccent, C.green);
  addText(s, "S2_HumanLabelText", "CON NGƯỜI", M + 12, 438, labelW - 24, 142, 16, C.green, { bold: true, align: "center", valign: "middle" });

  addShape(s, "S2_InputSystem", colX[0], 176, colW, 244, C.card, C.line);
  addPill(s, "S2_PhaseA", "PHA A • GATE 1–3", colX[0] + 18, 194, 150, C.teal);
  addText(s, "S2_PhaseATitle", "Nhận nhiều ảnh và xếp hàng", colX[0] + 18, 238, colW - 36, 28, 19, C.text, { bold: true });
  addText(s, "S2_PhaseADetail", "1–10 JPG/PNG • ≤ 5 MB/ảnh\nMIME + magic bytes\nPrivate storage + SHA-256\nSQL PENDING + AI_QUEUED", colX[0] + 18, 282, colW - 36, 106, 15, C.muted, { valign: "middle" });

  addShape(s, "S2_ProcessSystem", colX[1], 176, colW, 244, C.card, C.line);
  addPill(s, "S2_PhaseB", "PHA B • GATE 4–5", colX[1] + 18, 194, 150, C.teal);
  addText(s, "S2_PhaseBDetail", "HTTP 202 + statusUrl\nWorker lease 600s; reclaim sau restart", colX[1] + 18, 232, colW - 36, 62, 15, C.text, { bold: true, valign: "middle" });
  addLine(s, "S2_ProcessDivider", colX[1] + 18, 306, colW - 36, 0, C.line, 1);
  addPill(s, "S2_PhaseC", "PHA C • GATE 6–7", colX[1] + 18, 322, 150, C.blue);
  addText(s, "S2_PhaseCDetail", "AI permit 1 • data_collection=deny\nSchema → DTO • semantic repair ≤ 1", colX[1] + 18, 360, colW - 36, 50, 14, C.text, { bold: true, valign: "middle" });

  addShape(s, "S2_OutputSystem", colX[2], 176, colW, 244, C.card, C.line);
  addPill(s, "S2_PhaseD", "PHA D • GATE 8", colX[2] + 18, 194, 132, C.amber);
  addText(s, "S2_PhaseDTitle", "PolicyDecisionEngine C#", colX[2] + 18, 238, colW - 36, 30, 18, C.text, { bold: true });
  addText(s, "S2_PhaseDDetail", "Amount provenance + privacy category\nPolicy tất định; confidence không đổi rule", colX[2] + 18, 278, colW - 36, 50, 14, C.muted, { valign: "middle" });
  addShape(s, "S2_AutoBranch", colX[2] + 18, 346, 132, 52, C.cardAccent, C.green);
  addText(s, "S2_AutoBranchText", "AUTO_APPROVE\nGhi lịch sử", colX[2] + 26, 354, 116, 36, 13, C.green, { bold: true, align: "center", valign: "middle" });
  addShape(s, "S2_EscalateBranch", colX[2] + 166, 346, 142, 52, C.cardAccent, C.amber);
  addText(s, "S2_EscalateBranchText", "ESCALATE_*\nFACT • POLICY • PRIVACY", colX[2] + 174, 354, 126, 36, 11, C.amber, { bold: true, align: "center", valign: "middle" });

  addShape(s, "S2_InputHuman", colX[0], 438, colW, 142, C.cardAccent, C.green);
  addText(s, "S2_InputHumanTitle", "Người gửi", colX[0] + 18, 458, colW - 36, 26, 17, C.text, { bold: true });
  addText(s, "S2_InputHumanDetail", "Chọn tối đa 10 ảnh và metadata. Số tiền là tùy chọn.", colX[0] + 18, 500, colW - 36, 48, 14, C.muted, { valign: "middle" });
  addShape(s, "S2_ProcessHuman", colX[1], 438, colW, 142, C.cardAccent, C.green);
  addText(s, "S2_ProcessHumanTitle", "Người vận hành", colX[1] + 18, 458, colW - 36, 26, 17, C.text, { bold: true });
  addText(s, "S2_ProcessHumanDetail", "Thấy từng job đã xếp hàng và tiếp tục thao tác; không chờ request AI.", colX[1] + 18, 495, colW - 36, 58, 14, C.muted, { valign: "middle" });
  addShape(s, "S2_OutputHuman", colX[2], 438, colW, 142, C.cardAccent, C.green);
  addPill(s, "S2_Gate9", "GATE 9", colX[2] + 18, 454, 82, C.green);
  addText(s, "S2_OutputHumanDetail", "Nhân viên chuyển tiếp\nQuản lý trả lời CÓ / KHÔNG\nAudit + CSV tháng", colX[2] + 116, 452, colW - 134, 88, 15, C.text, { bold: true, valign: "middle" });

  addShape(s, "S2_AuthorityBand", M, 606, CONTENT_W, 48, C.cardAccent, C.line);
  addText(s, "S2_AuthorityAI", "AI: FACT", M + 20, 619, 300, 22, 16, C.blue, { bold: true, align: "center" });
  addText(s, "S2_AuthorityPolicy", "C# ENGINE: POLICY", M + 432, 619, 300, 22, 16, C.amber, { bold: true, align: "center" });
  addText(s, "S2_AuthorityHuman", "NHÂN VIÊN / QUẢN LÝ: AUTHORITY", M + 808, 619, 336, 22, 16, C.green, { bold: true, align: "center" });
  s.speakerNotes.text = ["MỞ: Sơ đồ chia rõ ba vùng và hai làn để thấy năm thay đổi sau feedback đã đi vào đúng boundary nào.", "Pha A nhận tối đa mười ảnh nhưng vẫn kiểm từng file, lưu riêng tư, tính SHA-256 rồi ghi một SQL row và AI_QUEUED riêng. UI gọi tuần tự cùng endpoint HTTP 202; mỗi ảnh là một hồ sơ độc lập, chưa có InvoiceBatch hoặc batchId phía server.", "Số tiền là tùy chọn. Nếu để trống, worker chỉ dùng final total do AI trả về sau JSON Schema và semantic validation; nguồn USER_PROVIDED, AI_EXTRACTED hoặc UNAVAILABLE được audit.", "Pha C vẫn giữ AI concurrency bằng một và gọi Qwen3.8 Flash qua OpenRouter. Payload yêu cầu data_collection=deny; strict ZDR chưa bật vì phải xác nhận route live còn provider đủ điều kiện.", "Sau DTO và semantic validation, privacy classifier chỉ giữ category, không lặp giá trị gốc. PolicyDecisionEngine C# mới route AUTO_APPROVE hoặc ESCALATE_FACT, POLICY, PRIVACY, AUTHORITY hay SYSTEM_ERROR.", "Gate con người không đổi: nhân viên chuyển tiếp, quản lý trả lời CÓ hoặc KHÔNG, Undo ghi event mới. Dashboard có thể tải CSV tháng từ SQL; báo cáo không xuất raw AI facts và chống CSV formula injection.", "Nguồn: docs/AFTER_FEEDBACK_IMPLEMENTATION.md; Services/ReceiptProcessingWorker.cs; Services/ReceiptAmountResolver.cs; Services/ReceiptPrivacyClassifier.cs; Services/ManagerOperationsReportExporter.cs; Services/PolicyDecisionEngine.cs.", "CHUYỂN: Những thay đổi này là bằng chứng kỹ thuật; tác động người dùng vẫn phải đo trước và sau."].join("\n\n");
}

{
  const s = addSlide(3, "Đối chiếu trước và sau: phương pháp đã khóa, kết quả còn PENDING", "Không suy tác động từ fixture; chỉ kết luận khi hai phía dùng cùng nhiệm vụ và tiêu chí.");
  const values = [
    ["CHỈ SỐ", "CÁCH ĐO GIỐNG NHAU", "TRƯỚC: THỦ CÔNG", "SAU: AURA", "TRẠNG THÁI"],
    ["ΔT\nThời gian đến kết quả cuối", "Timestamp; lấy median", "Chưa đo", "Chưa đo", "PENDING"],
    ["ΔH\nSố lần chạm", "Đếm thao tác mỗi hồ sơ", "Chưa đo", "Chưa đo", "PENDING"],
    ["E_missed\nBỏ sót ca cần chuyển", "So với đáp án chuẩn", "Chưa đo", "0/10", "Thiếu baseline"],
    ["E_over\nChuyển thừa", "So với đáp án chuẩn", "Chưa đo", "2/10", "Thiếu baseline"],
    ["A_trace\nĐủ dấu vết", "Kiểm tra audit timeline", "Chưa đo", "Có timeline", "Thiếu baseline"],
  ];
  const table = s.tables.add({ rows: values.length, columns: 5, left: M, top: 148, width: CONTENT_W, height: 304, columnWidths: [190, 282, 200, 190, 302], values });
  table.name = "S3_BeforeAfterTable";
  table.borders.assign({ style: "solid", fill: C.line, width: 1 });
  table.cells.block({ row: 0, column: 0, rowCount: values.length, columnCount: 5 }).assign({ fill: C.card, textStyle: { typeface: FONT, fontSize: 13, color: C.text }, margins: { top: 7, right: 10, bottom: 7, left: 10 } });
  table.cells.block({ row: 0, column: 0, rowCount: 1, columnCount: 5 }).assign({ fill: C.cardAccent, textStyle: { typeface: FONT, fontSize: 12, bold: true, color: C.muted } });
  table.cells.block({ row: 1, column: 0, rowCount: 5, columnCount: 1 }).assign({ textStyle: { typeface: FONT, fontSize: 13, bold: true, color: C.text } });
  table.cells.block({ row: 1, column: 2, rowCount: 5, columnCount: 1 }).assign({ textStyle: { typeface: FONT, fontSize: 13, bold: true, color: C.amber } });
  table.cells.block({ row: 1, column: 3, rowCount: 5, columnCount: 1 }).assign({ textStyle: { typeface: FONT, fontSize: 14, bold: true, color: C.green } });
  table.cells.block({ row: 1, column: 4, rowCount: 5, columnCount: 1 }).assign({ textStyle: { typeface: FONT, fontSize: 12, bold: true, color: C.amber } });
  table.rows[0].height = 42;
  for (let r = 1; r < values.length; r++) table.rows[r].height = 52;

  addShape(s, "S3_ProtocolBand", M, 472, CONTENT_W, 104, C.cardAccent, C.line);
  addText(s, "S3_ProtocolLabel", "GIAO THỨC ĐO TÁC ĐỘNG", M + 18, 486, 260, 20, 13, C.green, { bold: true });
  addText(s, "S3_ProtocolLine1", "3 người dùng × cùng 3 nhiệm vụ × cùng bộ hóa đơn; so median trước và sau; giữ cả phản hồi tiêu cực.", M + 18, 516, CONTENT_W - 36, 26, 16, C.text, { bold: true });
  addText(s, "S3_ProtocolLine2", "Ngưỡng đạt / không đạt sẽ được khóa trước khi mở kết quả. Hiện chưa có record người dùng thật.", M + 18, 548, CONTENT_W - 36, 20, 14, C.amber, { bold: true });

  addShape(s, "S3_TechnicalStrip", M, 596, CONTENT_W, 52, C.card, C.line);
  addText(s, "S3_TechnicalLabel", "BẰNG CHỨNG KỸ THUẬT, KHÔNG PHẢI TÁC ĐỘNG", M + 16, 611, 390, 20, 12, C.muted, { bold: true });
  addText(s, "S3_TechnicalValues", "181/181 offline    live baseline 5/5 • 65.004s    re-smoke: PENDING", M + 430, 608, CONTENT_W - 446, 26, 14, C.teal, { bold: true, align: "right" });
  s.speakerNotes.text = ["MỞ: Slide này dùng bảng đối chiếu thật. Mỗi hàng là một chỉ số; hai phía phải cùng cách đo.", "Delta T là thời gian đến kết quả cuối và Delta H là số lần chạm. E_missed và E_over so với cùng đáp án chuẩn. A_trace kiểm tra quyết định có đủ audit timeline hay không.", "Phía AURA đã có 0 trên 10 missed, 2 trên 10 over và audit timeline trên mini-holdout. Phía thủ công chưa đo trên cùng bộ hóa đơn, nên ba hàng này mới có một nửa bằng chứng. Delta T và Delta H chưa có số ở cả hai phía trong user study.", "Giao thức giữ nguyên: ba người dùng làm cùng ba nhiệm vụ trên cùng bộ hóa đơn, so median trước và sau, giữ cả phản hồi tiêu cực, và khóa ngưỡng trước khi mở kết quả.", "Sau feedback, candidate đạt 181 trên 181 test offline. Con số 5 trên 5 và 65.004 giây là baseline live ngày 14/10, không phải phép đo lại candidate ngày 17/10. Publish và live smoke vẫn PENDING.", "Nguồn: docs/AFTER_FEEDBACK_IMPLEMENTATION.md; docs/EVALUATION_EVIDENCE.md; docs/USER_STUDY_PROTOCOL.md; docs/MEASUREMENT_PLAN.md.", "CHUYỂN: Slide tiếp theo phân biệt rõ code đã triển khai, cấu hình cần re-smoke và phần vẫn ở roadmap."].join("\n\n");
}

{
  const s = addSlide(4, "Kiến trúc, ranh giới thực thi và phạm vi evidence", "Mỗi khối ghi rõ phần đã test, quyền của model và phần còn ở roadmap / probe.");
  const leftW = 770;
  const layers = [
    ["GIAO DIỆN", "Lượt 1–10 ảnh • amount tùy chọn • HTTP 202/poll • dashboard + CSV tháng", C.teal],
    ["QUEUE / WORKER", "SQL job riêng • lease 600s • resume pending • AI concurrency 1", C.teal],
    ["AI + VALIDATION", "OpenRouter/Qwen3.8 • data_collection=deny • Schema → DTO\nsemantic repair ≤ 1 • amount provenance • privacy triage sau inference", C.blue],
    ["POLICY + DỮ LIỆU", "PolicyDecisionEngine C# • ESCALATE_PRIVACY • SQL/private storage • audit/Undo", C.amber],
  ];
  layers.forEach(([label, detail, accent], i) => {
    const y = 150 + i * 92;
    addShape(s, `S4_Layer_${i + 1}`, M, y, leftW, 74, C.card, C.line);
    addPill(s, `S4_LiveBadge_${i + 1}`, "ĐÃ TEST", M + 16, y + 10, 110, C.green);
    addText(s, `S4_LayerLabel_${i + 1}`, label, M + 148, y + 12, 210, 24, 15, accent, { bold: true, valign: "middle" });
    addText(s, `S4_LayerText_${i + 1}`, detail, M + 148, y + 35, leftW - 170, 34, i === 2 ? 12 : 14, C.text, { bold: true, valign: "middle" });
    if (i < layers.length - 1) addLine(s, `S4_LayerConnector_${i + 1}`, M + leftW / 2, y + 74, 0, 18, C.line, 1.5);
  });

  const testX = 856;
  addShape(s, "S4_TestBoundary", testX, 150, 366, 350, C.cardAccent, C.amber);
  addText(s, "S4_TestTitle", "CHƯA NẰM TRONG CANDIDATE LIVE", testX + 20, 168, 326, 24, 14, C.amber, { bold: true });
  const simulated = [
    ["Mailbox connector", "Agent gọi tool; connector giữ OAuth + cursor"],
    ["Strict ZDR", "Cần probe route/provider trước khi bật"],
    ["Evidence overlay", "Prototype gắn hash; chưa nối extraction"],
  ];
  simulated.forEach(([label, detail], i) => {
    const y = 210 + i * 88;
    addShape(s, `S4_TestItem_${i + 1}`, testX + 20, y, 326, 70, C.card, C.line);
    addPill(s, `S4_TestBadge_${i + 1}`, "ROADMAP / PROBE", testX + 32, y + 10, 148, C.amber);
    addText(s, `S4_TestLabel_${i + 1}`, label, testX + 194, y + 10, 138, 24, 14, C.text, { bold: true, valign: "middle" });
    addText(s, `S4_TestDetail_${i + 1}`, detail, testX + 32, y + 42, 290, 20, 13, C.muted, { valign: "middle" });
  });

  addShape(s, "S4_DeploymentBand", M, 536, CONTENT_W, 96, C.cardAccent, C.line);
  addText(s, "S4_DeploymentLabel", "MÔ HÌNH HIỆN TẠI VÀ RANH GIỚI BẢO MẬT", M + 20, 550, 420, 20, 13, C.muted, { bold: true });
  addText(s, "S4_DeploymentText", "candidate source • một process • OpenRouter / Alibaba • fallback Ollama tắt", M + 20, 576, CONTENT_W - 40, 20, 15, C.text, { bold: true, valign: "middle" });
  addText(s, "S4_SecurityBoundaryText", "181/181 offline • cần publish + health + synthetic smoke trước khi gọi là live", M + 20, 602, CONTENT_W - 40, 18, 14, C.amber, { bold: true, valign: "middle" });
  s.speakerNotes.text = ["MỞ: Slide này phân định ba trạng thái khác nhau: code đã triển khai và test, candidate live cần re-smoke, và roadmap chưa được ghép vào pipeline.", "Bốn lớp bên trái đã có trong source: giao diện multi-file, dashboard và CSV report; SQL queue/worker; adapter cùng schema/semantic validation; amount resolver, privacy classifier và policy C#.", "Model chỉ nhận ảnh và trả DTO theo contract. Model không có tool, không có credential ghi database và không tự sửa trạng thái. Privacy classifier không cấp quyền; nó chỉ tạo category để policy C# fail-safe sang ESCALATE_PRIVACY. Vì triage chạy sau VLM, demo ưu tiên chứng từ tổng hợp hoặc đã ẩn danh; production cần pre-inference redaction hoặc local VLM cho dữ liệu nhạy cảm.", "OpenRouter payload mặc định gửi data_collection=deny. Strict ZDR chưa bật vì cần kiểm route Alibaba còn provider đủ điều kiện; bật mù có thể làm request không còn nơi xử lý.", "Mailbox connector chưa được triển khai. Workflow mục tiêu không tạo AI pipeline thứ hai: manual upload và mailbox cùng đi qua ingestion boundary hiện tại. Agent chỉ điều phối backend tool; connector riêng giữ OAuth, sync cursor và quarantine.", "Candidate source đạt 181 trên 181 test. Thay đổi report không sửa database model. Tuy nhiên live URL vẫn là baseline trước feedback cho tới khi publish, health và synthetic smoke hoàn tất.", "Nguồn: docs/AFTER_FEEDBACK_IMPLEMENTATION.md; docs/CURRENT_PROJECT_STATUS.md; docs/RECOMMENDED_INGESTION_WORKFLOW.md; Services/ManagerOperationsReportExporter.cs.", "CHUYỂN: Slide cuối nêu lỗi quan sát thật, giới hạn còn lại và gate triển khai chiều nay."].join("\n\n");
}

{
  const s = addSlide(5, "Sau feedback: phần đã làm, giới hạn và gate trước demo", "Cải tiến hôm nay chỉ có giá trị khi không che giấu lỗi cũ và có điều kiện xác nhận rõ.");
  const leftX = M;
  const leftW = 560;
  const rightX = 642;
  const rightW = 580;
  addText(s, "S5_ObservedHeading", "1. LỖI ĐÃ QUAN SÁT", leftX, 138, leftW, 24, 15, C.amber, { bold: true });
  addText(s, "S5_RoadmapHeading", "2. HẠN CHẾ CÒN LẠI                 3. GATE XỬ LÝ TIẾP THEO", rightX, 138, rightW, 24, 14, C.green, { bold: true });

  const observed = [
    ["EVIDENCE", "NGUYÊN NHÂN VÀ PHẢN ỨNG CỦA HỆ THỐNG"],
    ["2/10\nover-escalation", "FAHASA: nhãn ‘Thanh toán’ bị validator từ chối. GS25_2: model bỏ tên cửa hàng gần vùng che. AURA chuyển FACT để người duyệt kiểm tra; 0 missed, 0 lỗi provider."],
    ["2/30\nfield trọng yếu sai", "Phương Nam: vùng che làm mất ngày và model suy diễn nhầm identifier. Quyết định vẫn FACT; evidence công bố rõ confounder, không ghi đè raw."],
    ["10/12\nregression known data", "Vẫn gặp hai failure mode trên. Lượt tuần tự 11/12 cho thấy extraction variance; không tune bằng chính 12 ảnh đã biết."],
  ];
  const observedTable = s.tables.add({ rows: observed.length, columns: 2, left: leftX, top: 170, width: leftW, height: 420, columnWidths: [150, 410], values: observed });
  observedTable.name = "S5_ObservedErrorsTable";
  observedTable.borders.assign({ style: "solid", fill: C.line, width: 1 });
  observedTable.cells.block({ row: 0, column: 0, rowCount: observed.length, columnCount: 2 }).assign({ fill: C.card, textStyle: { typeface: FONT, fontSize: 12, color: C.text }, margins: { top: 7, right: 9, bottom: 7, left: 9 } });
  observedTable.cells.block({ row: 0, column: 0, rowCount: 1, columnCount: 2 }).assign({ fill: C.cardAccent, textStyle: { typeface: FONT, fontSize: 12, bold: true, color: C.muted } });
  observedTable.cells.block({ row: 1, column: 0, rowCount: 3, columnCount: 1 }).assign({ textStyle: { typeface: FONT, fontSize: 14, bold: true, color: C.amber } });
  observedTable.rows[0].height = 42;
  for (let r = 1; r < observed.length; r++) observedTable.rows[r].height = 126;

  const roadmap = [
    ["HẠN CHẾ", "HƯỚNG XỬ LÝ VÀ GATE XÁC NHẬN"],
    ["Candidate chưa publish", "Publish/recycle; health 200; single + multi-file + privacy synthetic smoke. Không dùng baseline cũ để claim UI mới."],
    ["Privacy chỉ là triage", "Demo dùng dữ liệu tổng hợp hoặc đã ẩn danh. Production thêm pre-inference control, OIDC/RBAC, retention và legal review."],
    ["Mailbox chưa nối", "Agent chỉ điều phối; connector giữ OAuth + cursor; cần idempotency, quarantine, backfill. Gate: duplicate và malicious-file tests pass."],
    ["Limiter / lock một process", "Đưa limiter, cooldown, circuit và Verify lock ra storage phân tán. Gate: capacity test multi-instance."],
    ["ZDR / local VLM chưa chốt", "Probe route trước strict ZDR; benchmark local về quality, latency và capacity trước luồng nhạy cảm."],
  ];
  const roadmapTable = s.tables.add({ rows: roadmap.length, columns: 2, left: rightX, top: 170, width: rightW, height: 420, columnWidths: [206, 374], values: roadmap });
  roadmapTable.name = "S5_RoadmapTable";
  roadmapTable.borders.assign({ style: "solid", fill: C.line, width: 1 });
  roadmapTable.cells.block({ row: 0, column: 0, rowCount: roadmap.length, columnCount: 2 }).assign({ fill: C.card, textStyle: { typeface: FONT, fontSize: 12, color: C.text }, margins: { top: 6, right: 8, bottom: 6, left: 8 } });
  roadmapTable.cells.block({ row: 0, column: 0, rowCount: 1, columnCount: 2 }).assign({ fill: C.cardAccent, textStyle: { typeface: FONT, fontSize: 12, bold: true, color: C.muted } });
  roadmapTable.cells.block({ row: 1, column: 0, rowCount: 5, columnCount: 1 }).assign({ textStyle: { typeface: FONT, fontSize: 12, bold: true, color: C.amber } });
  roadmapTable.cells.block({ row: 1, column: 1, rowCount: 5, columnCount: 1 }).assign({ textStyle: { typeface: FONT, fontSize: 12, color: C.text } });
  roadmapTable.rows[0].height = 42;
  for (let r = 1; r < roadmap.length; r++) roadmapTable.rows[r].height = 75;

  addShape(s, "S5_PrincipleBand", M, 610, CONTENT_W, 44, C.cardAccent, C.line);
  addText(s, "S5_PrincipleText", "ĐÃ LÀM: multi-file • AI amount • privacy triage • dashboard + CSV | CHƯA CLAIM LIVE: publish + smoke", M + 16, 622, CONTENT_W - 32, 20, 13, C.text, { bold: true, align: "center" });
  s.speakerNotes.text = ["MỞ: Slide cuối tách ba phần: lỗi đã quan sát, hạn chế còn lại và gate phải vượt trước demo hoặc pilot.", "Lỗi cũ không biến mất chỉ vì có feature mới. Mini-holdout vẫn có hai over-escalation và hai field trọng yếu sai; regression known data vẫn 10 trên 12. AURA fail-safe về người thay vì tự duyệt sai, nhưng chi phí vận hành vẫn phải công bố.", "Sau feedback, source đã có lượt chọn nhiều ảnh, AI-derived amount có provenance, privacy triage, dashboard ngày tháng năm và CSV report. Mỗi ảnh là một hồ sơ độc lập; chưa có InvoiceBatch hoặc batchId phía server. Release build sạch, 181 trên 181 test; report không sửa database model.", "Gate trước demo chiều nay là publish/recycle, health 200, rồi một single upload không nhập amount, một lượt nhiều ảnh nhỏ, một privacy synthetic case và tải CSV tháng. Không rerun holdout cũ để tạo claim accuracy mới.", "Privacy triage không phải compliance và chạy sau inference. Production còn cần pre-inference control hoặc local VLM, identity thật, least privilege, private object storage, retention, monitoring và legal review. Luật 91/2025 và Nghị định 356/2025 là bối cảnh hiện hành, không phải chứng nhận cho sản phẩm.", "Mailbox connector chỉ triển khai khi Agent bị giới hạn ở vai trò điều phối, OAuth nằm trong connector riêng và các gate cursor, idempotency, quarantine, backfill đều đạt. Scale multi-instance cần distributed limiter và lock. Strict ZDR hoặc local VLM phải qua benchmark riêng.", "Nguồn: docs/AFTER_FEEDBACK_IMPLEMENTATION.md; docs/CURRENT_PROJECT_STATUS.md; docs/RECOMMENDED_INGESTION_WORKFLOW.md; docs/EVALUATION_EVIDENCE.md; https://vanban.chinhphu.vn/?docid=214590&pageid=27160; https://vanban.chinhphu.vn/?docid=216387&pageid=27160&typegroupid=4.", "KẾT: AURA đã phản hồi feedback bằng thay đổi có boundary rõ và có test; em không gọi candidate là production-ready hoặc live trước khi các gate còn mở được xác nhận."].join("\n\n");
}

for (let i = 0; i < deck.slides.length; i++) {
  const preview = await deck.export({ slide: deck.slides.getItemAt(i), format: "png", scale: 1 });
  await fs.writeFile(path.join(TMP_DIR, `draft-slide-${i + 1}.png`), new Uint8Array(await preview.arrayBuffer()));
}

const stagingDir = path.join(TMP_DIR, "finalizer");
await fs.mkdir(stagingDir, { recursive: true });
const candidatePath = path.join(stagingDir, "AURA_5_SLIDES_AFTER_FEEDBACK.candidate.pptx");
await (await PresentationFile.exportPptx(deck)).save(candidatePath);
const expectedSlideSizeEmu = "12192000,6858000";
const result = await finalizePresentation({
  explicitTotalSlideCount: 5,
  requiredNativeTableOwnerSlides: [3, 5],
  requiredNativeChartOwnerSlides: [],
  workspaceDir: WORKSPACE_DIR,
  candidatePath,
  finalPath: FINAL_PPTX,
  pythonExecutable: RUNTIME_PYTHON,
  integrityValidatorPath: path.join(SKILL_DIR, "container_tools/inspect_presentation_package_integrity.py"),
  layoutValidatorPath: path.join(SKILL_DIR, "container_tools/inspect_presentation_layout_geometry.py"),
  layoutArgs: ["--expected-slide-size-emu", expectedSlideSizeEmu, "--validate-bullet-geometry", "--validate-heading-fit", "--require-native-table-slide", "3", "--require-native-table-slide", "5"],
  fontPolicy: { basis: "design", families: [FONT] },
  verifyArtifactToolImport: true,
  receiptPath: path.join(stagingDir, "AURA_5_SLIDES_AFTER_FEEDBACK.validation.json"),
});
console.log(JSON.stringify({ final: FINAL_PPTX, font: FONT, result }, null, 2));
