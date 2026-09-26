# AURA - Automated Underwriting & Reimbursement AI

AURA automates the first-line review of employee reimbursement receipts. It was developed for the **VNG Track – Challenge A: The Escalation Referee** at the MLAI Hackathon 2026.

Qwen Vision, accessed through OpenRouter by default or optional local Ollama, extracts structured facts from receipt images. It does not make the final business decision. The backend validates semantic consistency, then a deterministic C# policy engine decides whether a request can be approved automatically or must be escalated to a human reviewer.

## Submission Materials

| Item | Link |
| --- | --- |
| Demo video & API Key | [Watch the demo & Get API Key](https://drive.google.com/drive/folders/1_EHs9-KghK2JWpQGRAu_jLWl9WmBkMLc?usp=sharing) |
| Presentation | [Download AURA 5 Slides](https://raw.githubusercontent.com/BondPhuPhamzZ/AURA/master/AURA/submission/AURA_5_SLIDES.pptx) |
| Build Log | [Download AURA Build Log](https://raw.githubusercontent.com/BondPhuPhamzZ/AURA/master/AURA/submission/AURA_BUILD_LOG.docx) |

## What AURA Can Do

- Read single-page JPG and PNG receipt images with Qwen Vision.
- Return structured receipt facts using a JSON Schema contract.
- Detect semantic inconsistencies and allow one targeted AI re-read before safe human escalation.
- Apply reimbursement rules through a deterministic C# policy engine.
- Automatically approve clear, policy-compliant requests.
- Escalate uncertain, exceptional, or unauthorized requests for human review.
- Let an employee forward a case and let a manager approve, reject, or undo the latest decision.
- Record the request lifecycle in an audit trail.
- Run a five-case Verify Harness through the real application pipeline.
- Prevent overlapping workflow writes and stale updates from creating duplicate or conflicting data.

## Decision Flow

```text
Upload receipt
→ Qwen extracts structured facts
→ Backend validates the extracted data
→ C# policy engine makes a deterministic decision
→ AUTO_APPROVE or ESCALATE_*
→ Employee forwards the exception
→ Manager makes the human decision
→ Audit trail records the outcome
```

AURA does not fine-tune Qwen and does not give the model final decision authority. The model acts as a document reader; `PolicyDecisionEngine` remains the source of truth for business decisions. When AI output is missing, invalid, or uncertain, the request is routed to a human instead of being guessed.

## Technology

- ASP.NET Core 8 MVC
- Entity Framework Core and SQL Server
- Qwen3-VL-8B-Instruct through OpenRouter by default; optional local Qwen3-VL-4B through Ollama
- JSON Schema structured output
- Razor Views and the JavaScript Fetch API

## Sprint 1 Verification

- Build: **0 warnings, 0 errors**
- Automated tests: **70/70 passed**
- Verify Harness: **5 smoke-test cases**
- Evaluator reference pack: **15 test cases**

The local Ollama build passed five consecutive Verify batches (25/25 decisions across the five controlled synthetic fixtures) on 27 September 2026. A separate manual upload of `HoaDon1.jpg` also returned `AUTO_APPROVE` in 3/3 repeated runs. These controlled results must not be interpreted as an accuracy claim for independent real-world receipts. OpenRouter remains the default provider until the same build is compared on a fixed benchmark.

## Evaluation API Key

An evaluator-only OpenRouter key is provided to the organizers through a private channel (The API key is attached in the same folder containing the video). No active API key is stored in this repository, README, source code, screenshots, or demo video.

After receiving the evaluation key, replace the placeholder in the command below. .NET User Secrets stores the value outside the repository:

```powershell
dotnet user-secrets set "OpenRouter:ApiKey" "OPENROUTER_EVALUATION_KEY"
dotnet user-secrets set "OpenRouter:Model" "qwen/qwen3-vl-8b-instruct"
```

## Run Locally

Requirements: Windows, .NET 8 SDK, and SQL Server LocalDB or SQL Server.

```powershell
git clone https://github.com/BondPhuPhamzZ/AURA.git
cd AURA\AURA

dotnet tool restore
dotnet restore

dotnet user-secrets set "OpenRouter:ApiKey" "OPENROUTER_EVALUATION_KEY"
dotnet user-secrets set "OpenRouter:Model" "qwen/qwen3-vl-8b-instruct"

dotnet ef database update
dotnet run
```

Open the localhost address printed in the terminal, then (the quoted labels below match the Vietnamese UI):

1. Select **Chạy Verify Harness** to smoke-test five cases through the live AI pipeline.
2. Or upload a JPG/PNG receipt, enter the claimed amount, and select **AI tự động kiểm**.
3. For an `ESCALATE_*` result, select **Chuyển tiếp**.
4. Open the **Quản lý** tab and select **Đồng ý duyệt** or **Từ chối duyệt**.
5. Open **Lịch Sử Của Hệ Thống** to inspect the audit trail.

### Run the Automated Tests

These tests do not call the paid AI API:

```powershell
dotnet test tests\AURA.Tests\AURA.Tests.csproj
```

## Documentation

- [Architecture and Integration Report](AURA/ARCHITECTURE_AND_INTEGRATION_REPORT.md)
- [Business Rules](AURA/BUSINESS_RULES.md)
- [Workflow Specification](AURA/submission/AURA_WORKFLOW_SPEC.md)
- [Test Cases](AURA/docs/TEST_CASES.md)
- [Deployment and Operations Runbook](AURA/docs/RUNBOOK.md)
- [Optional Local Ollama Setup](AURA/docs/LOCAL_OLLAMA.md)

## Sprint 1 Limitations

- Supports one JPG/PNG image up to 5 MB; PDF and multi-page receipts are not supported yet.
- Does not yet include role-based authentication, e-invoice verification, tax-code lookup, currency conversion, or malware scanning.
- The default evaluation path depends on OpenRouter; the optional Ollama path passed the controlled fixture gate but still needs an independent receipt benchmark before promotion.
- Uploaded receipt evidence is stored by the application instance; production deployment requires managed durable storage and an explicit retention policy.
- AI failure or low confidence always results in human review rather than a fabricated decision.

## Author

**Pham Gia Phu**

VNG Track – Challenge A: The Escalation Referee
