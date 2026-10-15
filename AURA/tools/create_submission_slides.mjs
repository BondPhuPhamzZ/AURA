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

const { resolvePresentationFont, finalizePresentation } = await import(
  pathToFileURL(path.join(SKILL_DIR, "container_tools/artifact_tool_utils.mjs")).href,
);

await fs.mkdir(TMP_DIR, { recursive: true });
await fs.mkdir(path.dirname(FINAL_PPTX), { recursive: true });

const font = resolvePresentationFont();
const deck = Presentation.create({ slideSize: { width: 1280, height: 720 } });

const C = {
  bg: "#091522",
  surface: "#112338",
  surface2: "#162A42",
  line: "#31445C",
  white: "#F4F7FB",
  muted: "#A6B3C4",
  blue: "#5F8FD8",
  cyan: "#45A9BE",
  green: "#4FAF91",
  amber: "#C59A4A",
  red: "#D96B76",
  violet: "#7E78B8",
};

function shape(slide, x, y, w, h, fill = C.surface, radius = 6, line = C.line) {
  return slide.shapes.add({
    geometry: "rect",
    position: { left: x, top: y, width: w, height: h },
    fill,
    line: { style: "solid", fill: line, width: 1 },
    borderRadius: radius,
  });
}

function text(slide, value, x, y, w, h, size = 24, color = C.white, opts = {}) {
  const box = slide.shapes.add({
    geometry: "textbox",
    position: { left: x, top: y, width: w, height: h },
    fill: "none",
    line: { fill: "none", width: 0 },
  });
  box.text = value;
  box.text.style = {
    typeface: font,
    fontSize: size,
    bold: Boolean(opts.bold),
    color,
    alignment: opts.align ?? "left",
    verticalAlignment: opts.valign ?? "top",
    autoFit: opts.autoFit ?? "shrinkText",
    insets: opts.insets ?? { top: 0, right: 0, bottom: 0, left: 0 },
  };
  return box;
}

function pill(slide, label, x, y, w, fill, color = C.bg) {
  const p = shape(slide, x, y, w, 30, fill, 15, fill);
  p.text = label;
  p.text.style = {
    typeface: font,
    fontSize: 14,
    bold: true,
    color,
    alignment: "center",
    verticalAlignment: "middle",
    autoFit: "shrinkText",
    insets: { top: 1, right: 6, bottom: 1, left: 6 },
  };
  return p;
}

function title(slide, number, heading, subheading) {
  text(slide, `0${number}`, 50, 38, 68, 34, 18, C.cyan, { bold: true });
  text(slide, heading, 112, 30, 1110, 50, 34, C.white, { bold: true });
  text(slide, subheading, 112, 82, 1090, 32, 17, C.muted);
  const line = slide.shapes.add({
    geometry: "line",
    position: { left: 54, top: 122, width: 1170, height: 0 },
    fill: "none",
    line: { style: "solid", fill: C.line, width: 1 },
  });
  return line;
}

function footer(slide, number, source = "AURA • Track VNG • 15/10/2026") {
  text(slide, source, 54, 686, 950, 18, 12, C.muted);
  text(slide, String(number), 1170, 684, 54, 20, 13, C.muted, { align: "right", bold: true });
}

function step(slide, n, label, x, y, w, accent, detail = "") {
  const card = shape(slide, x, y, w, 106, C.surface, 6, C.line);
  pill(slide, String(n).padStart(2, "0"), x + 14, y + 14, 42, accent);
  text(slide, label, x + 15, y + 52, w - 30, 26, 17, C.white, { bold: true });
  if (detail) text(slide, detail, x + 15, y + 79, w - 30, 20, 12, C.muted);
  return card;
}

