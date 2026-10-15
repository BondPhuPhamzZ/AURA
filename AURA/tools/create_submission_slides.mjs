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
  addText(layout, "Layout_Footer", "AURA • Track VNG • 16/10/2026", M, 686, 540, 16, 10, C.muted);
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
  addText(s, "S1_Bridge", "AURA chuẩn hóa facts, route bằng policy C# và giữ quyền quyết định ngoại lệ cho con người.", M, 586, CONTENT_W, 42, 20, C.green, { bold: true, align: "center", valign: "middle" });
  s.speakerNotes.text = ["MỞ: Quy trình hoàn ứng hiện tại có năm điểm chờ. Vấn đề không chỉ là đọc chữ, mà là người duyệt phải tự ghép dữ kiện, quy định và lịch sử trước khi quyết định.", "Ảnh, claimed amount và metadata nằm rời nhau. Việc đọc thủ công dễ bỏ sót trường trọng yếu. Khi đối chiếu, FACT và POLICY dễ bị trộn. Câu hỏi chuyển cấp thường thiếu ngữ cảnh, còn lịch sử khó nối từ đầu vào tới người chịu trách nhiệm.", "Khoảng trống mà AURA giải quyết là đóng gói ngoại lệ thành một hồ sơ có facts, policy route và audit. Đây là dòng cầu nối sang giải pháp, chưa phải claim tác động.", "Nguồn: submission/AURA_PROPOSAL_VI.md; submission/BUSINESS_RULES.md; submission/AURA_WORKFLOW_SPEC.md.", "CHUYỂN: Slide tiếp theo cho thấy hệ thống xử lý ở đâu và con người quyết định ở đâu."].join("\n\n");
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
  addText(s, "S2_PhaseATitle", "Nhận và xếp hàng", colX[0] + 18, 238, colW - 36, 28, 19, C.text, { bold: true });
  addText(s, "S2_PhaseADetail", "JPG/PNG ≤ 5 MB\nMIME + magic bytes\nPrivate storage + SHA-256\nSQL PENDING + AI_QUEUED", colX[0] + 18, 282, colW - 36, 106, 16, C.muted, { valign: "middle" });

  addShape(s, "S2_ProcessSystem", colX[1], 176, colW, 244, C.card, C.line);
  addPill(s, "S2_PhaseB", "PHA B • GATE 4–5", colX[1] + 18, 194, 150, C.teal);
  addText(s, "S2_PhaseBDetail", "HTTP 202 + statusUrl\nWorker lease 600s; reclaim sau restart", colX[1] + 18, 232, colW - 36, 62, 15, C.text, { bold: true, valign: "middle" });
  addLine(s, "S2_ProcessDivider", colX[1] + 18, 306, colW - 36, 0, C.line, 1);
  addPill(s, "S2_PhaseC", "PHA C • GATE 6–7", colX[1] + 18, 322, 150, C.blue);
  addText(s, "S2_PhaseCDetail", "AI permit 1 • Schema → DTO\nSemantic validation • repair tối đa 1 lần", colX[1] + 18, 360, colW - 36, 50, 15, C.text, { bold: true, valign: "middle" });

  addShape(s, "S2_OutputSystem", colX[2], 176, colW, 244, C.card, C.line);
  addPill(s, "S2_PhaseD", "PHA D • GATE 8", colX[2] + 18, 194, 132, C.amber);
  addText(s, "S2_PhaseDTitle", "PolicyDecisionEngine C#", colX[2] + 18, 238, colW - 36, 30, 18, C.text, { bold: true });
  addText(s, "S2_PhaseDDetail", "Route theo business rules\nKhông dùng confidence để tự đổi policy", colX[2] + 18, 278, colW - 36, 50, 15, C.muted, { valign: "middle" });
  addShape(s, "S2_AutoBranch", colX[2] + 18, 346, 132, 52, C.cardAccent, C.green);
  addText(s, "S2_AutoBranchText", "AUTO_APPROVE\nGhi lịch sử", colX[2] + 26, 354, 116, 36, 13, C.green, { bold: true, align: "center", valign: "middle" });
  addShape(s, "S2_EscalateBranch", colX[2] + 166, 346, 142, 52, C.cardAccent, C.amber);
  addText(s, "S2_EscalateBranchText", "ESCALATE_*\nChờ người xử lý", colX[2] + 174, 354, 126, 36, 13, C.amber, { bold: true, align: "center", valign: "middle" });

  addShape(s, "S2_InputHuman", colX[0], 438, colW, 142, C.cardAccent, C.green);
  addText(s, "S2_InputHumanTitle", "Người gửi", colX[0] + 18, 458, colW - 36, 26, 17, C.text, { bold: true });
  addText(s, "S2_InputHumanDetail", "Chọn ảnh, nhập claimed amount và metadata.", colX[0] + 18, 500, colW - 36, 48, 15, C.muted, { valign: "middle" });
  addShape(s, "S2_ProcessHuman", colX[1], 438, colW, 142, C.cardAccent, C.green);
  addText(s, "S2_ProcessHumanTitle", "Người vận hành", colX[1] + 18, 458, colW - 36, 26, 17, C.text, { bold: true });
  addText(s, "S2_ProcessHumanDetail", "Thấy trạng thái đã xếp hàng và tiếp tục thao tác; không chờ request AI.", colX[1] + 18, 495, colW - 36, 58, 15, C.muted, { valign: "middle" });
  addShape(s, "S2_OutputHuman", colX[2], 438, colW, 142, C.cardAccent, C.green);
  addPill(s, "S2_Gate9", "GATE 9", colX[2] + 18, 454, 82, C.green);
  addText(s, "S2_OutputHumanDetail", "Nhân viên chuyển tiếp\nQuản lý trả lời CÓ / KHÔNG\nUndo ghi thêm event mới", colX[2] + 116, 452, colW - 134, 88, 15, C.text, { bold: true, valign: "middle" });

  addShape(s, "S2_AuthorityBand", M, 606, CONTENT_W, 48, C.cardAccent, C.line);
  addText(s, "S2_AuthorityAI", "AI: FACT", M + 20, 619, 300, 22, 16, C.blue, { bold: true, align: "center" });
  addText(s, "S2_AuthorityPolicy", "C# ENGINE: POLICY", M + 432, 619, 300, 22, 16, C.amber, { bold: true, align: "center" });
  addText(s, "S2_AuthorityHuman", "NHÂN VIÊN / QUẢN LÝ: AUTHORITY", M + 808, 619, 336, 22, 16, C.green, { bold: true, align: "center" });
  s.speakerNotes.text = ["MỞ: Sơ đồ chia rõ ba vùng và hai làn để thấy hệ thống làm gì, con người quyết định ở đâu.", "Pha A kiểm file, lưu riêng tư, tính SHA-256, rồi ghi PENDING cùng AI_QUEUED vào SQL. Người gửi chỉ cung cấp ảnh, claimed amount và metadata.", "Pha B trả HTTP 202 cùng statusUrl. UI poll trạng thái, còn worker dùng lease 600 giây để xử lý bền vững. Pha C lấy một AI permit, gọi OpenRouter tới Qwen3.8 Flash qua Alibaba, parse JSON Schema thành DTO, semantic validate và repair tối đa một lần.", "Pha D dùng PolicyDecisionEngine C# để route. AUTO_APPROVE ghi lịch sử. ESCALATE_* mở Gate 9: nhân viên chuyển tiếp, quản lý trả lời CÓ hoặc KHÔNG, Undo tạo event mới.", "Ranh giới quyền hạn: AI trả FACT; C# áp POLICY; con người giữ AUTHORITY.", "Nguồn: submission/AURA_WORKFLOW_SPEC.md; docs/RUNBOOK.md; ReceiptProcessingWorker.cs; ReceiptProcessingQueue.cs; VisionInvocationCoordinator.cs.", "CHUYỂN: Kiến trúc đã rõ, nhưng tác động chỉ được trình bày khi có đối chứng trước và sau."].join("\n\n");
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
  addText(s, "S3_TechnicalValues", "163/163 offline tests    5/5 Verify live    65.004s", M + 430, 608, CONTENT_W - 446, 26, 16, C.teal, { bold: true, align: "right" });
  s.speakerNotes.text = ["MỞ: Slide này dùng bảng đối chiếu thật. Mỗi hàng là một chỉ số; hai phía phải cùng cách đo.", "ΔT là thời gian đến kết quả cuối và ΔH là số lần chạm. E_missed và E_over so với cùng đáp án chuẩn. A_trace kiểm tra liệu quyết định có đủ audit timeline hay không.", "Phía AURA đã có 0/10 missed, 2/10 over và audit timeline trên mini-holdout. Phía thủ công chưa đo trên cùng bộ hóa đơn, nên ba hàng này mới có một nửa bằng chứng. ΔT và ΔH chưa có số ở cả hai phía trong user study.", "Giao thức: ba người dùng làm cùng ba nhiệm vụ trên cùng bộ hóa đơn; so median trước và sau; giữ phản hồi tiêu cực. Team phải khóa ngưỡng đạt hoặc không đạt trước khi nhận kết quả, không chọn ngưỡng sau khi thấy số.", "163/163, Verify 5/5 và 65.004 giây là bằng chứng kỹ thuật, không phải tác động người dùng hoặc ROI.", "Nguồn: docs/EVALUATION_EVIDENCE.md; docs/USER_STUDY_PROTOCOL.md; docs/MEASUREMENT_PLAN.md.", "CHUYỂN: Tiếp theo là kiến trúc triển khai và ranh giới giữa thành phần chạy thật với dữ liệu mô phỏng."].join("\n\n");
}

