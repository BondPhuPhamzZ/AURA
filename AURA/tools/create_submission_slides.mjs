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
  addText(layout, "Layout_Footer", "AURA • Track VNG • 15/10/2026", M, 686, 540, 16, 10, C.muted);
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
  const s = addSlide(1, "Hoàn ứng hôm nay: nhiều điểm chờ, ít bằng chứng", "AURA biến ngoại lệ thành câu hỏi có thể quyết định — không giao quyền duyệt cho AI.");
  const steps = [["Gửi hóa đơn", "Ảnh + số tiền"], ["Đọc thủ công", "Dễ sai định dạng"], ["Đối chiếu policy", "Phụ thuộc người xử lý"], ["Hỏi quản lý", "Thiếu câu hỏi cụ thể"], ["Lưu lịch sử", "Khó truy vết"]];
  const stepW = 216;
  const gap = 18;
  steps.forEach(([label, pain], i) => {
    const x = M + i * (stepW + gap);
    const painful = i >= 3;
    addShape(s, `S1_Flow_${i + 1}`, x, 145, stepW, 112, painful ? C.cardAccent : C.card, painful ? C.amber : C.line);
    addIndexIcon(s, `S1_FlowIcon_${i + 1}`, String(i + 1).padStart(2, "0"), x + 14, 160, painful ? C.amber : C.teal);
    addText(s, `S1_FlowLabel_${i + 1}`, label, x + 58, 160, stepW - 72, 28, 17, C.text, { bold: true, valign: "middle" });
    addText(s, `S1_FlowPain_${i + 1}`, pain, x + 14, 210, stepW - 28, 22, 13, painful ? C.amber : C.muted, { bold: painful });
    if (i < steps.length - 1) addRightArrow(s, `S1_Arrow_${i + 1}`, x + stepW + 1, 191, 16, 18);
  });
  addText(s, "S1_PillarsLabel", "BA TRỤ CỘT", M, 298, 180, 22, 13, C.muted, { bold: true });
  const pillars = [["AI đọc", "Trích facts có cấu trúc; không suy đoán.", C.blue, "AI"], ["Policy quyết định", "C# tất định; đầu vào nghi vấn không auto.", C.amber, "C#"], ["Con người chịu trách nhiệm", "Xử lý ngoại lệ; có thể hoàn tác.", C.teal, "H"]];
  const pillarW = 368;
  pillars.forEach(([label, detail, accent, icon], i) => {
    const x = M + i * (pillarW + 30);
    addShape(s, `S1_Pillar_${i + 1}`, x, 330, pillarW, 104, C.card, C.line);
    addIndexIcon(s, `S1_PillarIcon_${i + 1}`, icon, x + 18, 350, accent);
    addText(s, `S1_PillarLabel_${i + 1}`, label, x + 68, 345, pillarW - 88, 26, 18, C.text, { bold: true });
    addText(s, `S1_PillarDetail_${i + 1}`, detail, x + 68, 378, pillarW - 88, 36, 15, C.muted);
  });
  addShape(s, "S1_QuotePanel", M, 474, CONTENT_W, 154, C.cardAccent, C.line);
  addLine(s, "S1_QuoteAccent", M + 28, 500, 0, 94, C.amber, 4);
  addText(s, "S1_Quote", "“Không phải OCR — mà là biết khi nào phải dừng và hỏi người.”", M + 62, 500, 1060, 78, 28, C.text, { bold: true, valign: "middle" });
  addText(s, "S1_QuoteTag", "AURA = facts có cấu trúc + policy tất định + human accountability", M + 62, 580, 980, 24, 14, C.muted);
  s.speakerNotes.text = ["MỞ: Hoàn ứng không chỉ là bài toán đọc chữ; điểm đau thật nằm ở chỗ ngoại lệ chờ người xử lý và thiếu bằng chứng truy vết.", "AURA nhận ảnh hóa đơn và claimed amount, nhưng không giao quyền duyệt cho model. AI chỉ trích facts; PolicyDecisionEngine C# route; nhân viên và quản lý chịu trách nhiệm ở nhánh ngoại lệ.", "Thông điệp cần giữ: không phải OCR — mà là biết khi nào phải dừng và hỏi người.", "Nguồn: submission/AURA_PROPOSAL_VI.md; submission/BUSINESS_RULES.md; submission/AURA_WORKFLOW_SPEC.md.", "CHUYỂN: Tiếp theo là cách chín gate biến nguyên tắc này thành pipeline chạy thật."].join("\n\n");
}

