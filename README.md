# AURA — AI-Assisted Reimbursement Review

**27 October local checkpoint:** The owner's new synthetic Drive check showed an AUTO result for R3-01 and human handling for R3-06/R3-13; three unapproved real web uploads took the fail-safe privacy route before model inference. The screenshots do not measure upload-to-result latency or prove real-data privacy. [Evidence and publication boundary](AURA/docs/USER_DRIVE_PRIVACY_CHECK_2026-10-27.md). GitHub source remains separate from the SmartASP website; this checkpoint does not enable Drive on public hosting.

AURA helps review employee expense receipts. It extracts candidate facts from an image, validates them with a schema and semantic checks, then applies deterministic C# policy. Cases that are uncertain or need authority go to a human reviewer. The model does not approve or pay expenses.

The default branch is `master`. The previous default source is preserved at tag `archive/master-before-hardening-20261026`; a Git tag does not back up databases or receipt files. Git synchronization does not deploy the public website.

## Processing flow

`JPG/PNG upload → private receipt storage → durable SQL queue → optional egress approval → vision extraction → schema/Semantic validation → policy routing → human review when required → audit trail`

The current source supports manual image intake, a durable background queue, privacy-risk triage, reviewer actions, and dashboard/CSV reporting. For a controlled local synthetic demonstration, the owner can also grant Viewer access to a **Restricted** Google Drive folder. To request this route, email the project owner at the contact address provided for the pilot; there is no public upload folder or email-ingestion connector. The optional Drive reader is **Development/loopback-only** and disabled by default on public hosting.

SQL admission control caps combined `PENDING` and `PROCESSING` receipts at `ReceiptProcessing__MaxOutstandingReceipts` (default `1000`) across app instances that use the same SQL database. When full, upload returns HTTP 503 with `Retry-After`; Drive sandbox items remain available for the next poll. This is a queue backlog cap—not a lifetime storage quota, spend limit, or identity/abuse-control boundary. The existing upload rate limiter is process-local.

## Run locally (Windows / PowerShell)

Prerequisites: Git, .NET 8 SDK, and SQL Server LocalDB. From the directory where you want the checkout:

```powershell
git clone https://github.com/BondPhuPhamzZ/AURA.git
cd .\AURA\AURA
dotnet tool restore
dotnet restore
```

Store the OpenRouter key in .NET User Secrets (never commit it). The synthetic launcher prepares its own isolated database connection:

```powershell
dotnet user-secrets set "OpenRouter:ApiKey" "<YOUR_OPENROUTER_API_KEY>"
```

To keep an existing database unchanged, prepare the isolated synthetic profile first:

```powershell
.\tools\Start-AuraRetentionSandbox.ps1 -PrepareOnly -EnableSyntheticDrive
.\tools\Start-AuraRetentionSandbox.ps1
```

It prepares a separate LocalDB and receipt directory, enables retention and exact-image approval, and serves `http://127.0.0.1:5000`. Visual Studio **AURA** (F5) and the launcher use the same Git-ignored active profile; run only one host at a time. The launcher reuses the prepared DB on later days. Strict ZDR and provider-data denial are requested for Qwen3.8-27B; this does not redact images or establish a legal privacy guarantee. Only 49 verified synthetic fixture contents may reach the provider. The Restricted Drive test folder remains read-only and Development/loopback only. See the [single-profile evidence and two-case manual test](AURA/docs/LOCAL_SYNTHETIC_PROFILE_2026-10-26.md) and [runbook](AURA/docs/RUNBOOK.md).

For a new empty synthetic database, the preparation command above applies all EF migrations. To use a different database, pass a name such as `-DatabaseName AURA_RetentionSandbox_20261027` to that command. Build and test independently with:

```powershell
dotnet build AURA.csproj -c Release
dotnet test .\tests\AURA.Tests\AURA.Tests.csproj -c Release
```

