# Drive pipeline hardening evidence — 22 October 2026

Scope: local source and synthetic fixtures. This record does not certify a live deployment, a real-receipt pilot, or 100-image throughput.

## Changes

- Drive metadata and download GETs retry HTTP 429/500/502/503/504 at most twice after the initial request. Retry-After is honored up to five seconds per wait; 401/403 are not retried. A failed scan is retried by the existing poll loop. No Drive file is modified.
- New `AI_QUEUED` and `AI_PROCESSED_*` audit details omit original filenames, submitter identifiers, departments, receipt hashes, and free-text AI reasons. Existing historical audit rows are unchanged. The receipt row and extracted-facts JSON still contain data needed by the demo and need access control before real use.
- Fake-API tests cover one 429 followed by success and a 403 that stops without intake. The intake test checks that the new queue audit omits filename and submitter fields.
- The built-in `HttpClient` request logging for the Drive client is suppressed because its request URL contains Drive file IDs. Local test storage and the 100-image duplicate fixture directory are ignored by Git.

## Verification

| Check | Result |
|---|---|
| `dotnet build AURA.csproj -c Release --no-restore` | PASS: 0 warnings, 0 errors |
| `dotnet test tests/AURA.Tests/AURA.Tests.csproj -c Release --no-build --no-restore` | PASS: 218/218; no skipped tests |
| `dotnet ef migrations has-pending-model-changes --no-build` | PASS: no model changes |
| Focused Drive test run | PASS: 20/20 after the change |
| Isolated LocalDB and `/healthz` | PASS outside the command sandbox: `AuraDriveHardening_20261022` migrated; health `ok`, database available/up to date, pending migrations 0, Drive disabled; dashboard HTTP 200 |
| Live Google Drive/OpenRouter after this change | PASS on the isolated local test setup: four direct folder entries scanned, three synthetic JPG jobs `COMPLETED/AUTO_APPROVE`, zero failed; three private receipt files |
| Poll and restart idempotency | PASS: still three jobs and six audit events after repeated polls and one app restart |
| New audit detail check | PASS in test DB: six events, zero rows containing legacy `File=`, `Submitter=`, `SHA256=`, or `Reason=` patterns |
| 5/20/100 synthetic end-to-end batches | NOT RUN; no capacity claim |

The live local run used a new `AuraDriveHardening_20261022` database and a separate receipt storage directory. The connected Drive folder showed three synthetic JPG files and one `.txt` rejection fixture before it was enabled. AI processing latency among the three jobs ranged from 4,879 to 14,115 ms; this is a tiny sample and not a percentile or SLA. The app was stopped after the restart check; persistent user-secrets still have Drive disabled and malware scanning required. An earlier test invocation with restore emitted NU1900 because the NuGet vulnerability feed was unreachable. A sandboxed LocalDB command initially failed to inspect the instance registry; an approved out-of-sandbox check succeeded, then the existing instance started. No existing database was deleted. The 100-image local fixture directory contains 100 files but only 30 distinct SHA-256 values; it is unsuitable for a 100-independent-receipt accuracy claim.

## Next gates

1. Test actual Drive 429/5xx, revoked credentials, timeout, changed-version and malformed/oversized bytes without modifying the current accepted test set. Current 429/403 evidence uses a fake API; the live run did not induce upstream errors.
2. Run 5, 20, then 100 distinct synthetic files only after the smaller gates pass. A clean Restricted test folder must stay within the connector's 100-entry scan limit.
3. Review authentication/RBAC for receipt access and manager decisions, malware quarantine, retention/restore, and provider privacy before any real-data pilot.

The current decision remains **GO for offline regression and local synthetic testing with the isolated SQL database; NO-GO for real receipts, public Drive intake, and production scale claims**.
