# AURA — AI-Assisted Reimbursement Review

AURA helps review employee expense receipts. It extracts candidate facts from an image, validates them with a schema and semantic checks, then applies deterministic C# policy. Cases that are uncertain or need authority go to a human reviewer. The model does not approve or pay expenses.

## Processing flow

`JPG/PNG upload → private receipt storage → durable SQL queue → vision extraction → schema/Semantic validation → policy routing → human review when required → audit trail`

The current source supports manual image intake, a durable background queue, privacy-risk triage, reviewer actions, and dashboard/CSV reporting. The optional Google Drive connector is a **Development-only synthetic sandbox**, disabled by default; it is not a production mail or Drive integration.

SQL admission control caps combined `PENDING` and `PROCESSING` receipts at `ReceiptProcessing__MaxOutstandingReceipts` (default `1000`) across app instances that use the same SQL database. When full, upload returns HTTP 503 with `Retry-After`; Drive sandbox items remain available for the next poll. This is a queue backlog cap—not a lifetime storage quota, spend limit, or identity/abuse-control boundary. The existing upload rate limiter is process-local.

## Run locally (Windows / PowerShell)

Prerequisites: Git, .NET 8 SDK, and SQL Server LocalDB. From the directory where you want the checkout:

```powershell
git clone https://github.com/BondPhuPhamzZ/AURA.git
cd .\AURA
dotnet tool restore
dotnet restore
```

Store the OpenRouter key and local database connection string in .NET User Secrets (never commit either value):

```powershell
dotnet user-secrets set "OpenRouter:ApiKey" "<YOUR_OPENROUTER_API_KEY>"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\MSSQLLocalDB;Database=AuraDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Connect Timeout=5;ConnectRetryCount=0"
```

Create or update the local schema, then build, test, and run:

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

## Privacy and readiness

The default storage provider is local filesystem storage; private GCS support is opt-in and has not been validated against a real bucket in this repository's current evidence. OpenRouter requests ask for provider data collection to be denied, but **Strict Zero Data Retention is not enabled or verified**. Privacy triage runs after extraction, so do not use sensitive real receipts for an unapproved provider route.

This is a supervised MVP / contest project, **not production-ready software**. Authentication/RBAC, malware scanning and quarantine for manual uploads, approved retention and restore, real private-cloud storage validation, provider-specific privacy verification, and multi-host load/chaos testing remain open gates. See the [current status and checklist](AURA/docs/CURRENT_PROJECT_STATUS.md) and [readiness/scale assessment](AURA/docs/READINESS_AND_SCALE_ASSESSMENT_2026-10-23.md) before any pilot.

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

The proposal, slides, and build log are preserved submission snapshots; consult the current status report for later source changes. Do not commit API keys, connection strings, User Secrets, receipt images, database files, raw provider responses, or private evidence.