Open the local URL printed by the launcher or press F5 with Visual Studio profile **AURA** after preparation. Database migrations are **not** applied automatically at startup. For the opt-in SQL/LocalDB integration tests, start the existing LocalDB instance if needed and run:

```powershell
$env:AURA_RUN_LOCALDB_INTEGRATION = "1"
dotnet test .\tests\AURA.Tests\AURA.Tests.csproj -c Release
Remove-Item Env:\AURA_RUN_LOCALDB_INTEGRATION
```

These integration tests create uniquely named temporary databases and remove them when complete. Use a LocalDB instance owned by the current Windows account.

The optional backup-script integration cases need both Windows PowerShell 5.1 and PowerShell 7. Enable `AURA_RUN_POWERSHELL_INTEGRATION=1` to execute them; the current Release suite passes 378/378 with both integration flags enabled. Frontend race/error tests pass 8/8 with `node .\tests\frontend\workflow-refresh.test.cjs` (Node is needed for these tests only).

## Two intake routes and live status

- **Web:** select up to 10 JPG/PNG images per submission. Each accepted image is saved as a separate SQL request. The browser follows its returned request IDs while the AI queue processes them.
- **Restricted Drive (local synthetic demonstration):** place approved JPG/PNG files directly in the shared test folder. A read-only service account polls the folder about every 120 seconds by default, then saves each accepted file as a SQL request before model processing. Ask the owner by email for access; keep the folder Restricted. This is not enabled on SmartASP.

While the employee tab is visible, a read-only `fetch` request refreshes its result table every five seconds without reloading the page. Recent Drive requests show SQL `PENDING` and `PROCESSING`, then the result; Audit records the permanent events. The table includes up to 20 recent unforwarded Drive requests created in the last 24 hours **plus all unforwarded escalations**, including older ones. It is a live work view, not the complete SQL history. Polling pauses on hidden tabs. A full page refresh does not start the worker or force a Drive scan.

See the [Drive progress UI verification](AURA/docs/DRIVE_PROGRESS_UI_VERIFICATION_2026-10-27.md) for the executed tests and the remaining timing/hosting limits.

Drive idempotency uses folder ID, Google file ID and MD5: re-reading the same unchanged file silently skips it, without another model call or new audit. A newly uploaded *copy* has a new Google file ID and may enter SQL again; SHA-256 duplicate detection marks it, but the current synthetic repeatability profile has duplicate escalation disabled. Changing bytes creates another source version, subject to the exact-image privacy approval gate. Invalid files are rejected before SQL and recorded in the private rejection log. Do not interpret a skipped file or an `AUTO_APPROVE` duplicate as duplicate-expense protection.

## Current Drive and UX boundary

On 26 October, a fresh local Drive run completed 3/3 approved JPEGs; the later R3-01/R3-12 pair completed and a restart left **5 requests, 10 audit rows and no queued jobs**. This is execution/idempotency evidence, not an accuracy benchmark. The connector reads direct folder children, not nested folders; its bounded scan is not an unlimited batch drain. The repeatability profile has duplicate escalation off: same-source idempotency is not duplicate-expense prevention. Submitter/department are synthetic metadata.

Tabs, Forward, reviewer decisions/Undo and dashboard periods now refresh read-only fragments after server-confirmed writes. The UI preserves filters, focus, scroll and URL history, pauses inactive/hidden polling, and retains prior rows with retry recovery on read failure. Two-tab and lost-response checks retained one SQL decision; no automatic POST retry was added. Reviewer assignment, reminders, verified identity, sustained load and independent usability acceptance remain open. See the [readiness assessment](AURA/docs/READINESS_AND_SCALE_ASSESSMENT_2026-10-23.md).

## Privacy and readiness

The default receipt store is local filesystem storage. Private GCS support remains opt-in and has not been validated against a real bucket in this evidence.

