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
- Accept receipt uploads quickly, process AI work in a durable database-backed queue, and resume status polling after a page refresh.
- Optionally fail over once to Ollama for eligible infrastructure failures, with provider and error metadata in the audit trail.
- Prevent stale updates from overwriting another reviewer action through SQL row-version checks.

## Decision Flow

```text
Upload receipt
→ Save PENDING request and return HTTP 202
→ Background worker claims the job with a lease
→ Qwen extracts structured facts using the primary provider or an eligible fallback
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
- Entity Framework Core 8.0.31 and SQL Server
- Qwen3-VL-8B-Instruct through OpenRouter by default; optional local Qwen3-VL-4B through Ollama
- JSON Schema structured output
- Razor Views and the JavaScript Fetch API

## Sprint 1 Verification

- Build: **0 warnings, 0 errors**
- Automated tests: **80/80 passed**
- Verify Harness: **5 smoke-test cases**
- Evaluator reference pack: **15 test cases**
- Extended evaluator: **15 or 30 cases through the production upload endpoint, without an extra UI button**

On 27 September 2026, OpenRouter passed three consecutive Verify batches (15/15 decisions) and three manual uploads of `HoaDon1.jpg` (3/3 `AUTO_APPROVE`) on the current build. Its adjusted per-case latency was median 4.371 seconds and P95 11.317 seconds. The local Ollama build passed five consecutive Verify batches (25/25 decisions) and the same manual upload 3/3, but measured batches took about 303–304 seconds each on an RTX 3050 Laptop 4 GB. These are controlled synthetic-fixture results, not an accuracy claim for independent real-world receipts. OpenRouter therefore remains the default demo provider; Ollama remains an optional local privacy/offline path.

## Evaluation API Key

An evaluator-only OpenRouter key is provided to the organizers through a private channel (The API key is attached in the same folder containing the video). No active API key is stored in this repository, README, source code, screenshots, or demo video.

After receiving the evaluation key, replace the placeholder in the command below. .NET User Secrets stores the value outside the repository:

```powershell
dotnet user-secrets set "OpenRouter:ApiKey" "OPENROUTER_EVALUATION_KEY"
dotnet user-secrets set "OpenRouter:Model" "qwen/qwen3-vl-8b-instruct"
```

## Run Locally

Requirements: Windows, .NET 8 SDK (8.0.425 recommended), and SQL Server LocalDB or SQL Server.

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
5. Open **Lịch sử hành vi** to inspect the audit trail.

The upload endpoint now returns `202 Accepted`. The browser polls the request status while the background worker processes AI inference. For demo fallback on the same laptop, start Ollama first and opt in explicitly:

```powershell
dotnet user-secrets set "Vision:Provider" "OpenRouter"
dotnet user-secrets set "Vision:FallbackEnabled" "true"
dotnet user-secrets set "Vision:FallbackProvider" "Ollama"
```

Fallback only applies to eligible infrastructure failures such as timeout, 429, provider outage, auth/credit failure, or local Ollama unavailability. Schema, semantic, truncated-response, and invalid-request failures do not switch models. Keep fallback disabled when benchmarking one provider in isolation.

### Run the Automated Tests

These tests do not call the paid AI API:

```powershell
dotnet test tests\AURA.Tests\AURA.Tests.csproj
```

### Run the 15/30-case evaluation outside the UI

The dashboard intentionally keeps only the official five-case Verify button. The extended runner uses the same upload endpoint, background worker, provider, policy and audit pipeline, then writes CSV/JSON evidence under the Git-ignored `test_kit/results` directory:

```powershell
.\tools\Invoke-ExtendedDatasetEvaluation.ps1 -BaseUrl "http://localhost:5000" -ManifestPath ".\test_kit\judge-manifest.json" -ImagesDirectory ".\test_kit\images" -MaxCases 15 -InterCaseDelaySeconds 4
```

Run all 30 cases only after checking provider quota and cost. Keep fallback disabled when benchmarking one provider.

## Documentation

- [Architecture and Integration Report](AURA/ARCHITECTURE_AND_INTEGRATION_REPORT.md)
- [Business Rules](AURA/BUSINESS_RULES.md)
- [Workflow Specification](AURA/submission/AURA_WORKFLOW_SPEC.md)
- [Test Cases](AURA/docs/TEST_CASES.md)
- [OpenRouter Benchmark — 27/09/2026](AURA/docs/OPENROUTER_BENCHMARK_2026-09-27.md)
- [Dataset and Extended Evaluation Guide](AURA/docs/DATASET_EVALUATION_GUIDE.md)
- [Deployment and Operations Runbook](AURA/docs/RUNBOOK.md)
- [Optional Local Ollama Setup](AURA/docs/LOCAL_OLLAMA.md)
- [Sprint 2 Implementation Progress](AURA/docs/SPRINT2_IMPLEMENTATION_PROGRESS_2026-09-28.md)

## Sprint 1 Limitations

- Supports one JPG/PNG image up to 5 MB; PDF and multi-page receipts are not supported yet.
- Does not yet include role-based authentication, e-invoice verification, tax-code lookup, currency conversion, or malware scanning.
- OpenRouter remains the default. Ollama fallback is opt-in and practical only on a machine where Ollama and the model are already installed; both providers still need an independent real-receipt benchmark before any production accuracy claim.
- Uploaded receipt evidence is stored by the application instance; production deployment requires managed durable storage and an explicit retention policy.
- AI failure or low confidence always results in human review rather than a fabricated decision.

## Author

**Pham Gia Phu**

VNG Track – Challenge A: The Escalation Referee
