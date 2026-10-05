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

- OpenRouter/Qwen3-VL-8B is the default receipt reader; Ollama/Qwen3-VL-4B is optional.
- SQL-backed processing survives refresh/restart and avoids blocking the upload request.
- The model extracts structured facts; it never has final approval authority.
- Missing, contradictory, or unreadable evidence fails safe to human review.
- Verify Harness runs the official five cases in one action: three approvals and two escalations.
- Submitter identity, manager actions, provider metadata, reasons, and undo are auditable.

## Verified baseline

Last full validation: 4 October 2026 on candidate `c42c5ef`.

- Build: **0 warnings, 0 errors**.
- Automated tests: **108/108 passed**; they do not call the paid AI API.
- Official Verify Harness: **5/5** with the expected 3 approve / 2 escalate split.
- Locked synthetic judge set: OpenRouter **15/15 decisions**, Ollama **14/15**; Ollama missed escalation TK-12.
- SmartASP postfix gate: health/DB/storage/migration, upload security, initial-tab rendering, independent AUTO/FACT smoke, Verify, human workflow and persistence passed.
- Live concurrency: **2/2 + 5/5 completed**; end-to-end P95 **20.941 / 23.290 seconds**. The accepted timer includes antiforgery GET/setup/network, so it is not a pure POST SLO.
- Real-receipt development checks cover approval with repair, approval without repair, and fail-safe escalation for obscured line items.
- Isolated OpenRouter-to-Ollama fallback passed 1/1, but took **84.425 seconds**. Fallback therefore remains disabled in the official demo baseline.

These are regression/staging results, not a claim of production accuracy. Final gates remain a locked 15-case blind holdout, three real-user sessions, manual mobile/4G/no-flash evidence and three timed rehearsals. Recheck health and smoke immediately before publishing or demonstrating the live URL.

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
dotnet user-secrets set "OpenRouter:Model" "qwen/qwen3-vl-8b-instruct"
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

Expected: build 0 warnings/errors, 108/108 tests passed, and no model change pending migration.

Stop the app with `Ctrl+C`. No database reset or LocalDB deletion is required.

## Important documents

- [Judge local setup and troubleshooting](AURA/docs/JUDGE_LOCAL_SETUP.md)
- [Architecture and integration](AURA/ARCHITECTURE_AND_INTEGRATION_REPORT.md)
- [Business rules](AURA/BUSINESS_RULES.md)
- [Workflow specification](AURA/submission/AURA_WORKFLOW_SPEC.md)
- [Dataset and extended evaluation](AURA/docs/DATASET_EVALUATION_GUIDE.md)
- [Current verified status](AURA/docs/CURRENT_PROJECT_STATUS_2026-10-05.md)
- [Blind holdout locking guide](AURA/docs/HOLDOUT_LOCKING_GUIDE_2026-10-05.md)
- [Optional local Ollama setup](AURA/docs/LOCAL_OLLAMA.md)
- [Deployment decision](AURA/docs/DEPLOYMENT_DECISION_2026-10-02.md)
- [Current roadmap and acceptance gates](AURA/docs/NEXT_IMPLEMENTATION_ROADMAP_2026-10-03.md)

## Current limitations

- Supports one JPG/PNG receipt up to 5 MB; PDF and multi-page documents are not supported.
- OpenRouter requires Internet and available provider credit.
- Ollama fallback is opt-in and is not the official concurrent-capacity path.
- Production deployment still requires authentication/RBAC, managed durable receipt storage, retention/privacy controls, backup/restore, monitoring, and malware scanning.

## Author

**Pham Gia Phu** - The Deciders, VNG Track, MLAI Hackathon 2026.