// Slide 1
{
  const s = deck.slides.add();
  s.background.fill = C.bg;
  title(s, 1, "Hoàn ứng hôm nay: nhiều điểm chờ, ít bằng chứng", "AURA biến ngoại lệ thành câu hỏi có thể quyết định — không giao quyền duyệt cho AI.");

  const steps = [
    ["Gửi hóa đơn", "Ảnh + số tiền"],
    ["Đọc thủ công", "Dễ sai định dạng"],
    ["Đối chiếu policy", "Phụ thuộc người xử lý"],
    ["Hỏi quản lý", "Thiếu câu hỏi cụ thể"],
    ["Lưu lịch sử", "Khó truy vết"],
  ];
  const accents = [C.blue, C.blue, C.blue, C.blue, C.blue];
  for (let i = 0; i < steps.length; i++) {
    const x = 55 + i * 184;
    step(s, i + 1, steps[i][0], x, 178, 158, accents[i], steps[i][1]);
    if (i < steps.length - 1) {
      const a = s.shapes.add({
        geometry: "rightArrow",
        position: { left: x + 162, top: 216, width: 18, height: 28 },
        fill: C.line,
        line: { fill: C.line, width: 0 },
      });
      a.sendToBack();
    }
  }

  const insight = shape(s, 1010, 168, 214, 126, "#102A3E", 6, C.cyan);
  text(s, "Điểm nghẽn", 1032, 188, 170, 24, 15, C.cyan, { bold: true });
  text(s, "Không phải OCR — mà là biết khi nào phải dừng và hỏi người.", 1032, 220, 168, 58, 18, C.white, { bold: true });

  text(s, "Thiết kế lại quy trình", 55, 342, 400, 34, 24, C.white, { bold: true });
  const principles = [
    ["AI đọc", "Qwen trích dữ kiện có cấu trúc", C.blue],
    ["Policy quyết định", "C# tất định: FACT → POLICY → AUTHORITY", C.cyan],
    ["Con người chịu trách nhiệm", "Quản lý xử lý ngoại lệ và có thể hoàn tác", C.amber],
  ];
  principles.forEach((p, i) => {
    const x = 55 + i * 390;
    shape(s, x, 395, 365, 190, C.surface2, 6, C.line);
    pill(s, p[0], x + 20, 416, [130, 174, 246][i], p[2]);
    text(s, p[1], x + 20, 468, 325, 58, 21, C.white, { bold: true });
    text(s, i === 0 ? "Không suy đoán field không đọc chắc." : i === 1 ? "Đầu vào nghi vấn không bao giờ auto-approve." : "Audit giữ đủ timeline, UI chỉ hiện một hồ sơ.", x + 20, 535, 325, 38, 14, C.muted);
  });
  footer(s, 1);
  s.speakerNotes.textFrame.setText("Hiện trạng và nguyên tắc thiết kế của AURA; không dùng số liệu hiệu quả chưa đo.");
}

