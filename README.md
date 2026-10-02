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
- Losslessly canonicalize a sole printed paper purchase/transaction date while never promoting a delivery/completion date; reserve one targeted AI re-read for genuine remaining inconsistencies.
- Extract printed receipt-level discounts into `discountAmount`, ignore non-monetary loyalty/identifier values, and reconcile subtotal, tax, discount, line items, and final payable amount before policy evaluation.
- Fail safe when a paper receipt's purchased-item area is obscured or unreadable: an empty `lineItems` result is repaired once and then routed to `ESCALATE_FACT`, never treated as proof that no prohibited item exists.
- Distinguish formal invoice numbers, receipt/bill numbers, and per-purchase transaction references; store/POS/MID/TID/pager identifiers never satisfy transaction traceability.
- Apply reimbursement rules through a deterministic C# policy engine.
- Automatically approve clear, policy-compliant requests.
- Escalate uncertain, exceptional, or unauthorized requests for human review.
- Let an employee forward a case and let a manager approve, reject, or undo the latest decision.
- Record the request lifecycle in an audit trail.
- Run a five-case Verify Harness through the real application pipeline.
- Accept receipt uploads quickly, process AI work in a durable database-backed queue, and resume status polling after a page refresh.
- Refresh the employee escalation queue from the database every five seconds and show an explicit accessible progress notice while AI work is pending.
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

## Current Verification Baseline

- Build: **0 warnings, 0 errors**
- Automated tests: **102/102 passed**
- Verify Harness: **5 smoke-test cases**
- Evaluator reference pack: **15 test cases**
- Extended evaluator: **15 or 30 cases through the production upload endpoint, without an extra UI button**

On 29 September 2026, the extended runner evaluated both providers on the same 15-case judge manifest with fallback disabled. OpenRouter passed **15/15 decisions**, recorded **70/75 field exact matches**, and measured end-to-end P50/P95 of **3.614/16.459 seconds**. Ollama passed **14/15 decisions**, recorded **73/75 fields**, and measured **52.238/58.318 seconds** on an RTX 3050 Laptop 4 GB; it incorrectly auto-approved TK-12, whose final payable row was blurred. A five-request OpenRouter concurrency smoke completed 5/5 with P95 **17.072 seconds**.

These are controlled synthetic-regression results, not a production accuracy claim. OpenRouter remains the demo primary because it passes the safety gate and the latency/capacity target. Ollama remains an optional offline/manual fallback and should not be presented as automatic concurrent capacity until the TK-12 missed escalation is resolved on an independent holdout.

On 1 October 2026, repeated anonymized paper receipts exposed date-field variance: valid printed purchase dates sometimes appeared only in `transactionDate`. The shared semantic contract now losslessly moves that sole existing paper date to canonical `invoiceDate` without an extra provider call, never promotes `completionDate`, and keeps targeted repair plus `ESCALATE_FACT` for genuine unresolved contradictions.

A Vinamilk receipt then exposed a separate contract gap: the model read `183.114 - 2.828 = 180.286` correctly, but the old schema had no structured discount field. AURA now stores the printed reduction in `discountAmount`, validates the final-payable equation, compares the employee claim with the post-discount total, and no longer accepts a warning keyword as a substitute for arithmetic evidence. A Highlands postfix run also showed a loyalty value misclassified as a discount; the backend now clears it only when line items, subtotal, tax and final total independently prove that it has no monetary effect. Offline build and all 102 tests pass; the post-fix live evidence gate is documented separately.

On 2 October 2026, a real Phê La receipt exposed a safety gap: merchant, date, identifier and total were readable, but the item area was obscured and the old build still auto-approved with `lineItems=[]`. AURA now treats missing paper-receipt items as critical evidence loss, attempts one targeted re-read, then adds an explicit missing field/warning, caps confidence at 0.69 and escalates to a human. The original run remains regression evidence, not a successful approval sample.