{
  const s = addSlide(2, "Pipeline E2E: async trước, AI đọc, policy quyết định", "Chín gate chạy thật; con người chỉ quyết định ở nhánh ngoại lệ.");
  const phases = [["A", "NHẬN & XẾP HÀNG", "1–3", "JPG/PNG ≤ 5 MB\nMIME/magic • private + SHA\nSQL PENDING + audit", C.teal], ["B", "CHẠY NỀN", "4–5", "HTTP 202 + statusUrl\nWorker lease 600s\nReclaim sau restart", C.teal], ["C", "AI ĐỌC", "6–7", "1 AI call đồng thời\nSchema → DTO → semantic\nRepair ≤ 1", C.blue], ["D", "POLICY C#", "8", "PolicyDecisionEngine\nroute theo rules\nkhông dựa confidence", C.amber]];
  const phaseW = 220;
  const phaseGap = 18;
  phases.forEach(([letter, label, gates, detail, accent], i) => {
    const x = M + i * (phaseW + phaseGap);
    addShape(s, `S2_Phase_${letter}`, x, 158, phaseW, 224, C.card, C.line);
    addIndexIcon(s, `S2_PhaseIcon_${letter}`, letter, x + 16, 176, accent);
    addText(s, `S2_PhaseLabel_${letter}`, label, x + 62, 176, 136, 24, 14, C.text, { bold: true, valign: "middle" });
    addPill(s, `S2_Gates_${letter}`, `GATE ${gates}`, x + 16, 222, 92, accent);
    addText(s, `S2_PhaseDetail_${letter}`, detail, x + 16, 270, phaseW - 32, 88, 15, C.muted, { valign: "middle" });
    if (i < phases.length - 1) addRightArrow(s, `S2_PhaseArrow_${i + 1}`, x + phaseW + 1, 255, 16, 24);
  });
  const outcomeX = 1010;
  addRightArrow(s, "S2_PolicyToOutcome", 990, 257, 18, 24, C.amber);
  addShape(s, "S2_Auto", outcomeX, 158, 212, 96, C.cardAccent, C.green);
  addPill(s, "S2_AutoPill", "AUTO_APPROVE", outcomeX + 16, 172, 132, C.green);
  addText(s, "S2_AutoText", "Ca rõ ràng\nGhi lịch sử; không chờ quản lý", outcomeX + 16, 210, 180, 38, 14, C.text, { bold: true });
  addShape(s, "S2_Escalate", outcomeX, 278, 212, 144, C.cardAccent, C.amber);
  addPill(s, "S2_EscalatePill", "GATE 9 • ESCALATE_*", outcomeX + 16, 292, 170, C.amber);
  addPersonIcon(s, "S2_EmployeeIcon", outcomeX + 16, 334, C.amber);
  addText(s, "S2_Employee", "Nhân viên chuyển tiếp", outcomeX + 54, 336, 145, 20, 13, C.text, { bold: true });
  addPersonIcon(s, "S2_ManagerIcon", outcomeX + 16, 369, C.amber);
  addText(s, "S2_Manager", "Quản lý YES / NO", outcomeX + 54, 371, 145, 20, 13, C.text, { bold: true });
  addLine(s, "S2_AutoDown", 1116, 255, 0, 252, C.green, 1.5);
  addLine(s, "S2_EscalateDown", 1116, 423, 0, 84, C.amber, 1.5);
  addShape(s, "S2_AuditBand", 772, 500, 450, 112, C.cardAccent, C.line);
  addPill(s, "S2_AuditPill", "AUDIT / UNDO", 792, 518, 124, C.teal);
  addText(s, "S2_AuditText", "Append-only timeline  •  Undo = event mới", 938, 519, 256, 26, 15, C.text, { bold: true, valign: "middle" });
  addText(s, "S2_AuditWhy", "Hai nhánh cùng để lại bằng chứng; UI luôn đọc trạng thái từ hồ sơ bền vững.", 792, 562, 402, 28, 14, C.muted);
  addShape(s, "S2_PrincipleBand", M, 426, 680, 186, C.card, C.line);
  addText(s, "S2_PrincipleKicker", "NGUYÊN TẮC", M + 20, 446, 180, 20, 13, C.muted, { bold: true });
  addText(s, "S2_Principle", "Request nhận nhanh bằng HTTP 202.\nWorker xử lý nền.\nAI trả facts; policy mới trả route.", M + 20, 481, 620, 90, 22, C.text, { bold: true });
  s.speakerNotes.text = ["MỞ: Pipeline này ưu tiên phản hồi nhanh cho người dùng nhưng giữ xử lý AI ở nền và ghi trạng thái vào SQL.", "Gate 1–3 kiểm extension, MIME, magic bytes, giới hạn 5 MB; lưu private, tính SHA-256, ghi PENDING và AI_QUEUED. Gate 4 trả HTTP 202 + statusUrl để UI poll. Gate 5 worker claim bằng lease 600 giây và có thể reclaim sau restart.", "Gate 6–7 dùng VisionInvocationCoordinator để lấy một AI permit; OpenRouter gọi qwen/qwen3.8-flash qua route Alibaba với reasoning none. Adapter parse JSON Schema thành ReceiptExtractionDto; semantic validator kiểm mâu thuẫn; repair tối đa một lần.", "Gate 8 là PolicyDecisionEngine C#. Gate 9 chỉ xuất hiện ở ESCALATE_*: nhân viên chuyển tiếp rồi quản lý YES/NO. AUTO_APPROVE và ESCALATE đều về audit timeline; Undo tạo event mới.", "Nguồn: submission/AURA_WORKFLOW_SPEC.md; docs/RUNBOOK.md; ReceiptProcessingWorker.cs; ReceiptProcessingQueue.cs; VisionInvocationCoordinator.cs.", "CHUYỂN: Pipeline có vẻ hợp lý, nhưng slide tiếp theo chỉ giữ lại những gì đã đo được và tách phần còn PENDING."].join("\n\n");
}

