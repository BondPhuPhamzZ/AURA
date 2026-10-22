# AURA — Controlled AI Reimbursement Review

> **Current source status (22 October 2026):** 218/218 offline tests pass; the Release build has 0 warnings and 0 errors; EF Core reports no pending model changes. A restricted-folder Google Drive connector passed a small local end-to-end test using synthetic receipts before the latest hardening. An isolated local database now passes health and dashboard checks; Drive end-to-end testing after hardening is still pending. The connector is disabled by default. This source has **not** been republished to the live demo and is **not approved for real receipts or production use**.

AURA is a supervised first-line assistant for employee reimbursement review. A vision model extracts structured facts from receipt images; semantic validation checks the evidence; deterministic C# policy—not the model—selects the route. Uncertain or exceptional cases stop for human review and are recorded in an audit timeline.

## How it works

```text
JPG/PNG upload (up to 10 images per selection; 5 MB each)
  -> validate and persist each case -> return HTTP 202
  -> SQL-backed worker claims a job with a lease
  -> vision model extracts facts as JSON
  -> schema + semantic validation; at most one repair attempt
  -> deterministic C# policy chooses AUTO_APPROVE or ESCALATE_*
  -> employee/manager handles exceptions -> audit history
```

Each image is an independent case and status URL; there is no server-side batch transaction. AI concurrency is limited to one request per process. SQL is the durable queue source of truth; leases allow interrupted work to be reclaimed. A provider failure may end in `FAILED` and require operator handling—automatic retry is not guaranteed for every failure type.

The default vision route is Qwen3.8 Flash through OpenRouter. Requests ask the provider not to collect data (`data_collection=deny`); **Strict Zero Data Retention is not confirmed or claimed**. The model extracts facts only. C# policy applies business rules, and humans decide escalations.

## Current scope and safety boundary

- Manual intake accepts JPG/PNG up to 5 MB; the UI allows up to 10 images in one selection and submits them sequentially. PDF and multi-page documents are not supported.
- The dashboard provides processing summaries and CSV export. Sensitive raw AI facts are not included in the management CSV.
- A Drive connector exists only as a **Development + loopback synthetic sandbox**. It is read-only, off by default, and not a production intake channel. Email is not integrated.
- The local Drive end-to-end test processed three synthetic jobs, rejected a `.txt` file, and confirmed restart idempotency. This small run is not a load test or an accuracy claim.
- Production gates remain open: authentication/RBAC, malware scanning and quarantine, managed private object storage, approved retention and deletion, backup/restore, provider privacy verification, distributed coordination, monitoring, and capacity testing.

The public live URL may run an earlier published build. It is not evidence that the current branch or Drive connector is deployed.

## Public demo and submission artifacts

These repository files are public and were checked to exist in the public `master` branch. The proposal and build log are **dated submission snapshots**; use the current-status and architecture documents below for the latest source state.

| Resource | Link |
|---|---|
| Live demo (earlier published build) | [Open AURA](https://bondphupham-001-site1.ltempurl.com/) |
| Live health endpoint | [Open `/healthz`](https://bondphupham-001-site1.ltempurl.com/healthz) |
| Public repository | [BondPhuPhamzZ/AURA](https://github.com/BondPhuPhamzZ/AURA) |
| Final five-slide deck | [Download PPTX](https://raw.githubusercontent.com/BondPhuPhamzZ/AURA/master/AURA/submission/AURA_5_SLIDES_AFTER_FEEDBACK_FINAL.pptx) |
| English proposal (submission snapshot) | [Download PDF](https://raw.githubusercontent.com/BondPhuPhamzZ/AURA/master/AURA/submission/AURA_PROPOSAL_EN.pdf) |
| Vietnamese proposal (submission snapshot) | [Download PDF](https://raw.githubusercontent.com/BondPhuPhamzZ/AURA/master/AURA/submission/AURA_PROPOSAL_VI.pdf) |
| Project build log (submission snapshot) | [Download PDF](https://raw.githubusercontent.com/BondPhuPhamzZ/AURA/master/AURA/submission/AURA_BUILD_LOG.pdf) |

No evaluation-key folder or private Drive folder is linked from this public README. Credentials and any reviewer-only materials must be shared through an authorized private channel.

## Clone and run locally (Windows)

### Requirements

- Windows 10/11, .NET 8 SDK, PowerShell, and SQL Server Express LocalDB.
- An OpenRouter API key is needed only to process receipts through the default vision provider. Never commit it or paste it into shared logs.

### 1. Clone and restore

```powershell
git clone https://github.com/BondPhuPhamzZ/AURA.git
cd AURA\AURA
dotnet tool restore
dotnet restore
```

### 2. Configure local secrets and database

The checked-in default connection string targets a local database named `AuraDb`. Start LocalDB and apply the repository migrations before running the app:

```powershell
sqllocaldb start MSSQLLocalDB
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\MSSQLLocalDB;Database=AuraDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Connect Timeout=5;ConnectRetryCount=0"
dotnet ef database update
```

To use vision extraction, store your own OpenRouter key in .NET User Secrets (replace the placeholder locally; never commit the real value):

```powershell
dotnet user-secrets set "OpenRouter:ApiKey" "REPLACE_WITH_YOUR_OWN_KEY"
```

Do not run `dotnet user-secrets list` while screen-sharing or recording; it can print secret values. The Drive connector remains disabled unless explicitly configured for a local synthetic-only test. Do not enable it on a public host or point it at personal receipts.

### 3. Run AURA

```powershell
dotnet run
```

Open the local URL printed by ASP.NET Core (the default launch profile uses `http://localhost:5000`).

### 4. Run offline checks

From the `AURA\AURA` project directory:

```powershell
dotnet build AURA.csproj -c Release --no-restore
dotnet test tests\AURA.Tests\AURA.Tests.csproj -c Release --no-restore
dotnet ef migrations has-pending-model-changes --no-build
```

The current branch baseline is 218 passing tests, a clean Release build, and no pending model changes. Offline tests do not call the paid vision API. A successful test run does not replace live-provider, security, privacy, or load testing.

Stop the app with `Ctrl+C`. Do not reset or delete the database as a routine shutdown step.

## Current technical documentation

- [Current project status](AURA/docs/CURRENT_PROJECT_STATUS.md)
- [Drive sandbox checklist and GO/NO-GO gates](AURA/docs/DRIVE_SANDBOX_CHECKLIST.md)
- [Drive sandbox local setup and rollback](AURA/docs/DRIVE_SANDBOX_LOCAL.md)
- [Drive end-to-end evidence and limits](AURA/docs/DRIVE_SANDBOX_EVIDENCE_TEMPLATE.md)
- [Latest Drive hardening evidence and open gates](AURA/docs/DRIVE_PIPELINE_HARDENING_2026-10-22.md)
- [Architecture and integration report](AURA/ARCHITECTURE_AND_INTEGRATION_REPORT.md)
- [Business rules (runtime source)](AURA/BUSINESS_RULES.md)
- [Operations runbook](AURA/docs/RUNBOOK.md)
- [End-to-end workflow specification](AURA/submission/AURA_WORKFLOW_SPEC.md)
- [Submission index](AURA/submission/README.md)
- [Evaluation evidence summary](AURA/docs/EVALUATION_EVIDENCE.md)
- [User-study protocol](AURA/docs/USER_STUDY_PROTOCOL.md)

## Maintainer

Pham Gia Phu — The Deciders, VNG Track, MLAI Hackathon 2026.