// Slide 2
{
  const s = deck.slides.add();
  s.background.fill = C.bg;
  title(s, 2, "Pipeline E2E: async trước, AI đọc, policy quyết định", "Chín gate chạy thật; con người chỉ quyết định ở nhánh ngoại lệ.");

  const topFlow = [
    ["1  INPUT", "JPG/PNG ≤ 5 MB\nclaim + metadata", C.blue],
    ["2  INTAKE GATE", "extension • MIME • magic\nprivate store + SHA-256", C.blue],
    ["3  DURABLE ENQUEUE", "SQL PENDING\n+ audit AI_QUEUED", C.blue],
    ["4  ASYNC UI", "HTTP 202 + statusUrl\nUI poll • không giữ request", C.cyan],
  ];
  topFlow.forEach((f, i) => {
    const x = 54 + i * 295;
    shape(s, x, 153, 267, 118, C.surface, 6, C.line);
    pill(s, f[0], x + 14, 168, 150, f[2]);
    text(s, f[1], x + 14, 211, 239, 46, 15, C.muted, { align: "center", valign: "middle" });
    if (i < topFlow.length - 1) {
      s.shapes.add({ geometry: "rightArrow", position: { left: x + 270, top: 194, width: 20, height: 31 }, fill: C.line, line: { fill: C.line, width: 0 } });
    }
  });

  s.shapes.add({ geometry: "downArrow", position: { left: 1167, top: 275, width: 28, height: 31 }, fill: C.line, line: { fill: C.line, width: 0 } });

  const workerFlow = [
    ["5  WORKER LEASE", "claim job • lease 600s\nreclaim sau restart", C.blue, 939],
    ["6  AI PERMIT", "1 AI call đồng thời\nOpenRouter → Qwen3.8", C.blue, 644],
    ["7  CONTRACT GATE", "JSON Schema → DTO\nsemantic • repair ≤1", C.blue, 349],
    ["8  POLICY C#", "AUTO_APPROVE\nhoặc ESCALATE_*", C.amber, 54],
  ];
  workerFlow.forEach((f, i) => {
    const x = f[3];
    shape(s, x, 307, 267, 118, C.surface, 6, C.line);
    pill(s, f[0], x + 14, 322, 156, f[2]);
    text(s, f[1], x + 14, 365, 239, 46, 15, C.muted, { align: "center", valign: "middle" });
    if (i < workerFlow.length - 1) {
      s.shapes.add({ geometry: "leftArrow", position: { left: x - 23, top: 348, width: 20, height: 31 }, fill: C.line, line: { fill: C.line, width: 0 } });
    }
  });

  s.shapes.add({ geometry: "downArrow", position: { left: 176, top: 429, width: 28, height: 28 }, fill: C.line, line: { fill: C.line, width: 0 } });
  shape(s, 54, 466, 294, 138, C.surface2, 6, C.line);
  pill(s, "AUTO_APPROVE", 72, 483, 150, C.green);
  text(s, "Ca rõ ràng", 72, 526, 250, 25, 19, C.white, { bold: true });
  text(s, "ghi lịch sử • không chờ quản lý", 72, 560, 250, 24, 14, C.muted);

  shape(s, 382, 466, 570, 138, C.surface2, 6, C.line);
  pill(s, "9  ESCALATE_*", 400, 483, 160, C.amber);
  text(s, "Điểm người #1: nhân viên chuyển tiếp", 400, 526, 520, 24, 18, C.white, { bold: true });
  text(s, "Điểm người #2: quản lý YES / NO", 400, 558, 520, 24, 18, C.white, { bold: true });

  shape(s, 986, 466, 238, 138, C.surface2, 6, C.line);
  pill(s, "AUDIT / UNDO", 1004, 483, 150, C.cyan);
  text(s, "Append-only timeline", 1004, 526, 202, 24, 17, C.white, { bold: true, align: "center" });
  text(s, "Undo = event mới", 1004, 560, 202, 22, 14, C.muted, { align: "center" });

  s.shapes.add({ geometry: "rightArrow", position: { left: 351, top: 516, width: 25, height: 35 }, fill: C.line, line: { fill: C.line, width: 0 } });
  s.shapes.add({ geometry: "rightArrow", position: { left: 956, top: 516, width: 25, height: 35 }, fill: C.line, line: { fill: C.line, width: 0 } });
  footer(s, 2);
  s.speakerNotes.textFrame.setText("Pipeline chi tiết đã đối chiếu với submission/AURA_WORKFLOW_SPEC.md. Polling chỉ cập nhật UI; SQL queue và worker mới xử lý nền. AI chỉ tạo facts, PolicyDecisionEngine C# mới chọn route.");
}