{
  const s = addSlide(3, "Đã đo được gì — và chưa dám khẳng định gì", "Không suy ROI từ fixture; dùng cùng nhiệm vụ, cùng người, cùng tiêu chí.");
  const metrics = [["163/163", "OFFLINE TESTS", "Release build sạch", C.blue], ["8/10", "10 HÓA ĐƠN MỚI", "0 missed • 2 over fail-safe", C.green], ["5/5", "VERIFY LIVE", "65.004s", C.teal]];
  const cardW = 246;
  metrics.forEach(([value, label, detail, accent], i) => {
    const x = M + i * (cardW + 22);
    addShape(s, `S3_Metric_${i + 1}`, x, 162, cardW, 258, C.card, C.line);
    addText(s, `S3_MetricLabel_${i + 1}`, label, x + 18, 184, cardW - 36, 22, 13, C.muted, { bold: true, align: "center" });
    addText(s, `S3_MetricValue_${i + 1}`, value, x + 10, 222, cardW - 20, 76, value.length > 4 ? 54 : 60, accent, { bold: true, align: "center", valign: "middle" });
    addLine(s, `S3_MetricRule_${i + 1}`, x + 30, 316, cardW - 60, 0, accent, 2);
    addText(s, `S3_MetricDetail_${i + 1}`, detail, x + 18, 342, cardW - 36, 42, 15, C.text, { bold: true, align: "center", valign: "middle" });
  });
  const pendingX = 880;
  addShape(s, "S3_Pending", pendingX, 162, 342, 364, C.cardAccent, C.amber, 6);
  addPill(s, "S3_PendingPill", "ĐANG CHỜ ĐO • PENDING", pendingX + 22, 182, 194, C.amber);
  addText(s, "S3_PendingMetrics", "ΔT   +   ΔH", pendingX + 22, 230, 298, 48, 30, C.text, { bold: true, align: "center" });
  addText(s, "S3_PendingUsers", "3 người dùng • phản hồi dự kiến 16/10", pendingX + 22, 286, 298, 24, 14, C.amber, { bold: true, align: "center" });
  addText(s, "S3_MethodLabel", "PAIRED METHOD", pendingX + 22, 334, 160, 20, 12, C.muted, { bold: true });
  const method = ["Cùng người / cùng task", "Timestamp + số lần chạm", "So median trước / sau", "Giữ feedback tiêu cực"];
  method.forEach((line, i) => {
    addIndexIcon(s, `S3_MethodIcon_${i + 1}`, String(i + 1), pendingX + 22, 366 + i * 35, C.amber);
    addText(s, `S3_MethodText_${i + 1}`, line, pendingX + 68, 372 + i * 35, 248, 20, 13, C.text, { bold: true });
  });
  addShape(s, "S3_HonestyBand", M, 464, 790, 112, C.cardAccent, C.line);
  addText(s, "S3_HonestyKicker", "CÁCH ĐỌC ĐÚNG", M + 22, 484, 170, 20, 13, C.green, { bold: true });
  addText(s, "S3_HonestyText", "8/10 là mini-holdout mẫu nhỏ. Verify là fixture kỹ thuật.\nKhông số nào ở đây là production accuracy hay ROI.", M + 22, 518, 742, 44, 18, C.text, { bold: true });
  addText(s, "S3_Caption", "Mẫu nhỏ — không phải production accuracy.", M, 612, CONTENT_W, 24, 15, C.amber, { bold: true, align: "center" });
  s.speakerNotes.text = ["MỞ: Tôi tách rõ ba mốc đã đo và một phần chưa đo để tránh biến fixture thành câu chuyện ROI.", "Đã đo: 163/163 test offline, Release build sạch. Prospective mini-holdout 10 hóa đơn mới đạt 8/10 quyết định đúng, 0 missed escalation, 2 over-escalation fail-safe; mẫu nhỏ, không phải production accuracy. Verify live đạt 5/5 trong 65.004 giây.", "Công thức phương pháp nằm ở ghi chú, không phải một điểm số: U_AURA = <ΔT, ΔH, E_missed, E_over, A_trace>. ΔT là thời gian route; ΔH là số lần chạm; E_missed/E_over là hai loại lỗi quyết định; A_trace là mức đủ dấu vết.", "Regression 12 ảnh byte gốc đạt 10/12 và lượt tuần tự 11/12 chỉ là stability evidence trên dữ liệu đã biết, không phải holdout mới và không dùng làm headline accuracy.", "ΔT và ΔH vẫn PENDING, chờ paired feedback từ 3 người dùng dự kiến 16/10. Không suy ROI trước dữ liệu.", "Nguồn: docs/EVALUATION_EVIDENCE.md; docs/CURRENT_PROJECT_STATUS.md; docs/MEASUREMENT_PLAN.md.", "CHUYỂN: Để hiểu vì sao model có thể thay mà policy không đổi, ta nhìn kiến trúc theo bốn lớp."].join("\n\n");
}

