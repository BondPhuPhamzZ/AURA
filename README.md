# AURA — AI-Assisted Reimbursement Review

AURA helps review employee expense receipts. It extracts candidate facts from an image, validates them with a schema and semantic checks, then applies deterministic C# policy. Cases that are uncertain or need authority go to a human reviewer. The model does not approve or pay expenses.

## Processing flow

`JPG/PNG upload → private receipt storage → durable SQL queue → optional egress approval → vision extraction → schema/Semantic validation → policy routing → human review when required → audit trail`

The current source supports manual image intake, a durable background queue, privacy-risk triage, reviewer actions, and dashboard/CSV reporting. The optional Google Drive connector is a **Development-only synthetic sandbox**, disabled by default; it is not a production mail or Drive integration.

SQL admission control caps combined `PENDING` and `PROCESSING` receipts at `ReceiptProcessing__MaxOutstandingReceipts` (default `1000`) across app instances that use the same SQL database. When full, upload returns HTTP 503 with `Retry-After`; Drive sandbox items remain available for the next poll. This is a queue backlog cap—not a lifetime storage quota, spend limit, or identity/abuse-control boundary. The existing upload rate limiter is process-local.

## Run locally (Windows / PowerShell)

Prerequisites: Git, .NET 8 SDK, and SQL Server LocalDB. From the directory where you want the checkout:

```powershell
git clone https://github.com/BondPhuPhamzZ/AURA.git
cd .\AURA\AURA
dotnet tool restore
dotnet restore
```

Store the OpenRouter key and local database connection string in .NET User Secrets (never commit either value):

```powershell
dotnet user-secrets set "OpenRouter:ApiKey" "<YOUR_OPENROUTER_API_KEY>"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\MSSQLLocalDB;Database=AuraDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Connect Timeout=5;ConnectRetryCount=0"
```

To keep an existing database unchanged, use the isolated synthetic profile first:

```powershell
.\tools\Start-AuraRetentionSandbox.ps1
```

It prepares a separate dated LocalDB and receipt directory, enables retention and exact-image approval, and serves `http://127.0.0.1:5098`. It does not change User Secrets. Only verified synthetic fixtures are approved; the model/key still come from your current configuration. See the [runbook](AURA/docs/RUNBOOK.md) to reproduce the tested Qwen/Strict-ZDR request route.

For a new empty demo database, create the schema, then build, test, and run:

```powershell
dotnet ef database update
dotnet build AURA.csproj -c Release
dotnet test .\tests\AURA.Tests\AURA.Tests.csproj -c Release
dotnet run --project AURA.csproj
```

Open the local URL printed by `dotnet run`. Database migrations are **not** applied automatically at startup by default. For the opt-in SQL/LocalDB integration tests, start the existing LocalDB instance if needed and run:

```powershell
$env:AURA_RUN_LOCALDB_INTEGRATION = "1"
dotnet test .\tests\AURA.Tests\AURA.Tests.csproj -c Release
Remove-Item Env:\AURA_RUN_LOCALDB_INTEGRATION
```

These integration tests create uniquely named temporary databases and remove them when complete. Use a LocalDB instance owned by the current Windows account.

The optional backup-script integration cases need both Windows PowerShell 5.1 and PowerShell 7. Enable `AURA_RUN_POWERSHELL_INTEGRATION=1` to execute them; the recorded full verification enabled both integration flags.

## Privacy and readiness

The default receipt store is local filesystem storage. Private GCS support remains opt-in and has not been validated against a real bucket in this evidence.

The synthetic launcher enables `ExternalImagePrivacy:Mode=ApprovedImagesOnly`: adapters check the exact image SHA-256 before external HTTP, and unapproved images become human-review cases. An invalid approval file blocks startup. `BlockExternal` blocks remote inference while allowing loopback Ollama; `LegacyConfigured` preserves the contest configuration. These controls do not detect or redact personal data. Approved pixels still reach the configured provider before post-inference privacy triage. Strict-ZDR routing and data-collection denial are request settings, not contractual no-retention guarantees.

Seven-day local retention is opt-in for finalized decisions. Pending and unresolved human-review work is retained. SQL/image backup, restore and scheduled managed-backup expiry were exercised with synthetic data; real-data activation remains a separate gate. Cleanup waits until the app/task runs when the machine is off or the interactive user is logged out.

The latest full 15-case synthetic regression on `24b7b33` matched 15/15 decisions and 143/146 expected facts; a separate four-image privacy-role kit matched 12/12 decisions and 21/21 labeled categories across three runs. After adding the egress guard, a modified synthetic image was blocked and one approved smoke passed; that smoke is not a new accuracy benchmark. These are small, purpose-built synthetic checks, not general accuracy or detector-coverage estimates. See [detailed model and privacy evidence](AURA/docs/MODEL_ROUTE_COMPARISON_2026-10-24.md) and [current status](AURA/docs/CURRENT_PROJECT_STATUS.md).

This is a supervised MVP / contest project, **not production-ready software**. Authentication/RBAC, malware scanning and quarantine for manual uploads, real-data retention/restore approval, real private-cloud storage validation, provider-specific privacy verification, and multi-host load/chaos testing remain open gates. See the [current status and checklist](AURA/docs/CURRENT_PROJECT_STATUS.md) and [readiness/scale assessment](AURA/docs/READINESS_AND_SCALE_ASSESSMENT_2026-10-23.md) before any pilot.

To change the queue ceiling for a controlled deployment, set `ReceiptProcessing__MaxOutstandingReceipts` (valid range `1`–`10000`). Do not raise it until storage capacity, provider budget, and queue-age behavior have been load-tested.

## Project documents

- [Local setup for reviewers](AURA/docs/JUDGE_LOCAL_SETUP.md)
- [Operational runbook](AURA/docs/RUNBOOK.md)
- [Business rules](AURA/BUSINESS_RULES.md)
- [Shared private receipt storage design](AURA/docs/SHARED_RECEIPT_STORAGE.md)
- [English proposal — contest snapshot](AURA/submission/AURA_PROPOSAL_EN.pdf)
- [Vietnamese proposal — contest snapshot](AURA/submission/AURA_PROPOSAL_VI.pdf)
- [Contest slides — submitted snapshot](AURA/submission/AURA_5_SLIDES_AFTER_FEEDBACK_FINAL.pptx)
- [Build log — submitted snapshot](AURA/submission/AURA_BUILD_LOG.pdf)
- [Post-contest status addendum](AURA/submission/AURA_POST_CONTEST_ADDENDUM_2026-10-23.md)

The proposal, slides, and build log are preserved submission snapshots; consult the current status report for later source changes. Do not commit API keys, connection strings, User Secrets, real receipt images, database files, raw provider responses, or private evidence.
