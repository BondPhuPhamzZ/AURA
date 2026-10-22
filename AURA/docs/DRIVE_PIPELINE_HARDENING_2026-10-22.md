# Drive pipeline hardening evidence — 22 October 2026

Scope: local source and synthetic fixtures. This record does not certify a live deployment, a real-receipt pilot, or 100-image throughput.

## Changes

- Drive metadata and download GETs retry HTTP 429/500/502/503/504 at most twice after the initial request. Retry-After is honored up to five seconds per wait; 401/403 are not retried. A failed scan is retried by the existing poll loop. No Drive file is modified.
- New `AI_QUEUED` and `AI_PROCESSED_*` audit details omit original filenames, submitter identifiers, departments, receipt hashes, and free-text AI reasons. Existing historical audit rows are unchanged. The receipt row and extracted-facts JSON still contain data needed by the demo and need access control before real use.
- Fake-API tests cover one 429 followed by success and a 403 that stops without intake. The intake test checks that the new queue audit omits filename and submitter fields.

## Verification

| Check | Result |
|---|---|
| `dotnet build AURA.csproj -c Release --no-restore` | PASS: 0 warnings, 0 errors |
| `dotnet test tests/AURA.Tests/AURA.Tests.csproj -c Release --no-build --no-restore` | PASS: 218/218; no skipped tests |
| `dotnet ef migrations has-pending-model-changes --no-build` | PASS: no model changes |
| Focused Drive test run | PASS: 20/20 after the change |
| LocalDB runtime and `/healthz` | BLOCKED: LocalDB instance registry error; last smoke returned `degraded`, `databaseAvailable=false` |
| Live Google Drive/OpenRouter after this change | NOT RUN; the local database gate is unavailable |
| 5/20/100 synthetic end-to-end batches | NOT RUN; no capacity claim |

An earlier test invocation with restore emitted NU1900 because the NuGet vulnerability feed was unreachable. The final no-restore test above passed. The 100-image local fixture directory contains 100 files but only 30 distinct SHA-256 values; it is untracked and unsuitable for a 100-independent-receipt accuracy claim.

## Next gates

1. Restore a working isolated SQL test database without deleting an existing instance or data; verify `/healthz` and dashboard.
2. Re-run one synthetic Drive file, restart/idempotency, and negative cases against the actual Drive API; record case IDs and counts without raw receipt data.
3. Run 5, 20, then 100 distinct synthetic files only after the smaller gates pass. A clean Restricted test folder must stay within the connector's 100-entry scan limit.
4. Review authentication/RBAC for receipt access and manager decisions, malware quarantine, retention/restore, and provider privacy before any real-data pilot.

The current decision remains **GO for offline regression and local synthetic testing once SQL is healthy; NO-GO for real receipts, public Drive intake, and production scale claims**.