{
  const s = addSlide(4, "Kiến trúc triển khai: model thay được, policy đứng yên", "ASP.NET Core + SQL chạy thật; fixture/runner được phân định rõ là mô phỏng.");
  addPill(s, "S4_LiveBadge", "CHẠY THẬT", M, 142, 116, C.green);
  const layers = [["GIAO DIỆN", "Razor MVC • HTTP 202 • polling", C.teal], ["QUEUE / WORKER", "SQL job • lease 600s • retry / circuit", C.teal], ["AI ADAPTER + CONTRACT", "OpenRouter → Qwen3.8 Flash • Schema → DTO → semantic → repair ≤ 1", C.blue], ["POLICY + DỮ LIỆU", "PolicyDecisionEngine C# • EF Core • SQL Server • private storage • Undo event", C.amber]];
  layers.forEach(([label, detail, accent], i) => {
    const y = 186 + i * 80;
    addShape(s, `S4_Layer_${i + 1}`, M, y, 754, 62, C.card, C.line);
    addPill(s, `S4_LayerPill_${i + 1}`, label, M + 16, y + 17, i === 2 ? 202 : 166, accent);
    addText(s, `S4_LayerText_${i + 1}`, detail, M + (i === 2 ? 238 : 202), y + 18, i === 2 ? 492 : 526, 28, 14, C.text, { bold: true, valign: "middle" });
    if (i < layers.length - 1) addLine(s, `S4_LayerConnector_${i + 1}`, M + 377, y + 62, 0, 18, C.line, 1.5);
  });
  addShape(s, "S4_TestCard", 844, 142, 378, 364, C.cardAccent, C.amber);
  addPill(s, "S4_TestBadge", "MÔ PHỎNG / TEST", 866, 162, 166, C.amber);
  const simulated = ["30 ảnh tổng hợp", "5 fixture Verify trên UI", "Runner ngoài dashboard"];
  simulated.forEach((item, i) => {
    addIndexIcon(s, `S4_TestIcon_${i + 1}`, String(i + 1), 866, 222 + i * 52, C.amber);
    addText(s, `S4_TestText_${i + 1}`, item, 912, 228 + i * 52, 278, 22, 16, C.text, { bold: true });
  });
  addLine(s, "S4_TestDivider", 866, 390, 332, 0, C.line, 1);
  addText(s, "S4_TestCaveat", "Không phải receipt thật / user evidence.\nKhông claim production SLA.", 866, 414, 332, 58, 16, C.muted, { bold: true });
  addShape(s, "S4_AuthorityBand", M, 532, CONTENT_W, 108, C.cardAccent, C.line);
  addText(s, "S4_AuthorityLabel", "RANH GIỚI QUYỀN HẠN", M + 20, 548, 222, 20, 13, C.muted, { bold: true });
  const nodes = [["AI trả facts", C.blue], ["Policy C# route", C.amber], ["Người quyết định ngoại lệ", C.teal]];
  nodes.forEach(([label, accent], i) => {
    const x = M + 260 + i * 280;
    addShape(s, `S4_AuthorityNode_${i + 1}`, x, 562, 220, 52, C.card, accent);
    addText(s, `S4_AuthorityNodeText_${i + 1}`, label, x + 12, 577, 196, 22, 16, C.text, { bold: true, align: "center" });
    if (i < nodes.length - 1) addRightArrow(s, `S4_AuthorityArrow_${i + 1}`, x + 230, 580, 30, 18);
  });
  s.speakerNotes.text = ["MỞ: Kiến trúc tách extraction khỏi decision để đổi model không tự thay đổi business policy.", "Lớp giao diện dùng Razor MVC, HTTP 202 và polling. Queue/Worker lưu job trong SQL, dùng lease 600 giây, retry và circuit state. AI Adapter gọi OpenRouter → qwen/qwen3.8-flash/Alibaba; contract parse JSON Schema thành ReceiptExtractionDto, semantic validate và repair tối đa một lần. PolicyDecisionEngine C# route; EF Core/SQL Server/private storage lưu hồ sơ và event Undo.", "Baseline live: fallback Ollama mặc định tắt. Model có thể thay và làm facts thay đổi, nhưng không tự đổi policy vì mọi provider phải qua cùng contract, semantic gate và PolicyDecisionEngine.", "Phần mô phỏng/test được tách rõ: 30 ảnh tổng hợp, 5 fixture Verify trên UI, runner ngoài dashboard. Đây không phải receipt thật, user evidence hay SLA.", "Repository và localhost runbook: https://github.com/BondPhuPhamzZ/AURA ; docs/RUNBOOK.md ; docs/JUDGE_LOCAL_SETUP.md.", "CHUYỂN: Cuối cùng, tôi trình bày các failure mode hiện tại và cách baseline dừng an toàn."].join("\n\n");
}