{
  const s = addSlide(4, "Kiến trúc triển khai và phạm vi evidence", "Nhãn CHẠY THẬT hoặc MÔ PHỎNG / TEST nằm trực tiếp trên từng khối.");
  const leftW = 770;
  const layers = [
    ["GIAO DIỆN", "Razor MVC • HTTP 202 • polling trạng thái", C.teal],
    ["QUEUE / WORKER", "SQL job • lease 600s • retry / circuit", C.teal],
    ["AI ADAPTER + CONTRACT", "OpenRouter • Qwen3.8 Flash • Schema → DTO → semantic → repair ≤ 1", C.blue],
    ["POLICY + DỮ LIỆU", "PolicyDecisionEngine C# • EF Core • SQL Server • private storage • Undo event", C.amber],
  ];
  layers.forEach(([label, detail, accent], i) => {
    const y = 150 + i * 92;
    addShape(s, `S4_Layer_${i + 1}`, M, y, leftW, 74, C.card, C.line);
    addPill(s, `S4_LiveBadge_${i + 1}`, "CHẠY THẬT", M + 16, y + 10, 110, C.green);
    addText(s, `S4_LayerLabel_${i + 1}`, label, M + 148, y + 12, 210, 24, 15, accent, { bold: true, valign: "middle" });
    addText(s, `S4_LayerText_${i + 1}`, detail, M + 148, y + 40, leftW - 170, 24, 14, C.text, { bold: true, valign: "middle" });
    if (i < layers.length - 1) addLine(s, `S4_LayerConnector_${i + 1}`, M + leftW / 2, y + 74, 0, 18, C.line, 1.5);
  });

  const testX = 856;
  addShape(s, "S4_TestBoundary", testX, 150, 366, 350, C.cardAccent, C.amber);
  addText(s, "S4_TestTitle", "DỮ LIỆU VÀ PROTOTYPE", testX + 20, 168, 326, 24, 15, C.amber, { bold: true });
  const simulated = [
    ["30 ảnh tổng hợp", "Dữ liệu synthetic dùng cho test"],
    ["5 fixture Verify", "Bộ minh họa chạy trên UI"],
    ["Evidence overlay", "Prototype gắn hash; chưa nối extraction"],
  ];
  simulated.forEach(([label, detail], i) => {
    const y = 210 + i * 88;
    addShape(s, `S4_TestItem_${i + 1}`, testX + 20, y, 326, 70, C.card, C.line);
    addPill(s, `S4_TestBadge_${i + 1}`, "MÔ PHỎNG / TEST", testX + 32, y + 10, 148, C.amber);
    addText(s, `S4_TestLabel_${i + 1}`, label, testX + 194, y + 10, 138, 24, 14, C.text, { bold: true, valign: "middle" });
    addText(s, `S4_TestDetail_${i + 1}`, detail, testX + 32, y + 42, 290, 20, 13, C.muted, { valign: "middle" });
  });

  addShape(s, "S4_DeploymentBand", M, 544, CONTENT_W, 82, C.cardAccent, C.line);
  addText(s, "S4_DeploymentLabel", "MÔ HÌNH TRIỂN KHAI HIỆN TẠI", M + 20, 560, 310, 20, 13, C.muted, { bold: true });
  addText(s, "S4_DeploymentText", "localhost • một process • AI qua OpenRouter / Alibaba • fallback Ollama mặc định tắt", M + 20, 590, CONTENT_W - 40, 24, 17, C.text, { bold: true });
  s.speakerNotes.text = ["MỞ: Slide này phân định trực tiếp thành phần chạy thật với dữ liệu và prototype mô phỏng.", "Bốn lớp chạy thật gồm giao diện Razor MVC, queue/worker trong SQL, AI adapter cùng evidence contract, và PolicyDecisionEngine C# với dữ liệu bền vững. Model có thể thay, nhưng mọi facts vẫn phải qua contract, semantic gate và cùng policy engine.", "Ba khối mô phỏng hoặc test là 30 ảnh tổng hợp, 5 fixture Verify và evidence overlay prototype. Overlay hiện chỉ gắn với hash, chưa nối vào extraction hoặc policy và chưa có benchmark localization trên ảnh unseen.", "Mô hình triển khai hiện tại là localhost, một process; AI gọi qua OpenRouter tới route Alibaba; fallback Ollama mặc định tắt.", "Nguồn: submission/AURA_WORKFLOW_SPEC.md; docs/EVALUATION_EVIDENCE.md; docs/RUNBOOK.md; docs/CURRENT_PROJECT_STATUS.md.", "CHUYỂN: Kiến trúc đã xác định rõ phạm vi; slide cuối nêu lỗi đã quan sát và điều kiện để mở rộng an toàn."].join("\n\n");
}

