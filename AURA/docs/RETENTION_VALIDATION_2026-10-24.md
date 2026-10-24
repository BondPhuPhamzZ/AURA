# Local retention candidate validation — 2026-10-24

## Policy implemented

- Fixed maximum retention: seven days from the automated final approval or the
  manager's recorded final decision.
- `RetentionExpiresAt` is stored in SQL and indexed. The migration backfills only
  already-final records; unresolved escalations and pending/processing/retryable
  work remain without an expiry.
- When explicitly enabled, AURA performs a purge sweep before serving requests,
  then every five minutes by default. It deletes the receipt object and the SQL
  request/facts/audit rows. Undo is rejected after the recorded deadline.
- If the startup sweep itself fails while retention is enabled, AURA fails to
  start instead of serving data while overdue records may remain. A later
  scheduled sweep failure is logged and retried on the next interval.
- Backup manifests record the database backup hash, receipt hashes, data expiry
  and calculated purge deadline. The backup remover is report-only unless passed
  `-Apply`, and only manages named backup sets with a valid manifest.
- Both mechanisms are local best-effort. If the host is powered off, cleanup is
  delayed until AURA or the Windows scheduled task runs again. Backup manifests
  do not prove that a `.bak` is valid; SQL checksum/verify and a restore drill are
  still required.

## Evidence

- Release build: passed, 0 warnings and 0 errors.
- Full Release test suite with `AURA_RUN_LOCALDB_INTEGRATION=1`: 339 passed,
  0 failed, 0 skipped.
- Full Debug test suite with `AURA_RUN_LOCALDB_INTEGRATION=1`: 339 passed,
  0 failed, 0 skipped.
- EF migration check (normal build path): no pending model changes. An initial
  `--no-build` check used a stale compiled snapshot and reported pending changes;
  the rebuilt check resolved this. Do not use `--no-build` for this check after
  editing migrations.
- Migration `20261024130944_AddReceiptRetentionDeadline` applied successfully to
  an isolated LocalDB database. No user's database was migrated.
- Retention-enabled startup smoke against an isolated empty LocalDB returned
  `/healthz` `ok`, database/schema current, `retentionCleanupEnabled=true`, and
  `retentionDays=7`. No AI request or user receipt was used.
- Purger LocalDB integration tests cover selective deletion of an expired final
  decision, preservation of pending/unresolved/recent records, image and audit
  removal, retry after an image is already missing, and blocking Undo after the
  deadline.
- Backup registration script passed against an isolated zero-row LocalDB and
  synthetic files. Backup remover synthetic test confirmed default report-only,
  removal of only an expired managed set with `-Apply`, and preservation of a
  future-dated set and an unmanaged directory.
- PowerShell parser and `git diff --check` were reviewed. `git diff --check`
  initially found an extra trailing blank line in `RUNBOOK.md`; it was removed
  before final verification.

## State and remaining gates

`DataRetention:Enabled` remains **false by default** and was enabled only in the
isolated startup smoke. This candidate has not been deployed or enabled on a
user/demo database. Before enabling it on a real local instance:

1. Back up and restore-test the exact database plus receipt directory.
2. Confirm the final-decision status list with the data owner; confirm legal and
   operational approval for seven-day deletion, including audit deletion.
3. Choose a private backup root outside the repository and configure Windows
   Task Scheduler for daily cleanup plus an at-startup catch-up trigger. Review
   a report-only run before enabling `-Apply`.
4. Enable `DataRetention:Enabled=true` only after migration and backup readiness
   are confirmed. Verify `/healthz`, cleanup logs and a synthetic end-to-end
   expiry before retaining real user data.
5. Continue to treat privacy as an open gate: the current privacy classifier is
   post-inference and does not redact receipt images before provider submission.

No personal receipt images, service-account keys, connection strings, or provider
credentials were included in these tests or this evidence file.