The synthetic launcher enables `ExternalImagePrivacy:Mode=ApprovedImagesOnly`: adapters check the exact image SHA-256 before external HTTP, and unapproved images become human-review cases. An invalid approval file blocks startup. `BlockExternal` blocks remote inference while allowing loopback Ollama; `LegacyConfigured` preserves the contest configuration. These controls do not detect or redact personal data. Approved pixels still reach the configured provider before post-inference privacy triage. Strict-ZDR routing and data-collection denial are request settings, not contractual no-retention guarantees.

Seven-day local retention is opt-in for finalized decisions. Pending and unresolved human-review work is retained. SQL/image backup, restore and scheduled managed-backup expiry were exercised with synthetic data; real-data activation remains a separate gate. Cleanup waits until the app/task runs when the machine is off or the interactive user is logged out.

The latest full 15-case synthetic regression through the approval gate on `649885e` matched 15/15 decisions and 143/146 expected facts; a separate four-image privacy-role kit matched 12/12 decisions and 21/21 labeled categories across three runs. A modified synthetic image was blocked before inference. In the full approved regression, Semantic attempted one repair and safely retained an unresolved FACT case; E2E P50/P95 was 5,139/32,739 ms. No latency improvement is claimed. The user confirmed synthetic-only scope, preserving the old database. These are small, purpose-built synthetic checks, not general accuracy or detector-coverage estimates. See [detailed model and privacy evidence](AURA/docs/MODEL_ROUTE_COMPARISON_2026-10-24.md) and [current status](AURA/docs/CURRENT_PROJECT_STATUS.md).

This is a supervised MVP / contest project, **not production-ready software**. Authentication/RBAC, malware scanning and quarantine for manual uploads, real-data retention/restore approval, real private-cloud storage validation, provider-specific privacy verification, and multi-host load/chaos testing remain open gates. See the [current status and checklist](AURA/docs/CURRENT_PROJECT_STATUS.md) and [readiness/scale assessment](AURA/docs/READINESS_AND_SCALE_ASSESSMENT_2026-10-23.md) before any pilot.

To change the queue ceiling for a controlled deployment, set `ReceiptProcessing__MaxOutstandingReceipts` (valid range `1`–`10000`). Do not raise it until storage capacity, provider budget, and queue-age behavior have been load-tested.

Build/publish boundaries exclude runtime data and private test results. A real Docker ignore-matcher fixture and a local publish probe passed after fixing a raw-results inclusion defect; this does not establish container runtime readiness. The [deployment guide](AURA/docs/DEPLOYMENT.md) separates controlled staging from the current Development-only Drive connector. A Production-mode localhost staging check passed, and one UI/model smoke returned AUTO with facts in 4,530 ms processing; this is not a new accuracy benchmark. Hosting operations are deferred by the owner: the prepared UX package does not enable the Development-only Drive connector on the public site.

## Project documents

- [Local setup for reviewers](AURA/docs/JUDGE_LOCAL_SETUP.md)
- [Operational runbook](AURA/docs/RUNBOOK.md)
- [Business rules](AURA/BUSINESS_RULES.md)
- [Shared private receipt storage design](AURA/docs/SHARED_RECEIPT_STORAGE.md)
- [English proposal — current note plus contest baseline](AURA/submission/AURA_PROPOSAL_EN.pdf)
- [Vietnamese proposal — current note plus contest baseline](AURA/submission/AURA_PROPOSAL_VI.pdf)
- [Five slides — current status labeled separately](AURA/submission/AURA_5_SLIDES_AFTER_FEEDBACK_FINAL.pptx)
- [Build log — current note plus contest baseline](AURA/submission/AURA_BUILD_LOG.pdf)
- [Post-contest status addendum](AURA/submission/AURA_POST_CONTEST_ADDENDUM_2026-10-23.md)

The proposal, slides, and build log label the submitted contest results as historical and add a dated current-source note. Git history retains the original submission versions. Do not commit API keys, connection strings, User Secrets, real receipt images, database files, raw provider responses, or private evidence.