{
  const s = addSlide(5, "Giới hạn, tình huống lỗi và bước tiếp theo", "Baseline có người giám sát: lỗi phải dừng an toàn và để lại bằng chứng.");
  const values = [["TÌNH HUỐNG", "AURA PHẢN ỨNG"], ["JSON / semantic sai", "Repair đúng 1 lần → FACT fail-safe"], ["429 / timeout", "Fallback chỉ khi bật → audit provider"], ["App restart", "DB job + lease → worker reclaim"], ["5 upload đồng thời", "5/5 hoàn tất • P95 93.684s • chưa là SLA"], ["Ảnh mơ hồ / giả", "FACT ưu tiên • không auto-approve"]];
  const table = s.tables.add({ rows: values.length, columns: 2, left: M, top: 152, width: 710, height: 418, columnWidths: [232, 478], values });
  table.name = "S5_LimitsTable";
  table.borders.assign({ style: "solid", fill: C.line, width: 1 });
  table.cells.block({ row: 0, column: 0, rowCount: values.length, columnCount: 2 }).assign({ fill: C.card, textStyle: { typeface: FONT, fontSize: 14, color: C.text }, margins: { top: 8, right: 12, bottom: 8, left: 12 } });
  table.cells.block({ row: 0, column: 0, rowCount: 1, columnCount: 2 }).assign({ fill: C.cardAccent, textStyle: { typeface: FONT, fontSize: 13, bold: true, color: C.muted } });
  table.cells.block({ row: 1, column: 0, rowCount: 5, columnCount: 1 }).assign({ textStyle: { typeface: FONT, fontSize: 14, bold: true, color: C.text } });
  table.getCell(4, 1).text.style = { typeface: FONT, fontSize: 14, bold: true, color: C.amber };
  table.rows[0].height = 48;
  for (let r = 1; r < values.length; r++) table.rows[r].height = 74;
  addShape(s, "S5_OpenCard", 806, 152, 416, 246, C.cardAccent, C.amber);
  addPill(s, "S5_OpenPill", "CÒN MỞ", 828, 172, 98, C.amber);
  addText(s, "S5_OpenText", "• Phản hồi 3 người dùng: chờ 16/10\n• Limiter / lock mới ở một process\n• PDF • RBAC • object storage\n• Evidence overlay mới là prototype", 828, 222, 370, 144, 16, C.text, { bold: true });
  addShape(s, "S5_NextCard", 806, 426, 416, 144, C.cardAccent, C.green);
  addPill(s, "S5_NextPill", "BƯỚC TIẾP THEO", 828, 446, 154, C.green);
  addText(s, "S5_NextText", "Sau feedback: chọn 1 thay đổi truy vết → chạy lại build / test / Verify.\nKhông tune bằng holdout.", 828, 490, 370, 64, 16, C.text, { bold: true });
  addText(s, "S5_SafetyLine", "Fail-safe trước • audit đầy đủ • claim đúng phạm vi", M, 610, CONTENT_W, 24, 18, C.amber, { bold: true, align: "center" });
  s.speakerNotes.text = ["MỞ: Baseline hiện tại không che giấu failure mode; mỗi lỗi phải dừng an toàn và để lại bằng chứng.", "JSON hoặc semantic invalid được repair đúng một lần, sau đó FACT fail-safe. 429/timeout chỉ dùng fallback khi cấu hình bật và luôn audit provider. App restart không mất row SQL; worker mới reclaim khi lease hết hạn.", "Burst 5 upload khác nhau hoàn tất 5/5, E2E P95 93.684 giây. Đây là queue integrity trên một process, không phải production SLA. Limiter, cooldown, circuit state và Verify lock hiện vẫn theo process; multi-instance cần distributed coordination.", "Ảnh mơ hồ/giả ưu tiên ESCALATE_FACT, không auto-approve. Evidence overlay mới là prototype cô lập, chưa nằm trong production pipeline.", "Sau feedback 3 người dùng: chọn đúng một thay đổi có traceability, rồi chạy lại Release build, 163 test và Verify. Không tune bằng holdout.", "Nguồn: docs/CURRENT_PROJECT_STATUS.md; docs/EVALUATION_EVIDENCE.md; docs/RUNBOOK.md; submission/AURA_WORKFLOW_SPEC.md.", "CHUYỂN/KẾT: AURA không hứa thay người duyệt; AURA làm ngoại lệ rõ hơn, dừng đúng lúc và để lại audit trail cho quyết định có trách nhiệm."].join("\n\n");
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
  requiredNativeTableOwnerSlides: [5],
  requiredNativeChartOwnerSlides: [],
  workspaceDir: WORKSPACE_DIR,
  candidatePath,
  finalPath: FINAL_PPTX,
  pythonExecutable: RUNTIME_PYTHON,
  integrityValidatorPath: path.join(SKILL_DIR, "container_tools/inspect_presentation_package_integrity.py"),
  layoutValidatorPath: path.join(SKILL_DIR, "container_tools/inspect_presentation_layout_geometry.py"),
  layoutArgs: ["--expected-slide-size-emu", expectedSlideSizeEmu, "--validate-bullet-geometry", "--validate-heading-fit", "--require-native-table-slide", "5"],
  fontPolicy: { basis: "user_request", families: [FONT] },
  verifyArtifactToolImport: true,
  receiptPath: path.join(stagingDir, "AURA_5_SLIDES_REDESIGN.validation.json"),
});
console.log(JSON.stringify({ final: FINAL_PPTX, font: FONT, result }, null, 2));
