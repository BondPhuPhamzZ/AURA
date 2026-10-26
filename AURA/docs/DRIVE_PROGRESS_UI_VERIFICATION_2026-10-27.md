# Drive progress UI verification — 27 October 2026

## Change and expected behavior

The Development/loopback synthetic Drive worker already persisted accepted files to
SQL and sent them through the queue. The employee result component previously
selected only unforwarded escalations, so Drive PENDING, PROCESSING and AUTO
records were invisible there until a full refresh or audit lookup. It now
adds the latest 20 unforwarded Drive records created in the last 24 hours;
unforwarded escalations continue to appear regardless of age.
The existing visible-tab `fetch` fragment poll reads that SQL view every five
seconds. A duplicate-content marker appears when SQL reports one; the decision
policy and Drive intake behavior did not change.

## Checks actually run

| Check | Result |
|---|---|
| Release build | 0 warnings, 0 errors |
| Release .NET tests with LocalDB and PowerShell integration enabled | 378 passed, 0 failed, 0 skipped |
| Frontend polling/race/error tests | 8 passed, 0 failed |
| Local Release profile `/healthz` | `ok`; DB available; 0 pending migrations; Drive configured; `ApprovedImagesOnly` with 49 hashes; ZDR routing request and data-collection denial true; 1 AI permit |
| Local worker on startup | Scanned 10 direct Drive entries; no new model inference claimed |
| `GET /Home/EmployeeEscalations` | HTTP 200; five persisted Drive rows; existing AUTO result and duplicate marker visible |

The test suite covers selector behavior for PENDING, PROCESSING, AUTO and
escalated Drive rows and excludes forwarded, old and public-profile rows.
There was no live PENDING job during the HTTP check, so the timing from a *new*
Drive upload to each displayed state has not been measured end to end in this
revision. The user's earlier two synthetic files reached their expected final
statuses; this verification did not repeat the model benchmark or change labels.
An initial integration attempt inside the restricted tool sandbox could not
create the Windows user's LocalDB instance (`0x89c50118`); the same full suite
passed in the Windows session with LocalDB access. The failed attempt remains
part of the test record rather than being treated as an application failure.

## Publication boundary

This is a local synthetic UI improvement. The Drive connector remains
Development/loopback only, with Restricted folder access and a read-only
service account; it is not enabled on SmartASP. A web-upload release still
needs hosting backup, configuration and migration checks, deployment to a
separate staging environment, then a fresh synthetic upload, audit/Undo and
restart/storage check. User identity, malicious-file isolation, PII controls,
provider terms, reviewer assignment and multi-host capacity remain open for
real-data or public Drive use. `provider.zdr=true` is an endpoint routing
condition; it does not keep images inside the app or erase application copies.
