import fs from "node:fs/promises";
import path from "node:path";
import { pathToFileURL } from "node:url";
import { Presentation, PresentationFile } from "@oai/artifact-tool";

const { SKILL_DIR, TMP_DIR, WORKSPACE_DIR, FINAL_PPTX, RUNTIME_PYTHON } = process.env;
for (const [name, value] of Object.entries({ SKILL_DIR, TMP_DIR, WORKSPACE_DIR, FINAL_PPTX, RUNTIME_PYTHON })) {
  if (!value || !path.isAbsolute(value)) throw new Error(`${name} must be an absolute path`);
}

const { resolvePresentationFont, finalizePresentation } = await import(
  pathToFileURL(path.join(SKILL_DIR, "container_tools/artifact_tool_utils.mjs")).href,
);

await fs.mkdir(TMP_DIR, { recursive: true });
await fs.mkdir(path.dirname(FINAL_PPTX), { recursive: true });

const font = resolvePresentationFont();
const deck = Presentation.create({ slideSize: { width: 1280, height: 720 } });

const C = {
  bg: "#081426",
  surface: "#10223B",
  surface2: "#142A47",
  line: "#2A4262",
  white: "#F5F8FF",
  muted: "#9FB1CA",
  blue: "#4D8DFF",
  cyan: "#22D3EE",
  green: "#2DD4A7",
  amber: "#F8C14C",
  red: "#FF6B7A",
  violet: "#A78BFA",
};

