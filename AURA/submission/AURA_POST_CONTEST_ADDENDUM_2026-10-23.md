# AURA post-contest status addendum — 23 October 2026

**26 October local profile update:** Visual Studio F5 and the PowerShell launcher
now share one prepared synthetic profile at `127.0.0.1:5000`, using the same
isolated LocalDB, private receipt store, 49-image SHA-256 approval set,
Qwen3.8-27B Strict ZDR routing request, one AI permit and optional read-only
Drive poller. On this checkpoint, both start paths returned `/healthz=ok`;
Release tests passed 377/377 and frontend refresh tests 8/8. The previous
R3-01/R3-12 Drive pair remained idempotent after restart: 5 requests, 10 audit
rows and 0 queued jobs. These are configuration and known-data checks, not a
new accuracy result or a production privacy certification. See the
[current local profile](../docs/LOCAL_SYNTHETIC_PROFILE_2026-10-26.md).

The proposal, one-page build log and five-slide deck in this folder document the **submitted contest candidate**. They are deliberately preserved, including their then-current 163-test and claimed-amount descriptions. They must not be read as specifications for the newer source or as evidence that the newer source has been published.

**26 October verification:** final Debug/Release suites passed 371/371 each
with LocalDB and PowerShell integration enabled. A restart smoke hit a DNS
failure and stopped safely; after transport-error classification was corrected,
one recovery smoke returned AUTO with 11/11 facts in 7,578 ms. Both runs remain
in the evidence. This phase is explicitly synthetic-only, preserving the old
database; it is not approval for real-data pilot or a hosting release.

**25 October update:** the separate local synthetic sandbox passed 367/367 tests
in each of Debug and Release with LocalDB and PowerShell integration enabled.
The latest full synthetic regression through exact-hash approval on `649885e`
matched 15/15 decisions and 143/146 facts; E2E P50/P95 was 5,139/32,739 ms.
Semantic attempted one repair on an obscured total and safely retained FACT.
Unknown image bytes were blocked before inference and preserved for manual
review. Seven-day retention, SQL/image restore and scheduled managed-backup
expiry were exercised on synthetic data; the old contest database was kept
unchanged. This does not establish PII detection/redaction, real-data retention,
public-host readiness or multi-server safety. The paragraphs below preserve the
23 October checkpoint; use the linked current status for later evidence.

The current local source keeps the same Track A boundary: the vision model extracts receipt facts, schema/semantic validation checks them, C# policy selects `AUTO_APPROVE` or an `ESCALATE_*` route, and people decide exceptions with an audit trail. The intake UI now omits manual claimed amount; each selected JPG/PNG becomes its own durable SQL job. The UI accepts up to ten files per selection, not an unlimited server-side batch. Dashboard/CSV and privacy triage are post-feedback additions. A read-only Google Drive connector has been tested only with synthetic receipts on Development loopback and remains disabled by default on public hosting; there is no Gmail intake.

As of this addendum, 231/231 Release offline tests pass. The local Drive run produced 100 jobs from 100 files, but only 30 distinct image contents; 98 completed and two stopped fail-safe. Fifteen consented real images completed through manual localhost upload, without independent matching labels, so no accuracy result is claimed for them. The older prospective ten-receipt contest mini-holdout remains 8/10 exact, zero missed and two over-escalations. The public URL has not been verified as running this newer branch. An expired provider key requires private rotation and a synthetic inference smoke before another live demo.

**Readiness:** supervised, controlled MVP yes; production-ready and unrestricted real-user pilot no. Track A's core workflow is present, while the three-person study and broader independent accuracy evidence remain open. Identity/authorization, malware quarantine, retention/restore, route-specific privacy verification and multi-instance coordination are not complete. See [current status](../docs/CURRENT_PROJECT_STATUS.md), [readiness and scale assessment](../docs/READINESS_AND_SCALE_ASSESSMENT_2026-10-23.md), and [measured local validation](../docs/DRIVE_MVP_VALIDATION_2026-10-22.md).
