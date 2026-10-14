import crypto from "node:crypto";
import fs from "node:fs/promises";
import path from "node:path";
import { pathToFileURL } from "node:url";

const workspaceDir = path.resolve(".");
const sourcePath = path.join(workspaceDir, "submission", "AURA_5_SLIDES_FINAL.pptx");
const qaDir = path.join(workspaceDir, ".tmp", "final-artifact-qa", "slides");
const candidatePath = path.join(qaDir, "candidate.pptx");
const finalPath = path.join(qaDir, "AURA_5_SLIDES_FINAL.cleaned.pptx");
const receiptPath = path.join(workspaceDir, ".tmp", "final-artifact-qa", "slide-validation.json");
const skillDir = "C:/Users/bondp/.codex/plugins/cache/openai-primary-runtime/presentations/26.1011.11113/skills/presentations";
const modulesDir = "C:/Users/bondp/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules";
const runtimePython = "C:/Users/bondp/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe";
const artifactPath = path.join(modulesDir, "@oai/artifact-tool/dist/artifact_tool.mjs");
const { FileBlob, PresentationFile } = await import(pathToFileURL(artifactPath).href);
const { finalizePresentation } = await import(pathToFileURL(path.join(skillDir, "container_tools", "artifact_tool_utils.mjs")).href);

await fs.mkdir(qaDir, { recursive: true });
const deck = await PresentationFile.importPptx(await FileBlob.load(sourcePath));

function replaceText(id, oldText, newText) {
  const target = deck.resolve(id);
  if (!target) throw new Error(`Missing target ${id}`);
  target.text.replace(oldText, newText);
}

for (const id of ["sh/mpozalcz", "sh/fypcfqdg", "sh/l4fi9cza", "sh/ilwf69kv"]) {
  replaceText(id, "AURA • Track A • 13/10/2026", "AURA • Track A • 14/10/2026");
}

const slide3Notes = deck.resolve("nt/ofy9wn61");
if (!slide3Notes) throw new Error("Missing slide 3 notes");
slide3Notes.textFrame.setText(
  "Nguồn hiện hành 14/10/2026: 163/163 test offline và Release build sạch. Candidate Qwen3.8 Flash qua OpenRouter/Alibaba, reasoning none. Prospective mini-holdout 10 hóa đơn mới đạt 8/10 quyết định đúng, 0 missed, 2 over fail-safe, 0 system/provider/429 error và 28/30 field trọng yếu; mẫu nhỏ và có redaction confounder, không phải production accuracy. Regression 12 ảnh byte gốc đạt 10/12; lượt chạy tuần tự lại trên dữ liệu đã biết đạt 11/12 và chỉ được dùng làm evidence ổn định. Burst 5 upload hoàn tất 5/5; AI concurrency vẫn là 1 nên queue wait tăng. Verify live đạt 5/5 trong 65.004 giây. Ollama chỉ là fallback local có giám sát. Ba user session vẫn là gate chưa hoàn tất.",
);

const slide5Notes = deck.resolve("nt/i107q5of");
if (!slide5Notes) throw new Error("Missing slide 5 notes");
slide5Notes.textFrame.setText(
  "Nguồn kỹ thuật trong repo: ReceiptProcessingWorker.cs, ReceiptExtractionContract.cs, PolicyDecisionEngine.cs, VietnamTime.cs, tools/Invoke-ExtendedDatasetEvaluation.ps1, docs/CURRENT_PROJECT_STATUS.md và docs/EVALUATION_EVIDENCE.md. Fallback vẫn opt-in và mặc định tắt; Ollama chỉ bật local sau smoke. Qwen3.8 Flash qua OpenRouter/Alibaba, reasoning none, là primary candidate. Prospective holdout đạt 8/10 với 0 missed và 2 over; sequential repeat trên dữ liệu đã biết đạt 11/12; Verify live đạt 5/5 trong 65.004 giây. Burst 5 upload hoàn tất 5/5 nhưng phép đo một process không phải production SLA. Ba phiên người dùng thật, video chính thức và kiểm tra quyền Team Drive vẫn là gate mở.",
);

const inspection = await deck.inspect({ kind: "textbox,notes", maxChars: 60000 });
for (const stale of ["CURRENT_PROJECT_STATUS_2026", "OFFICIAL_HOLDOUT10_2026", "161/161"]) {
  if (inspection.ndjson.includes(stale)) throw new Error(`Stale slide claim remains: ${stale}`);
}

await (await PresentationFile.exportPptx(deck)).save(candidatePath);
await fs.rm(finalPath, { force: true });
await fs.rm(receiptPath, { force: true });
const sourceSha256 = crypto.createHash("sha256").update(await fs.readFile(sourcePath)).digest("hex");
const result = await finalizePresentation({
  explicitTotalSlideCount: 5,
  requiredNativeTableOwnerSlides: [],
  requiredNativeChartOwnerSlides: [],
  workspaceDir,
  candidatePath,
  finalPath,
  pythonExecutable: runtimePython,
  integrityValidatorPath: path.join(skillDir, "container_tools", "inspect_presentation_package_integrity.py"),
  layoutValidatorPath: path.join(skillDir, "container_tools", "inspect_presentation_layout_geometry.py"),
  layoutArgs: [
    "--expected-slide-size-emu", "12192000,6858000",
    "--validate-bullet-geometry",
    "--validate-heading-fit",
  ],
  fontPolicy: {
    basis: "reference",
    families: ["Arial"],
    referencePath: sourcePath,
    referenceSha256: sourceSha256,
  },
  verifyArtifactToolImport: true,
  receiptPath,
});

console.log(JSON.stringify(result, null, 2));
