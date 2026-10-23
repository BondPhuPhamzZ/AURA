# Local Drive MVP validation — 22 October 2026

This is a measured **local, Development-only** run, not a deployment approval or an accuracy benchmark. The source build after the idempotency fix is `4644e45`; this report was added afterward. AURA ran on loopback with separate LocalDB databases and Git-ignored private receipt directories. The Restricted Drive folder was shared only with the test service account at Viewer level; real receipts were **not** placed on Drive.

## Dataset and method

| Set | Scope | Ground-truth status |
|---|---:|---|
| Synthetic Drive batch | 100 files, 100 distinct names, **30 distinct SHA-256 values**; 70 are byte-identical copies | Throughput/queue and idempotency evidence, **not** 100 independent accuracy cases |
| Consented local real receipts | 15 distinct files, previously used | Technical regression only. None of their hashes matches the candidate 15-case locked manifest from 12 October; previous labels were not reused and decision accuracy was **not assessed**. |

The 5-file and 20-file Drive stages completed 5/5 and 20/20 with no provider error. They were progressively uploaded into the same batch folder. The 100-file runs used real Google Drive API reads, local SQL/storage, the configured OpenRouter `qwen/qwen3.8-flash` route, semantic validation and C# policy. AI concurrency was 1. The 15 real files used the normal localhost multipart upload endpoint with Drive disabled; the owner explicitly authorized sending them to the configured provider. `providerDataCollectionDenied=true` but `strictZeroDataRetention=false`; no Strict ZDR claim is made.

## Exact observed outcomes

| Run | Durable jobs / source files | Terminal outcomes | Processing P50 / P95 | Queue wait P50 / P95 | Audit / private files |
|---|---:|---|---:|---:|---:|
| Original 100-file build | **104 / 100** (4 extra jobs) | 103 completed, 1 `AI_RATE_LIMIT` → `ESCALATE_SYSTEM_ERROR` | 5,826 / 13,989 ms | 189,777 / 331,297 ms | 208 / 104 |
| Fixed 100-file build | **100 / 100** (30 distinct contents) | 98 completed, 2 fail-safe: 1 `AI_RATE_LIMIT`, 1 `AI_SCHEMA_MISMATCH` | 5,434 / 18,063 ms | 651,121 / 847,152 ms | 200 / 100 |
| Consented real 15 | 15 / 15 | 15 completed, 0 provider/client errors | 24,087 / 43,103 ms | 5 / 47 ms | 30 / 15 |

The fixed synthetic decision routes were 40 `AUTO_APPROVE`, 14 `ESCALATE_AUTHORITY`, 29 `ESCALATE_FACT`, 15 `ESCALATE_POLICY`, and 2 `ESCALATE_SYSTEM_ERROR`. The 15 real routes were 3 `AUTO_APPROVE`, 10 `ESCALATE_FACT`, 1 `ESCALATE_POLICY`, and 1 `ESCALATE_PRIVACY`. These are **route counts, not correctness scores**. Failed model/provider calls were retained in the denominator and were not rerun to improve the result. Percentiles use SQL `PERCENTILE_CONT(0.5/0.95)` over terminal jobs, with `ProcessingLatencyMs` for processing and `DATEDIFF_BIG(millisecond, QueuedAt, ProcessingStartedAt)` for queue wait; they are not extrapolated from UI timestamps.

For the fixed 100-file run, first queue to last terminal timestamp was 2026-10-22 16:34:56–16:51:59 UTC (17m03s). Staged arrivals and a provider cooldown affect queue percentiles; these numbers are not an SLA. The 15-file sequential real run lasted 2026-10-22 16:27:16–16:33:33 UTC (6m17s). In both final runs, no pending/processing job remained, `ProcessingAttemptCount` never exceeded 1, and the sampled new audit details had 0 rows matching the former filename/submitter/SHA-256 patterns. The fixed Drive job/audit counts remained **100/200** after an additional poll and after a process restart.

## Defect found and verified fix

The original source key used `Drive file ID + version`. A metadata-only update can change the Drive version without changing image bytes; the original batch produced 104 jobs from 100 unique file names. The connector now uses `folder ID + file ID + Drive MD5` for *content identity* while retaining the version comparison to reject an edit during download. Unit tests cover metadata-only version changes and changed bytes. A live one-file smoke run showed 1 job/2 audit events after a metadata rename, another poll, and restart. The fresh 100-file run showed exactly 100 jobs/200 audit events after poll and restart.

**Migration caveat:** the identity algorithm changed without a database migration. Re-enabling the patched connector against an older local Drive test DB can re-ingest old files once. The validation deliberately used a new isolated DB; do not reuse an old Drive sandbox DB without explicit reconciliation. This connector remains disabled by default and must not be published to the public host.

## Reproducibility and limits

- Isolated LocalDBs: `AuraSyntheticBatch_20261022` (original), `AuraSyntheticPostFix_20261022` (fixed), `AuraDriveIdempotency_20261022` (one-file smoke), and `AuraRealConsented_20261022` (real). The app was stopped after each test; no existing database was deleted.
- Private per-case real results and input hash sequence: `test_kit/results/private-receipts-20261022-232716/` (Git-ignored). Original real photos remain in the owner's `D:\aura\Real_Receipt`; receipt copies in `App_Data/receipts-real-consented-20261022/` are also Git-ignored. No original image, key, raw fact, name, or private result is included in this report or commit.
- Release suite: **220/220 tests pass**; Release build 0 warnings/errors with `--no-restore`; EF reports no pending model changes. A test invocation emitted `NU1900` because the NuGet vulnerability feed was unreachable, so a fresh dependency audit was **not** confirmed. Local `/healthz` was `ok`, DB up to date, storage available, AI permit 1.
- This run did not measure memory peak, multi-instance locking, 1,000-file pagination, independent decision accuracy, human-review quality, consent/legal compliance, or recovery from actual database/storage loss. The 100-entry folder limit and one AI permit mean this is not a high-volume production intake design.

**Decision:** GO for a supervised **local synthetic MVP** and further controlled regression. The 15 consented images demonstrate that the manual pipeline executes end-to-end, not that its decisions are correct or that public use is authorized. NO-GO for production or a general real-user pilot until identity/RBAC, image malware quarantine, private storage access control, retention and restore, provider privacy/Strict ZDR review, monitoring, and a labeled independent evaluation are completed. The 2/100 fail-safe rate is a technical outcome, **not** “98% accuracy.”