// Slide 3
{
  const s = deck.slides.add();
  s.background.fill = C.bg;
  title(s, 3, "Đối chiếu trước–sau: khẳng định nào đã có bằng chứng?", "Không suy ROI từ fixture; dùng cùng nhiệm vụ, cùng người và cùng tiêu chí.");

  shape(s, 54, 158, 354, 222, C.surface2, 6, C.line);
  pill(s, "TRƯỚC — THỦ CÔNG", 74, 178, 158, C.line, C.white);
  text(s, "Sẽ đo trên 3 người dùng", 74, 226, 312, 28, 22, C.white, { bold: true });
  text(s, "Cùng 3 nhiệm vụ:\n• gửi hồ sơ\n• xử lý ngoại lệ\n• kiểm tra / hoàn tác", 74, 267, 312, 96, 16, C.muted);

  shape(s, 435, 158, 370, 222, C.surface2, 6, C.line);
  pill(s, "SAU — AURA", 455, 178, 128, C.blue);
  text(s, "Nguồn đo có truy vết", 455, 226, 328, 28, 22, C.white, { bold: true });
  text(s, "HTTP 202 / status timestamps\nroute terminal + Audit / Undo\n8/10 prospective • 0 missed\n2 over-escalation fail-safe", 455, 267, 328, 92, 16, C.muted);

  shape(s, 832, 158, 392, 222, C.surface2, 6, C.line);
  pill(s, "KHUNG ĐO", 852, 178, 110, C.cyan);
  text(s, "U_AURA = <ΔT, ΔH, E_missed,\nE_over, A_trace>", 852, 225, 350, 58, 21, C.white, { bold: true, align: "center", valign: "middle" });
  text(s, "ΔT: thời gian route • ΔH: số lần chạm\nE: lỗi quyết định • A: đủ dấu vết", 852, 299, 350, 54, 14, C.muted, { align: "center" });

  text(s, "Mốc kỹ thuật đã đo", 54, 416, 260, 28, 20, C.white, { bold: true });
  const evidence = [
    ["163/163", "test offline", C.blue],
    ["8/10", "0 missed • 2 over", C.green],
    ["5/5", "Verify 65.004s", C.blue],
  ];
  evidence.forEach((e, i) => {
    const x = 54 + i * 185;
    shape(s, x, 458, 164, 122, C.surface2, 6, C.line);
    text(s, e[0], x + 12, 477, 140, 38, 30, e[2], { align: "center", bold: true });
    text(s, e[1], x + 10, 526, 144, 34, 14, C.muted, { align: "center", bold: true, valign: "middle" });
  });

  shape(s, 635, 416, 589, 164, C.surface2, 6, C.line);
  text(s, "Phương pháp paired — gate còn mở", 658, 439, 530, 27, 20, C.white, { bold: true });
  text(s, "1. Cùng người, cùng task\n2. Ghi timestamp + số lần chạm\n3. So median trước/sau\n4. Giữ cả feedback tiêu cực", 658, 481, 522, 80, 16, C.muted);
  text(s, "Phản hồi 3 người dùng dự kiến nhận 16/10; ΔT/ΔH hiện là PENDING, không phải kết quả.", 54, 606, 1170, 29, 15, C.amber, { align: "center", bold: true });
  footer(s, 3);
  s.speakerNotes.textFrame.setText("Nguồn hiện hành 15/10/2026: 163/163 test offline và Release build sạch. Prospective mini-holdout 10 hóa đơn mới đạt 8/10 quyết định đúng, 0 missed, 2 over fail-safe, 0 system/provider/429 error và 28/30 field trọng yếu; mẫu nhỏ, không phải production accuracy. Verify live đạt 5/5 trong 65.004 giây. Regression 12 ảnh byte gốc đạt 10/12 và lượt tuần tự 11/12 chỉ là stability evidence trên dữ liệu đã biết. Khung U_AURA là vector phương pháp đo, không phải điểm ROI. Feedback 3 người dùng chưa nhận nên Delta T và Delta H vẫn để PENDING.");
}