function shape(slide, x, y, w, h, fill = C.surface, radius = "rounded-xl", line = C.line) {
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
  const p = shape(slide, x, y, w, 30, fill, "rounded-xl", fill);
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
  text(slide, `0${number}`, 54, 38, 54, 34, 18, C.cyan, { bold: true });
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

function footer(slide, number, source = "AURA • Track A • 22/09/2026") {
  text(slide, source, 54, 686, 950, 18, 12, C.muted);
  text(slide, `${number}/5`, 1160, 684, 64, 20, 13, C.muted, { align: "right", bold: true });
}

function step(slide, n, label, x, y, w, accent, detail = "") {
  const card = shape(slide, x, y, w, 106, C.surface, "rounded-xl", C.line);
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
  const accents = [C.blue, C.violet, C.amber, C.red, C.muted];
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

  const insight = shape(s, 1010, 168, 214, 126, "#102A3E", "rounded-xl", C.cyan);
  text(s, "Điểm nghẽn", 1032, 188, 170, 24, 15, C.cyan, { bold: true });
  text(s, "Không phải OCR — mà là biết khi nào phải dừng và hỏi người.", 1032, 220, 168, 58, 18, C.white, { bold: true });

  text(s, "Thiết kế lại quy trình", 55, 342, 400, 34, 24, C.white, { bold: true });
  const principles = [
    ["AI đọc", "Qwen trích dữ kiện có cấu trúc", C.blue],
    ["Policy quyết định", "C# tất định: FACT → POLICY → AUTHORITY", C.green],
    ["Con người chịu trách nhiệm", "Quản lý xử lý ngoại lệ và có thể hoàn tác", C.amber],
  ];
  principles.forEach((p, i) => {
    const x = 55 + i * 390;
    shape(s, x, 395, 365, 190, C.surface2, "rounded-xl", C.line);
    pill(s, p[0], x + 20, 416, 130, p[2]);
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
  title(s, 2, "Một luồng — hai điểm con người quyết định", "Input và AI là dữ kiện; quyết định nghiệp vụ nằm ở policy và quản lý.");

  const flow = [
    ["Input", "JPG/PNG ≤5MB\n+ số tiền", C.blue],
    ["Kiểm file", "MIME • magic bytes\nSHA-256", C.cyan],
    ["Vision", "Qwen/OpenRouter\nJSON Schema", C.violet],
    ["Policy", "C# tất định\nFACT → POLICY → AUTHORITY", C.green],
    ["Kết quả", "AUTO_APPROVE\nhoặc ESCALATE_*", C.amber],
  ];
  flow.forEach((f, i) => {
    const x = 54 + i * 237;
    shape(s, x, 168, 205, 142, C.surface, "rounded-xl", f[2]);
    pill(s, String(i + 1), x + 15, 184, 34, f[2]);
    text(s, f[0], x + 58, 184, 132, 26, 17, C.white, { bold: true });
    text(s, f[1], x + 16, 230, 173, 62, 16, C.muted, { align: "center", valign: "middle" });
    if (i < flow.length - 1) {
      s.shapes.add({ geometry: "rightArrow", position: { left: x + 210, top: 220, width: 22, height: 32 }, fill: C.line, line: { fill: C.line, width: 0 } });
    }
  });

  text(s, "Điểm người #1", 58, 357, 162, 26, 15, C.cyan, { bold: true });
  shape(s, 54, 390, 535, 218, C.surface2, "rounded-xl", C.cyan);
  text(s, "Nhân viên xác nhận chuyển tiếp", 78, 414, 470, 32, 23, C.white, { bold: true });
  text(s, "• chỉ hồ sơ ESCALATE_*\n• chuyển từng hồ sơ hoặc tất cả\n• tab quản lý cập nhật ngay", 78, 463, 450, 94, 18, C.muted);
  pill(s, "Không chuyển âm thầm", 78, 564, 190, C.cyan);

  text(s, "Điểm người #2", 650, 357, 162, 26, 15, C.amber, { bold: true });
  shape(s, 645, 390, 579, 218, C.surface2, "rounded-xl", C.amber);
  text(s, "Quản lý trả lời câu hỏi cụ thể", 670, 414, 515, 32, 23, C.white, { bold: true });
  shape(s, 670, 466, 235, 67, "#12352F", "rounded-xl", C.green);
  text(s, "ĐỒNG Ý", 688, 478, 199, 24, 16, C.green, { bold: true, align: "center" });
  text(s, "duyệt ngoại lệ / tiếp nhận", 688, 505, 199, 18, 13, C.muted, { align: "center" });
  shape(s, 929, 466, 235, 67, "#3A202C", "rounded-xl", C.red);
  text(s, "TỪ CHỐI", 947, 478, 199, 24, 16, C.red, { bold: true, align: "center" });
  text(s, "trả lại / yêu cầu bổ sung", 947, 505, 199, 18, 13, C.muted, { align: "center" });
  text(s, "Audit append-only • hoàn tác quyết định gần nhất", 670, 558, 494, 24, 15, C.white, { align: "center", bold: true });
  footer(s, 2);
  s.speakerNotes.textFrame.setText("Workflow chi tiết: submission/AURA_WORKFLOW_SPEC.md");
}

// Slide 3
{
  const s = deck.slides.add();
  s.background.fill = C.bg;
  title(s, 3, "Tác động phải đo được — không tự tuyên bố", "AURA phân biệt kết quả kỹ thuật đã xác minh với tác động vận hành cần pilot.");

  shape(s, 54, 162, 560, 232, C.surface, "rounded-xl", C.red);
  pill(s, "TRƯỚC", 76, 182, 86, C.red);
  text(s, "Đọc và phân loại thủ công", 77, 231, 500, 32, 24, C.white, { bold: true });
  text(s, "• nhiều lần chạm trên hồ sơ routine\n• câu hỏi escalation không nhất quán\n• lịch sử rời rạc, khó giải thích", 77, 278, 505, 92, 18, C.muted);

  shape(s, 666, 162, 558, 232, C.surface, "rounded-xl", C.green);
  pill(s, "SAU — MỤC TIÊU", 690, 182, 145, C.green);
  text(s, "Tự xử lý ca rõ ràng", 690, 231, 500, 32, 24, C.white, { bold: true });
  text(s, "• con người chỉ nhận ngoại lệ\n• câu hỏi có dữ kiện và lựa chọn\n• một hồ sơ, một timeline truy vết", 690, 278, 500, 92, 18, C.muted);

  text(s, "Bằng chứng đã có", 54, 428, 250, 28, 20, C.white, { bold: true });
  const evidence = [
    ["54/54", "test offline", C.blue],
    ["5/5", "Verify v2 local", C.green],
    ["15", "ca BGK khóa", C.violet],
  ];
  evidence.forEach((e, i) => {
    const x = 54 + i * 185;
    shape(s, x, 473, 164, 119, C.surface2, "rounded-xl", e[2]);
    text(s, e[0], x + 12, 491, 140, 42, 31, e[2], { align: "center", bold: true });
    text(s, e[1], x + 12, 543, 140, 22, 14, C.muted, { align: "center", bold: true });
  });

  shape(s, 635, 428, 589, 164, C.surface2, "rounded-xl", C.line);
  text(s, "Đo trong pilot", 660, 449, 200, 28, 20, C.white, { bold: true });
  const metrics = ["Touchless rate", "Review time", "Over-escalation", "Missed escalation"];
  metrics.forEach((m, i) => {
    const x = 660 + (i % 2) * 270;
    const y = 492 + Math.floor(i / 2) * 43;
    pill(s, m, x, y, 236, i < 2 ? C.blue : C.amber, i < 2 ? C.bg : C.bg);
  });
  text(s, "Baseline và target chỉ điền sau khi có dữ liệu người dùng thật.", 635, 610, 589, 34, 15, C.amber, { align: "center", bold: true });
  footer(s, 3);
  s.speakerNotes.textFrame.setText("Nguồn nội bộ: 54 test offline; người dùng xác nhận Verify v2 5/5 local ngày 22/09/2026. Không suy rộng thành accuracy thực tế.");
}

// Slide 4
{
  const s = deck.slides.add();
  s.background.fill = C.bg;
  title(s, 4, "Kiến trúc: model thay được, policy đứng yên", "Vertical slice chạy thật trên ASP.NET Core; Test Kit chỉ là dữ liệu mô phỏng.");

  const layers = [
    ["GIAO DIỆN", "Razor MVC • fetch/fragment • polling 10s", C.blue, 54, 162, 760],
    ["ỨNG DỤNG", "Applicant / Verify / Reviewer • state validation", C.cyan, 54, 248, 760],
    ["QUYẾT ĐỊNH", "IVisionExtractor → PolicyDecisionEngine → EscalationWorkflow", C.green, 54, 334, 760],
    ["DỮ LIỆU", "EF Core • SQL Server • private receipt storage • AuditLogs", C.violet, 54, 420, 760],
  ];
  layers.forEach(([label, detail, accent, x, y, w], i) => {
    shape(s, x, y, w, 66, C.surface, "rounded-xl", accent);
    pill(s, label, x + 16, y + 18, 132, accent);
    text(s, detail, x + 168, y + 20, w - 190, 28, 17, C.white, { bold: true });
    if (i < layers.length - 1) {
      s.shapes.add({ geometry: "line", position: { left: 434, top: y + 66, width: 0, height: 20 }, fill: "none", line: { style: "solid", fill: C.line, width: 2 } });
    }
  });

  shape(s, 54, 535, 760, 100, "#0E2A3D", "rounded-xl", C.cyan);
  text(s, "OPENROUTER / QWEN", 75, 554, 205, 24, 15, C.cyan, { bold: true });
  text(s, "Trích xuất JSON có cấu trúc • không có quyền quyết định", 75, 584, 700, 28, 19, C.white, { bold: true });

  shape(s, 856, 162, 368, 210, C.surface2, "rounded-xl", C.green);
  pill(s, "CHẠY THẬT", 880, 184, 118, C.green);
  text(s, "Upload + validation\nQwen/OpenRouter\nPolicy C#\nSQL + Audit + Undo", 880, 235, 305, 118, 20, C.white, { bold: true });

  shape(s, 856, 397, 368, 238, C.surface2, "rounded-xl", C.amber);
  pill(s, "MÔ PHỎNG", 880, 419, 118, C.amber);
  text(s, "30 ảnh tổng hợp\n5 ca Verify\n15 ca giao BGK", 880, 470, 305, 92, 20, C.white, { bold: true });
  text(s, "Không có tax/e-invoice lookup\nKhông có user role thật", 880, 574, 305, 44, 14, C.muted);
  footer(s, 4);
  s.speakerNotes.textFrame.setText("Repository: https://github.com/BondPhuPhamzZ/AURA — Live URL cần điền sau deploy.");
}

// Slide 5
{
  const s = deck.slides.add();
  s.background.fill = C.bg;
  title(s, 5, "Giới hạn được nói thẳng — và cách fail-safe", "Không gọi một retry là circuit breaker; không biến lỗi provider thành quyết định nghiệp vụ.");

  const risks = [
    ["429 / hết credit", "Không retry • dừng phần Verify còn lại", C.red],
    ["5xx tạm thời", "Retry tối đa 1 lần • rồi kiểm thủ công", C.amber],
    ["JSON lệch schema", "Chuẩn hóa giới hạn • fail-safe nếu vẫn sai", C.violet],
    ["Ảnh mơ hồ / giả", "FACT ưu tiên • không auto-approve", C.cyan],
    ["Storage / deploy", "SQL Server + private folder bền", C.blue],
  ];
  risks.forEach((r, i) => {
    const y = 157 + i * 84;
    shape(s, 54, y, 746, 66, C.surface, "rounded-xl", C.line);
    pill(s, r[0], 72, y + 18, 180, r[2]);
    text(s, r[1], 276, y + 19, 500, 28, 17, C.white, { bold: true });
  });

  shape(s, 842, 157, 382, 242, C.surface2, "rounded-xl", C.green);
  text(s, "TRƯỚC KHI NỘP", 869, 180, 326, 24, 15, C.green, { bold: true });
  const todo = ["Deploy SmarterASP.NET", "Smoke 1 ảnh + Verify 1 lượt", "Quay video < 3 phút", "Điền Live URL đồng nhất"];
  todo.forEach((t, i) => {
    pill(s, `${i + 1}`, 869, 225 + i * 40, 30, C.green);
    text(s, t, 914, 228 + i * 40, 270, 24, 16, C.white, { bold: true });
  });

  shape(s, 842, 424, 382, 153, "#342334", "rounded-xl", C.red);
  text(s, "CHƯA CÓ TRONG SPRINT 1", 869, 447, 326, 24, 15, C.red, { bold: true });
  text(s, "PDF/nhiều trang • authentication\ntax lookup • object storage\nbenchmark dữ liệu độc lập", 869, 487, 326, 72, 17, C.white, { bold: true });

  text(s, "Lộ trình: benchmark Qwen3-VL 4B self-host khi có dữ liệu và phần cứng phù hợp.", 842, 601, 382, 44, 14, C.amber, { align: "center", bold: true });
  footer(s, 5, "AURA • Giới hạn hiện tại, không phải lời hứa marketing");
  s.speakerNotes.textFrame.setText(
    "Nguồn deploy: https://render.com/docs/free ; https://www.smarterasp.net/support/kb/a2437/how-to-set-environment-variable-for-your-account.aspx . Cơ chế retry đối chiếu OpenRouterVisionExtractorService.cs.",
  );
}

for (let i = 0; i < deck.slides.length; i++) {
  const preview = await deck.export({ slide: deck.slides.getItemAt(i), format: "png", scale: 1 });
  await fs.writeFile(path.join(TMP_DIR, `slide-${i + 1}.png`), new Uint8Array(await preview.arrayBuffer()));
}

const stagingDir = path.join(WORKSPACE_DIR, ".tmp", "slides-finalizer");
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
