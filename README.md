# AURA - Automated Underwriting & Reimbursement AI

AURA is an ASP.NET Core application for first-line employee reimbursement review. A vision model reads receipt facts, but a deterministic C# policy engine makes the final decision: automatically approve clear routine cases or escalate uncertain, out-of-policy, and over-authority cases to a human reviewer.

## Submission

| Item | Link |
|---|---|
| Sprint 1 video and evaluator API key | [Shared submission folder](https://drive.google.com/drive/folders/1_EHs9-KghK2JWpQGRAu_jLWl9WmBkMLc?usp=sharing) |
| Presentation | [AURA 5 Slides](https://raw.githubusercontent.com/BondPhuPhamzZ/AURA/master/AURA/submission/AURA_5_SLIDES.pptx) |
| Build log | [AURA Build Log](https://raw.githubusercontent.com/BondPhuPhamzZ/AURA/master/AURA/submission/AURA_BUILD_LOG.docx) |

The key in the shared folder is temporary and evaluator-only. Do not commit it, paste it into source/config files, or show it in screenshots and recordings. Store it locally with .NET User Secrets and revoke/rotate it after evaluation.

## Core workflow

```text
Upload JPG/PNG receipt
-> save request and return HTTP 202
-> database-backed worker calls Qwen Vision
-> semantic validation and at most one repair
-> deterministic C# policy decision
-> AUTO_APPROVE or ESCALATE_FACT/POLICY/AUTHORITY/SYSTEM_ERROR
-> employee forwards exception
-> manager approves, rejects, or undoes
-> audit trail records the lifecycle
```

Key capabilities:

- OpenRouter/Qwen3.8 Flash on Alibaba, with reasoning disabled for deterministic extraction, is the default receipt reader; Ollama/Qwen3-VL-4B is an optional local contingency.
- SQL-backed processing survives refresh/restart and avoids blocking the upload request.
- The model extracts structured facts; it never has final approval authority.
- Missing, contradictory, or unreadable evidence fails safe to human review.
- Verify Harness runs the official five cases in one action: three approvals and two escalations.
- Submitter identity, manager actions, provider metadata, reasons, and undo are auditable.

## Verified baseline

Last evidence update: 13 October 2026. Live health and the Qwen3.8 Flash Verify gate remain the frozen demo baseline; a separate prospective 10-receipt mini-holdout was completed once without reruns.

- Build: **0 warnings, 0 errors**.
- Automated tests after Alibaba/Qwen3.8 Flash hardening: **161/161 passed**; they do not call the paid AI API.
- Official Verify Harness: **5/5** with the expected 3 approve / 2 escalate split.
- Historical locked synthetic judge set: OpenRouter **15/15 decisions**, Ollama **14/15**. The post-contract OpenRouter v2 run reached **14/15** and still missed TK-12; the physical-ink-loss v3.1 safety set reached **13/15** with zero missed escalations and zero system errors.
- The 12 October Qwen3.8 Flash candidate completed the same 15 locked post-holdout real receipts at **15/15**, with zero missed/over/system errors and 30/30 critical fields. Five concurrent uploads completed with 41.079-second P95 at AI concurrency 1. This is post-adjustment regression evidence, not a new blind holdout or production SLA.
- A new owner-supplied, redacted and pre-locked 10-receipt mini-holdout completed **8/10 exact**, with **0 missed escalations, 2 fail-safe over-escalations, 0 system/provider/429 errors observed, and 28/30 core date/identifier/total fields**. The first five accepted requests were terminal by 64.763 seconds and all ten by 108.803 seconds at AI concurrency 1. It was run once with no replay; disclosed redaction interactions prevent treating it as production accuracy.
- A separately locked two-receipt prospective addendum completed **1/2 exact**, with **0 missed escalations, 1 fail-safe over-escalation and 0 system/provider/429 errors**. Three O'Clock's core facts were correct, but a free-text warning about the privacy blackout triggered FACT review. The descriptive combined view is 9/12; it is not a new twelve-case holdout and neither package may be reused for a blind claim.
- After publish/recycle, the live Qwen3.8 Flash Verify Harness completed **5/5** in **65.004 seconds** with no system/provider error. Five simultaneous clients produced one HTTP 200 and four HTTP 409 responses, so duplicate clicks did not create duplicate AI batches on the single-process demo deployment.
- SmartASP post-holdout gate: home and health 5/5, DB/storage/migration, upload/security-negative checks, browser mobile UI and Verify OpenRouter 5/5 passed. Human workflow also passed 2/2 with synthetic fixtures: policy reject/undo and fact approve/undo.
- Live concurrency: **2/2 + 5/5 completed**; end-to-end P95 **20.941 / 23.290 seconds**. The accepted timer includes antiforgery GET/setup/network, so it is not a pure POST SLO.
- The one-time locked real holdout produced **10/15 raw decision matches** (one missed escalation, three over-escalations, zero system errors). A post-hoc review found one locked label error, giving an adjudicated analysis of 11/15 without changing raw evidence. The 28/30 field score measured only currency and total, not full extraction accuracy.
- A later post-holdout regression on the same 15 receipts reached **11/15**, with zero missed escalations, four over-escalations and zero system errors. It is regression evidence, not a new blind holdout.
- Holdout findings led to evidence contract v2: printed-final-total provenance, raw-date verification, second-level money/identifier guards, time normalization and expanded personal-item taxonomy. Contract v2 is deployed on `1f4b3c9`.
- The Audit timezone defect found on the earlier deployment is fixed and live-verified: new and historical events render in Vietnam time while UTC remains the database source of truth.
- Isolated OpenRouter-to-Ollama fallback passed 1/1, but took **84.425 seconds**. Fallback therefore remains disabled in the official demo baseline.

These results support a supervised demo, not production accuracy. Both real-receipt sets are now known and cannot be reused for a new holdout claim. Accuracy claims and unattended rollout remain NO-GO. Real-phone 4G/5G passed; the remaining evidence gates are three supervised user sessions and timed rehearsals on the frozen commit.

## Run locally

### Requirements

- Windows 10/11.
- .NET 8 SDK.
- SQL Server Express LocalDB (`MSSQLLocalDB`).
- PowerShell.
- An evaluator OpenRouter key from the shared submission folder.

Ollama is not required for the default setup.

### 1. Clone and restore tools

```powershell
git clone https://github.com/BondPhuPhamzZ/AURA.git
cd AURA\AURA

dotnet tool restore
dotnet restore
```

### 2. Configure local secrets

Replace `OPENROUTER_EVALUATION_KEY` with the evaluator key. User Secrets stores it outside Git.

```powershell
dotnet user-secrets set "OpenRouter:ApiKey" "OPENROUTER_EVALUATION_KEY"
dotnet user-secrets set "OpenRouter:Model" "qwen/qwen3.8-flash"
dotnet user-secrets set "OpenRouter:ReasoningEffort" "none"
dotnet user-secrets set "Vision:Provider" "OpenRouter"
dotnet user-secrets set "Vision:FallbackEnabled" "false"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\mssqllocaldb;Database=AuraDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Connect Timeout=5;ConnectRetryCount=0"
```

Do not run `dotnet user-secrets list` while sharing or recording the screen because it may print the key.

### 3. Start LocalDB and update the database

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Test-DemoReadiness.ps1" -StartLocalDb -SkipHttp -DiagnoseLocalDb
dotnet ef database update
```

The pre-app check must report `READY: 0 failures`. A warning caused by `-SkipHttp` is expected. A LocalDB registry-inspection warning is diagnostic only; do not delete registry keys, the LocalDB instance, or MDF files to silence it.

### 4. Run AURA

```powershell
dotnet run
```

Open `http://localhost:5000` or the listening URL printed by .NET.

In a second PowerShell window, return to the same folder containing `AURA.csproj`, then verify the running app:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Test-DemoReadiness.ps1" -DiagnoseLocalDb
```

Expected: `READY: 0 failures`, health `status=ok`, database and storage available, no pending migration, provider `OpenRouter`, and `fallbackEnabled=false`.

### 5. Use the application

1. Select **Chạy Verify Harness (90s)** to run the official five cases.
2. Or upload one JPG/PNG receipt, enter the claimed amount, and select **AI tự động kiểm**.
3. For an `ESCALATE_*` result, select **Chuyển tiếp**.
4. In **Quản lý**, approve or reject the request.
5. Open **Lịch sử hành vi** to inspect the audit trail or undo the latest manager action.

### 6. Run offline tests

```powershell
dotnet build AURA.csproj --no-restore
dotnet test tests\AURA.Tests\AURA.Tests.csproj --no-restore
dotnet ef migrations has-pending-model-changes --no-build
```

Expected for the current candidate: build 0 warnings/errors, 161/161 tests passed, and no model change pending migration.

Stop the app with `Ctrl+C`. No database reset or LocalDB deletion is required.

## Important documents

- [Judge local setup and troubleshooting](AURA/docs/JUDGE_LOCAL_SETUP.md)
- [Architecture and integration](AURA/ARCHITECTURE_AND_INTEGRATION_REPORT.md)
- [Business rules](AURA/BUSINESS_RULES.md)
- [Workflow specification](AURA/submission/AURA_WORKFLOW_SPEC.md)
- [Dataset and extended evaluation](AURA/docs/DATASET_EVALUATION_GUIDE.md)
- [Current verified status](AURA/docs/CURRENT_PROJECT_STATUS_2026-10-13.md)
- [Official 10-receipt mini-holdout](AURA/docs/OFFICIAL_HOLDOUT10_2026-10-13.md)
- [Evidence localization and reviewer overlay plan](AURA/docs/EVIDENCE_LOCALIZATION_PLAN_2026-10-13.md)
- [Qwen3.8 Flash live Verify gate](AURA/docs/QWEN38_FLASH_LIVE_VERIFY_2026-10-12.md)
- [Current progress report](AURA/docs/PROGRESS_REPORT_2026-10-13.md)
- [Live post-holdout validation](AURA/docs/LIVE_POST_HOLDOUT_VALIDATION_2026-10-06.md)
- [Official holdout postmortem](AURA/docs/OFFICIAL_HOLDOUT_POSTMORTEM_2026-10-06.md)
- [Blind holdout locking guide](AURA/docs/HOLDOUT_LOCKING_GUIDE_2026-10-05.md)
- [Optional local Ollama setup](AURA/docs/LOCAL_OLLAMA.md)
- [Deployment decision](AURA/docs/DEPLOYMENT_DECISION_2026-10-02.md)
- [Current roadmap and acceptance gates](AURA/docs/NEXT_IMPLEMENTATION_ROADMAP_2026-10-07.md)
- [Manual phone, user-session and rehearsal gates](AURA/docs/MANUAL_FINAL_GATES_2026-10-06.md)

## Current limitations

- Supports one JPG/PNG receipt up to 5 MB; PDF and multi-page documents are not supported.
- OpenRouter requires Internet and available provider credit.
- Ollama fallback is opt-in and is not the official concurrent-capacity path.
- Production deployment still requires authentication/RBAC, managed durable receipt storage, retention/privacy controls, backup/restore, monitoring, and malware scanning.

## Author

**Pham Gia Phu** - The Deciders, VNG Track, MLAI Hackathon 2026.