{
  const s = addSlide(5, "Lỗi đã quan sát, hạn chế còn lại và hướng xử lý", "Mỗi vấn đề có nguyên nhân, phản ứng an toàn và điều kiện xác nhận trước khi mở rộng.");
  const leftX = M;
  const leftW = 560;
  const rightX = 642;
  const rightW = 580;
  addText(s, "S5_ObservedHeading", "1. LỖI ĐÃ QUAN SÁT", leftX, 138, leftW, 24, 15, C.amber, { bold: true });
  addText(s, "S5_RoadmapHeading", "2. HẠN CHẾ CÒN LẠI                         3. HƯỚNG XỬ LÝ TIẾP THEO", rightX, 138, rightW, 24, 14, C.green, { bold: true });

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
    ["Feedback 3 user còn PENDING", "Chạy paired protocol; khóa ngưỡng trước kết quả. Gate: 3 record có consent và giữ feedback tiêu cực."],
    ["Limiter / lock theo process", "Đưa limiter, cooldown, circuit và Verify lock ra storage phân tán. Gate: test capacity multi-instance."],
    ["PDF + object storage", "Thêm PDF ingestion và private object store. Gate: MIME, AV và retrieval regression đều pass."],
    ["RBAC", "Lập role matrix và authorization tests. Gate: thao tác sai quyền bị chặn và có audit."],
    ["Overlay mới là prototype", "Chuẩn hóa bounding box + fallback low-confidence. Gate: localization QA trên receipt unseen."],
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
  addText(s, "S5_PrincipleText", "Giữ fail-safe, không tune bằng holdout; mọi cải tiến phải có fixture độc lập và gate xác nhận.", M + 16, 622, CONTENT_W - 32, 20, 15, C.text, { bold: true, align: "center" });
  s.speakerNotes.text = ["MỞ: Slide cuối tách ba phần: lỗi đã quan sát, hạn chế còn lại và điều kiện xác nhận cho bước tiếp theo.", "Trong mini-holdout, hai over-escalation là FAHASA và GS25_2. FAHASA bị validator từ chối nhãn ‘Thanh toán 91.500 đ’; GS25_2 bị thiếu merchantName gần vùng che. Hệ thống dừng ở FACT để người duyệt kiểm tra, nên không có missed escalation hay provider error.", "Hai trên ba mươi field trọng yếu sai là hai field của Phương Nam: ngày bị vùng che cắt và identifier bị suy diễn quá mức. Quyết định vẫn đúng FACT; đây là redaction confounder phải công bố riêng, không đồng nhất với hai ca over-escalation.", "Regression trên 12 ảnh đã biết đạt 10/12 với cùng hai failure mode. Lượt tuần tự 11/12 cho thấy variance, nên không dùng chính các ảnh này để nới guard.", "Lộ trình có gate cụ thể: user study có consent và feedback tiêu cực; distributed coordination trước multi-instance; PDF/object storage qua kiểm MIME, AV và retrieval; RBAC qua authorization test; overlay qua localization QA trên receipt unseen.", "Nguồn: docs/EVALUATION_EVIDENCE.md; D:/aura/demo_evidence/22_NewRealReceipt_ForHoldout/holdout10_evidence_v3_2026-10-13/09_documentation_update/mismatch-register.corrected-utf8.json; D:/aura/demo_evidence/22_NewRealReceipt_ForHoldout/regression12_original_bytes_v1_2026-10-13/07_analysis/REGRESSION12_ORIGINAL_BYTES_RESULT_2026-10-13.md.", "KẾT: AURA hiện phù hợp cho demo có giám sát. Bước tiếp theo chỉ được coi là đạt khi qua gate đã định trước."].join("\n\n");
}

for (let i = 0; i < deck.slides.length; i++) {
  const preview = await deck.export({ slide: deck.slides.getItemAt(i), format: "png", scale: 1 });
  await fs.writeFile(path.join(TMP_DIR, `draft-slide-${i + 1}.png`), new Uint8Array(await preview.arrayBuffer()));
}

const stagingDir = path.join(TMP_DIR, "finalizer");
await fs.mkdir(stagingDir, { recursive: true });
const candidatePath = path.join(stagingDir, "AURA_5_SLIDES_REDESIGN.candidate.pptx");
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
  fontPolicy: { basis: "user_request", families: [FONT] },
  verifyArtifactToolImport: true,
  receiptPath: path.join(stagingDir, "AURA_5_SLIDES_REDESIGN.validation.json"),
});
console.log(JSON.stringify({ final: FINAL_PPTX, font: FONT, result }, null, 2));
