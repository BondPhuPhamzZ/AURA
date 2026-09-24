# AURA - Automated Underwriting & Reimbursement AI

AURA automates the first-line review of employee reimbursement receipts. It was developed for the **VNG Track – Challenge A: The Escalation Referee** at the MLAI Hackathon 2026.

Qwen Vision, accessed through OpenRouter, extracts structured facts from receipt images. It does not make the final business decision. A deterministic C# policy engine decides whether a request can be approved automatically or must be escalated to a human reviewer.

## Submission Materials

| Item | Link |
| --- | --- |
| Demo video | [Watch the demo (under 3 minutes)](https://drive.google.com/drive/folders/1_EHs9-KghK2JWpQGRAu_jLWl9WmBkMLc?usp=sharing) |
| Presentation | [Download AURA 5 Slides](https://raw.githubusercontent.com/BondPhuPhamzZ/AURA/master/AURA/submission/AURA_5_SLIDES.pptx) |
| Build Log | [Download AURA Build Log](https://raw.githubusercontent.com/BondPhuPhamzZ/AURA/master/AURA/submission/AURA_BUILD_LOG.docx) |

## What AURA Can Do

- Read single-page JPG and PNG receipt images with Qwen Vision.
- Return structured receipt facts using a JSON Schema contract.
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
- Qwen3-VL-8B-Instruct through OpenRouter
- JSON Schema structured output
- Razor Views and the JavaScript Fetch API

## Sprint 1 Verification

- Build: **0 warnings, 0 errors**
- Automated tests: **56/56 passed**
- Verify Harness: **5 smoke-test cases**
- Evaluator reference pack: **15 test cases**

The recorded 5/5 Verify result uses controlled synthetic fixtures. It must not be interpreted as an accuracy claim for independent real-world receipts.

## Evaluation API Key

An evaluator-only OpenRouter key is provided to the organizers through a private channel. No active API key is stored in this repository, README, source code, screenshots, or demo video.

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

Open the localhost address printed in the terminal, then:

1. Select **Run Verify Harness** to smoke-test five cases through the live AI pipeline.
2. Or upload a JPG/PNG receipt, enter the claimed amount, and select **Run AI Review**.
3. For an `ESCALATE_*` result, select **Forward**.
4. Open the manager tab and select **Approve** or **Reject**.
5. Open **System History** to inspect the audit trail.

### Run the Automated Tests

These tests do not call the paid AI API:

```powershell
dotnet test tests\AURA.Tests\AURA.Tests.csproj
```

## Documentation

- [Architecture and Integration Report](ARCHITECTURE_AND_INTEGRATION_REPORT.md)
- [Business Rules](BUSINESS_RULES.md)
- [Workflow Specification](submission/AURA_WORKFLOW_SPEC.md)
- [Test Cases](docs/TEST_CASES.md)
- [Deployment and Operations Runbook](docs/RUNBOOK.md)

## Sprint 1 Limitations

- Supports one JPG/PNG image up to 5 MB; PDF and multi-page receipts are not supported yet.
- Does not yet include role-based authentication, e-invoice verification, tax-code lookup, currency conversion, or malware scanning.
- Depends on OpenRouter and the selected Qwen provider for vision extraction.
- Uploaded receipt evidence is stored by the application instance; production deployment requires managed durable storage and an explicit retention policy.
- AI failure or low confidence always results in human review rather than a fabricated decision.

## Author

**Pham Gia Phu**

VNG Track – Challenge A: The Escalation Referee