The live post-fix gate subsequently passed: Phê La returned `ESCALATE_FACT`, the official Verify batch passed 5/5, a fully readable Vinamilk receipt remained `AUTO_APPROVE`, status/images/audit survived restart, and a five-upload concurrent smoke completed 5/5 with 530 ms acceptance P95 and 39.380 s end-to-end P95. These results close the targeted regression; they do not replace a diverse, consented real-receipt holdout.

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

Before starting the demo, run the read-only readiness gate. The first command may start the named LocalDB instance but does not call either AI provider:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Test-DemoReadiness.ps1" -StartLocalDb -SkipHttp -DiagnoseLocalDb -OutputPath "D:\aura\demo_evidence\00_preflight\preflight-before-app.txt"
dotnet run
```

In a second PowerShell window, verify the running application, database migration state, storage, policy, provider configuration, and fallback baseline:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Test-DemoReadiness.ps1" -DiagnoseLocalDb -OutputPath "D:\aura\demo_evidence\00_preflight\preflight-running-app.txt"
```

The full gate must return `READY: 0 failures`, `AuraDb`, health `status=ok`, `databaseAvailable=true`, no pending migration, and fallback disabled. A `sqllocaldb info` registry warning is diagnostic only when the running-app health check proves the database is available and current; do not delete registry keys or MDF files to silence it. The current offline suite contains **102 tests**. `-OutputPath` creates the evidence directory and transcript, so no `$evidenceRoot` variable or `Tee-Object` pipeline is required.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Test-DemoReadiness.ps1" -StartLocalDb -SkipHttp -DiagnoseLocalDb -OutputPath "D:\aura\demo_evidence\00_preflight\localdb-diagnostics.txt"
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
- [Semantic Date Repair — 01/10/2026](AURA/docs/SEMANTIC_DATE_REPAIR_2026-10-01.md)
- [Receipt Discount Reconciliation — 01/10/2026](AURA/docs/RECEIPT_DISCOUNT_RECONCILIATION_2026-10-01.md)
- [Paper Receipt Line-item Safety Guard — 02/10/2026](AURA/docs/PAPER_RECEIPT_LINE_ITEM_GUARD_2026-10-02.md)
- [OpenRouter Benchmark — 27/09/2026](AURA/docs/OPENROUTER_BENCHMARK_2026-09-27.md)
- [Dataset and Extended Evaluation Guide](AURA/docs/DATASET_EVALUATION_GUIDE.md)
- [Deployment and Operations Runbook](AURA/docs/RUNBOOK.md)
- [Optional Local Ollama Setup](AURA/docs/LOCAL_OLLAMA.md)
- [Sprint 2 Implementation Progress](AURA/docs/SPRINT2_IMPLEMENTATION_PROGRESS_2026-09-28.md)
- [Live Provider and UI Validation — 29/09/2026](AURA/docs/LIVE_VALIDATION_2026-09-29.md)
- [Demo Readiness and Remaining Gates — 29/09/2026](AURA/docs/DEMO_READINESS_2026-09-29.md)
- [Logic, Evidence, LocalDB and Self-hosted VLM Plan — 30/09/2026](AURA/docs/LOGIC_EVIDENCE_AND_CLOUD_PLAN_2026-09-30.md)
- [Final Demo Test Plan and Freeze Schedule — 01/10/2026](AURA/docs/FINAL_DEMO_TEST_PLAN_2026-10-01.md)

## Sprint 1 Limitations

- Supports one JPG/PNG image up to 5 MB; PDF and multi-page receipts are not supported yet.
- Does not yet include role-based authentication, e-invoice verification, tax-code lookup, currency conversion, or malware scanning.
- OpenRouter remains the default. Ollama fallback is opt-in and practical only on a machine where Ollama and the model are already installed; the current 4B local result still contains one missed escalation. Both providers need a consented, anonymized real-receipt holdout before any production accuracy claim.
- Uploaded receipt evidence is stored by the application instance; production deployment requires managed durable storage and an explicit retention policy.
- AI failure or low confidence always results in human review rather than a fabricated decision.

## Author

**Pham Gia Phu**

VNG Track – Challenge A: The Escalation Referee