// Slide 4
{
  const s = deck.slides.add();
  s.background.fill = C.bg;
  title(s, 4, "Kiến trúc triển khai: model thay được, policy đứng yên", "ASP.NET Core + SQL chạy thật; fixture/runner được phân định rõ là mô phỏng.");

  const layers = [
    ["GIAO DIỆN", "Razor MVC • HTTP 202 • status polling", C.blue, 54, 154, 760],
    ["QUEUE / WORKER", "SQL job • lease 600s • retry / circuit state", C.blue, 54, 220, 760],
    ["AI ADAPTER", "permit 1 → OpenRouter → Qwen3.8 Flash / Alibaba", C.blue, 54, 286, 760],
    ["CONTRACT", "JSON Schema → ReceiptExtractionDto → semantic → repair ≤1", C.blue, 54, 352, 760],
    ["DECISION", "PolicyDecisionEngine C# → AUTO / ESCALATE → human review", C.amber, 54, 418, 760],
    ["DỮ LIỆU / AUDIT", "EF Core • SQL Server • private storage • Undo event", C.blue, 54, 484, 760],
  ];
  layers.forEach(([label, detail, accent, x, y, w], i) => {
    shape(s, x, y, w, 52, C.surface, 6, C.line);
    pill(s, label, x + 14, y + 11, 146, accent);
    text(s, detail, x + 178, y + 13, w - 196, 26, 16, C.white, { bold: true });
    if (i < layers.length - 1) {
      s.shapes.add({ geometry: "line", position: { left: 434, top: y + 52, width: 0, height: 14 }, fill: "none", line: { style: "solid", fill: C.line, width: 2 } });
    }
  });

  shape(s, 54, 568, 760, 67, "#0E2A3D", 6, C.cyan);
  text(s, "RANH GIỚI QUYỀN HẠN", 75, 582, 230, 22, 14, C.cyan, { bold: true });
  text(s, "AI trả facts; policy C# route; nhân viên/quản lý quyết định ngoại lệ.", 75, 607, 690, 22, 17, C.white, { bold: true });

  shape(s, 856, 154, 368, 218, C.surface2, 6, C.line);
  pill(s, "CHẠY THẬT", 880, 176, 118, C.green);
  text(s, "Upload + validation\nSQL queue + worker\nVision provider + contract\nPolicy C# + Audit + Undo", 880, 225, 305, 128, 19, C.white, { bold: true });

  shape(s, 856, 397, 368, 238, C.surface2, 6, C.line);
  pill(s, "MÔ PHỎNG / TEST", 880, 419, 156, C.amber);
  text(s, "30 ảnh tổng hợp\n5 fixture Verify trên UI\nrunner mở rộng ngoài dashboard", 880, 469, 305, 92, 19, C.white, { bold: true });
  text(s, "Không phải receipt thật / user evidence\nKhông claim production SLA", 880, 573, 305, 46, 14, C.muted);
  footer(s, 4);
  s.speakerNotes.textFrame.setText("Repository và hướng dẫn chạy localhost: https://github.com/BondPhuPhamzZ/AURA. Primary hiện tại: OpenRouter qwen/qwen3.8-flash, reasoning none, route Alibaba. Fallback Ollama mặc định tắt. Thay model có thể đổi facts nhưng không tự đổi business policy; mọi provider phải qua cùng contract, semantic gate và PolicyDecisionEngine.");
}

// Slide 5
{
  const s = deck.slides.add();
  s.background.fill = C.bg;
  title(s, 5, "Giới hạn, tình huống lỗi và bước tiếp theo", "Baseline có người giám sát: lỗi phải dừng an toàn và để lại bằng chứng.");

  const risks = [
    ["JSON / semantic sai", "Repair đúng 1 lần • rồi FACT fail-safe", C.line, C.white],
    ["429 / timeout", "Fallback chỉ khi bật • audit provider", C.red, C.bg],
    ["App restart", "DB job + lease • worker reclaim", C.line, C.white],
    ["5 upload đồng thời", "Queue hoàn tất 5/5 • P95 93.684s • chưa là SLA", C.amber, C.bg],
    ["Ảnh mơ hồ / giả", "FACT ưu tiên • không auto-approve", C.line, C.white],
  ];
  risks.forEach((r, i) => {
    const y = 157 + i * 84;
    shape(s, 54, y, 746, 66, C.surface, 6, C.line);
    pill(s, r[0], 72, y + 18, 180, r[2], r[3]);
    text(s, r[1], 276, y + 19, 500, 28, 17, C.white, { bold: true });
  });

  shape(s, 842, 157, 382, 242, C.surface2, 6, C.line);
  text(s, "ĐÃ BÀN GIAO", 869, 180, 326, 24, 15, C.green, { bold: true });
  const todo = ["DB-backed queue + HTTP 202", "163/163 test • build sạch", "Holdout 8/10 • 0 missed", "Verify 5/5 • 65.004s"];
  todo.forEach((t, i) => {
    pill(s, `${i + 1}`, 869, 225 + i * 40, 30, C.green);
    text(s, t, 914, 228 + i * 40, 270, 24, 16, C.white, { bold: true });
  });

  shape(s, 842, 424, 382, 172, C.surface2, 6, C.line);
  text(s, "RỦI RO / ĐIỀU KIỆN CÒN MỞ", 869, 447, 326, 24, 15, C.red, { bold: true });
  text(s, "• phản hồi 3 người dùng: chờ 16/10\n• limiter/lock mới ở một process\n• PDF • RBAC • object storage\n• evidence overlay mới là prototype", 869, 485, 326, 96, 15, C.white, { bold: true });

  text(s, "Sau feedback: chọn 1 thay đổi truy vết → chạy lại build/test/Verify; không tune bằng holdout.", 842, 610, 382, 39, 14, C.amber, { align: "center", bold: true });
  footer(s, 5, "AURA • Giới hạn hiện tại, không phải lời hứa marketing");
  s.speakerNotes.textFrame.setText(
    "Nguồn kỹ thuật trong repo: ReceiptProcessingWorker.cs, ReceiptProcessingQueue.cs, VisionInvocationCoordinator.cs, ReceiptExtractionContract.cs, ReceiptSemanticValidator.cs, PolicyDecisionEngine.cs, docs/CURRENT_PROJECT_STATUS.md và docs/EVALUATION_EVIDENCE.md. Fallback opt-in và mặc định tắt. Burst 5 upload hoàn tất 5/5; E2E P95 93.684 giây là queue integrity trên một process, không phải production SLA. Verify giữ lock theo process để tránh nhân batch fixture. Ba phiên người dùng thật và distributed limiter/lock vẫn là gate mở. Evidence localization chỉ là prototype cô lập, chưa nằm trong production pipeline.",
  );
}

for (let i = 0; i < deck.slides.length; i++) {
  const preview = await deck.export({ slide: deck.slides.getItemAt(i), format: "png", scale: 1 });
  await fs.writeFile(path.join(TMP_DIR, `slide-${i + 1}.png`), new Uint8Array(await preview.arrayBuffer()));
}

const stagingDir = path.join(TMP_DIR, "finalizer");
await fs.mkdir(stagingDir, { recursive: true });
const candidatePath = path.join(stagingDir, "AURA_5_SLIDES.candidate.pptx");
await (await PresentationFile.exportPptx(deck)).save(candidatePath);

const requirements = {
  explicitTotalSlideCount: 5,
  requiredNativeTableOwnerSlides: [],
  requiredNativeChartOwnerSlides: [],
};
const fontPolicy = { basis: "design", families: [font] };
const expectedSlideSizeEmu = "12192000,6858000";

const result = await finalizePresentation({
  ...requirements,
  workspaceDir: WORKSPACE_DIR,
  candidatePath,
  finalPath: FINAL_PPTX,
  pythonExecutable: RUNTIME_PYTHON,
  integrityValidatorPath: path.join(SKILL_DIR, "container_tools/inspect_presentation_package_integrity.py"),
  layoutValidatorPath: path.join(SKILL_DIR, "container_tools/inspect_presentation_layout_geometry.py"),
  layoutArgs: ["--expected-slide-size-emu", expectedSlideSizeEmu, "--validate-bullet-geometry", "--validate-heading-fit"],
  requiredNativeTableOwnerSlides: [],
  fontPolicy,
  verifyArtifactToolImport: true,
  receiptPath: path.join(stagingDir, "AURA_5_SLIDES.validation.json"),
});

console.log(JSON.stringify({ final: FINAL_PPTX, font, result }, null, 2));
