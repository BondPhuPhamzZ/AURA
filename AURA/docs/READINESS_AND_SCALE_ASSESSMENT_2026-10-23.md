# AURA readiness, Track A fit and scale boundary — 23 October 2026

This is a **source-and-evidence assessment**, not a certification or a statement that the public host runs this commit. The dated proposal, build log and five-slide deck remain the contest submission snapshot. See the [post-contest submission addendum](../submission/AURA_POST_CONTEST_ADDENDUM_2026-10-23.md) for a short outward-facing update.

## Drive, UX and deployment review — 26 October 2026

**Decision:** supervised synthetic MVP on one local host is demonstrated. This
does not mean all business rules are approved for a company, every OCR field is
correct, or the complete Drive pipeline is ready for SmartASP Production.

The configured Restricted test folder currently has three JPEG entries, two
subfolders and one TXT. The connector reads direct children of the configured
folder, rejects unsupported entries and does not recurse into subfolders. A
fresh local Drive run on 26 October used Qwen3.8-27B, Strict ZDR requested,
data-collection denial, one AI permit, no fallback and the 49-image approval
gate. All three images completed as AUTO with facts and matching stored
size/SHA-256; three jobs/six audit rows remained unchanged after process restart.
No Drive files or User Secrets were modified. This is current execution and
idempotency evidence, not a ground-truth accuracy score or a rerun of 100 files.
The older 100-file run has 30 distinct contents, 98 completed and two fail-safe
errors; retain its original model/date and denominator.

The repeatability profile has duplicate escalation disabled. One of the three
images was detected as duplicate and still routed AUTO by that configuration.
Same-source idempotency prevents another job for the same Drive file/content;
it does not prevent a different file ID from representing the same expense.
Choose and test `DecisionPolicy__EscalateDuplicateReceipts=true` before any
business pilot. Do not silently change this flag or re-score past regressions.
Drive metadata currently records a synthetic submitter/department, not a
verified employee identity; owner/cost-center mapping remains a business gate.

Browser/source review confirms tab changes and completed workflow POSTs use a
full navigation. Page reloads do not themselves enqueue an AI request; they
re-read SQL/view components. The applicant fragment also polls every five
seconds, including while hidden. There is no load/usability evidence proving
this cannot overload SQL at scale or that staff find it easy to use. Follow-up
UX should refresh the active read-only fragment after a committed action,
preserve tab/month/focus/scroll, avoid overlapping/stale responses, pause hidden
polling and retain an explicit refresh recovery path. Existing SQL rowversion,
antiforgery and server-confirmed decisions must remain intact.

HITL exists as employee handoff, manager route-specific action, audit and Undo
before retention expiry. FACT/PRIVACY acceptance is manual intake, not payment
approval. Timely handling still requires an operator to open the app; there is
no assigned reviewer, reminder/SLA notification or endpoint RBAC. The tabs are
demo roles, not an authorization boundary. Do not present the workflow as a
fully autonomous finance agent or promise unattended approval quality.

All 12 reference images in the two supplied local folders were inspected:

| Reference topic | Apply to AURA now | Keep for a later requirement |
| --- | --- | --- |
| Folder layout, API/worker responsibilities | Keep Controllers, Services, policy and infrastructure boundaries; extraction stays separate from deterministic decisions | Separate worker deployment only when operations/scale justify it |
| Dockerfile/Compose/network/volumes | Harden build context and publish inputs, external secrets, private persistent receipt storage | Actual container runtime with TCP SQL Server; LocalDB is not the Linux container database |
| Health, logs, restore, controlled updates | Keep aggregate queue monitoring, bounded failures, backup/restore and rollback evidence | Alert delivery, SLOs and cross-host restore/load tests |
| Kafka and the large multi-agent sample stack | SQL durable queue is sufficient for the measured local workflow | Kafka, Redis, vector DB, RAG and microservices require a concrete need; they are not an agent-completeness checklist |
| Retrieval scoring/filtering | Do not use a retrieval score or an arbitrary 0.75 threshold to approve expenses | A versioned policy-document assistant could later use retrieval, with independently calibrated relevance |
| Encoder/decoder, MoE/SSM and model naming | Distinguish model internals from app architecture; verify actual image support and evidence | Local VLM selection/VRAM/quantization benchmarking remains deferred |

The pictured Python `pip install -r pyproject.toml` is not an AURA setup command.
Use the repository's .NET SDK build. Compose short-form `depends_on` orders
startup but does not wait for application health; use healthchecks and
`service_healthy` when a container deployment is actually tested. A `.env` file
is not an encrypted vault; a persistent volume is not a backup. See
[Docker startup ordering](https://docs.docker.com/compose/how-tos/startup-order/).

Packaging review found an actual boundary defect: nine raw `TestResults` JSON
files entered a local publish probe. The project now excludes test/runtime and
known secret inputs. A repeat publish with a synthetic runtime JSON canary
excluded all protected paths and retained required runtime files. Docker's real
ignore matcher also excluded 21 synthetic private canaries and retained four
public runtime inputs. The scratch fixture build does not prove the .NET/SQL
pipeline runs in a container; no image was pushed or hosting changed.

The live public health endpoint still returned Flash, Strict ZDR false and
Verify enabled on 26 October. It does not expose a verified source commit, so
it is not proof the newer master is deployed. Source synchronization and web
deployment are distinct. Drive remains Development/loopback-only; do not set a
public site to Development to bypass that boundary. IIS idle/recycle behavior
can pause in-process workers; see [Microsoft IIS guidance](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/?view=aspnetcore-8.0#application-initialization-module-and-idle-timeout).
Use the current [deployment gate](DEPLOYMENT.md) before preparing a controlled
manual synthetic staging site. Raw metadata, logs, screenshots, TRX and packaging
proof stay Git-ignored at `test_kit/results/mvp-deploy-review-20261026/`.

## Local verification update — 25–26 October 2026

The current synthetic sandbox has opt-in seven-day finalized-record retention,
a verified SQL/image restore drill and scheduled managed-backup expiry. It also
approves exact synthetic image hashes before external inference; unknown bytes
stop for manual privacy review. Final Debug/Release suites passed 371/371 each
with LocalDB and PowerShell integration enabled. The legacy contest database
remains unchanged. These are local synthetic results, not real-data deletion,
PII redaction, hosting or multi-server validation. The historical counts and
expired-key finding below describe their dated checkpoints. See [current
status](CURRENT_PROJECT_STATUS.md) and [model evidence](MODEL_ROUTE_COMPARISON_2026-10-24.md)
for the latest scope and remaining real-data/pilot gates.

The user confirmed this phase as synthetic-only, preserving the old database.
Full v3.1 through the approval gate on `649885e` matched 15/15 decisions and
143/146 facts; P50/P95 was 5,139/32,739 ms, so no latency improvement is claimed.
R3-06 remained FACT after one unresolved Semantic repair. The runtime emitted
EF MARS/savepoint warnings; this is not a warning-free operations claim.

A 26 October restart smoke encountered a real DNS failure and stopped safely.
Transport errors are now sanitized known provider failures, covered alongside
caller cancellation; final suites passed 371/371 each. One post-fix recovery
smoke on `5143ab4` returned AUTO with 11/11 facts in 7,578 ms. The failed run is
retained; this does not establish provider/network availability guarantees.

## Source update — 24 October 2026

The historical assessment below remains as-of 23 October. Through 24 October, the code adds an `IReceiptFileStore` abstraction, a local default and an opt-in GCS adapter. GCS uploads are create-only and reads are generation-pinned; image bytes are size/hash-verified against SQL before inference. Fake-client tests cover the SDK call contract and size-race handling. Source-key intake now preserves bytes after pre-insert database read failure, and verifies uncertain insert results with a persisted (`AsNoTracking`) query rather than an EF tracked entity. Two independent SQL intake instances racing the same source key create one request, one audit and one file; an audit-triggered transaction rollback is not misreported as success and a retry in a fresh scope completes once. The manual upload flow now accepts an optional UUID `Idempotency-Key`; the browser preserves a pending key by file SHA-256 in same-tab session storage and reuses it across bounded network/429/5xx retries, while SQL uniqueness arbitrates concurrent retries. A LocalDB integration race using two independent intake instances confirms a shared manual key creates one row, audit and file. Clients omitting the header still get a new request ID per submission; the key is ephemeral and expires with its browser session. Independent LocalDB contexts racing manager approval and manager Undo each produce exactly one state/audit write; SQL `rowversion` rejects the stale write. A fresh retry after an ambiguous response loss now recognizes the same completed manager decision or latest Undo and returns `alreadyApplied` without a duplicate audit; an in-flight stale request still conflicts and can retry after reloading. Local filesystem crash debris is swept only when a generated AURA staging `.tmp` matches a strict filename pattern, is older than 24 hours, and can be opened exclusively; finished receipts and GCS objects are not deleted. Runtime smoke against a newly migrated empty LocalDB observed `deleted=1; skipped=0` for one synthetic stale staging file, then returned HTTP 200 / `/healthz=status=ok`; Drive/Verify were disabled, queue was empty, no model call or receipt upload occurred, and the temporary DB/folder were dropped. The latest full Debug and Release suites pass **332/332** with LocalDB integration enabled and zero skipped; Release build has 0 warnings/errors, and EF has no pending model changes. GCS auth supports explicit ADC and external JSON-key modes, with unit coverage for mutually exclusive configuration and credential-file path policy, including `GOOGLE_APPLICATION_CREDENTIALS`; actual ADC identity discovery and GCP access remain untested. This remains fake-GCS/simulated-host evidence, not a real bucket or separate-machine test. The adapter has not been exercised with a real bucket or IAM identity; this does not change the **NO-GO** decision for multi-host/live production. Queue monitoring emits structured transition warnings and recorded-byte snapshots, but no metrics/alert backend or real bucket-usage feed is connected. See [the storage rollout gates](SHARED_RECEIPT_STORAGE.md). Retention deletion remains intentionally absent until retry/recovery and retention approval are addressed.

Follow-up verification on 24 October adds LocalDB races for manager Undo and bulk handoff, plus controller-level LocalDB coverage for lost-response retries after manager decision and Undo. Across independent contexts, each race persisted exactly one state change and audit; the stale context was rejected by SQL `rowversion`. The latest full-suite result on source commit `0568b5a` supersedes the earlier counts above: Debug and Release each pass **333/333** with LocalDB integration enabled, and the Release build has 0 warnings/errors. This extends workflow write-integrity evidence only; it does not change the real-bucket, multi-host, restore, alerting, identity, malware, retention, or load-test gates below.

## Decision

| Level | Decision | Basis and limit |
|---|---|---|
| Supervised contest MVP | **Yes, for a controlled demo** | Manual image intake, durable SQL queue, schema and semantic validation, deterministic C# policy, specific escalation, manager action, audit and undo are implemented. Release tests: 231/231. Prior live Verify: 5/5 within 90 seconds on the earlier deployed candidate. |
| All Track A requirements | **Core minimum demonstrated; advanced evidence incomplete** | Enterprise feedback marked the six minimum requirements and spec checks as met. The independent prospective ten-case sample was 8/10 with zero missed and two over-escalations; it is too small to settle general accuracy. Three qualifying hands-on user sessions, confirmed quotations, a feedback-derived change and a negative-effect measurement have not been evidenced. New unseen judge cases cannot be guaranteed. |
| Near production-ready | **No** | Anonymous case/image/status/manager/report surfaces, no server-side identity/RBAC, no malware quarantine, no approved retention/deletion and restore drill, no independently verified route-specific ZDR, distributed SQL gate not enabled or validated on hosting, and no sustained capacity/operations evidence. These are release gates, not optional polish. |

The current source is not claimed to be published on the live URL. An expired OpenRouter key also means `/healthz` can report `aiConfigured=true` while inference fails: health checks configuration presence, not a charged model call. Rotate the secret and run a single synthetic inference smoke before relying on the demo. Never commit the key.

## What the measured runs prove

- 231/231 Release offline tests pass (23 October rerun); they are code tests, not receipt accuracy. Release build and EF model checks from the same baseline reported zero warnings/errors and no pending model changes. A `NU1900` advisory-feed warning appeared in a no-restore test invocation, so that invocation alone is not a fresh vulnerability audit.
- The local Restricted-folder Drive test ingested 100 files as 100 SQL jobs after the identity fix; **only 30 image byte streams were distinct**. Ninety-eight completed and two stopped fail-safe. Queue-wait P95 was about 847 seconds. This proves a bounded single-worker pipeline and restart/idempotency behavior, not 98% decision accuracy or 1,000-file capacity.
- Fifteen consented, distinct real images completed through **manual localhost intake**, not Drive. There is no hash-matched independent label set for that run, so accuracy was not calculated. `data_collection=deny` was requested; Strict ZDR was not verified.
- The prospective contest mini-holdout of ten images produced 8/10 exact routes, zero missed escalations and two over-escalations in one small run. Keep this separate from known-data regression and the later unlabeled tests.

## Scope in an organization

The workflow concerns **employee expense reimbursement**. Employees from any department *could* submit receipts if their employer adopts one common expense policy; accounting/finance or an operations reviewer checks exceptions, and a designated budget owner/manager decides authority and policy exceptions. The current code does **not** implement organization-wide rollout, department-specific policy, cost-center routing, delegated authority, tax validation, ERP/payment posting or roles. Therefore “covers all departments” is a possible future deployment model, **not** a demonstrated capability. AURA does not transfer money or certify invoice authenticity.

## UX: why some actions reload the page

The applicant escalation queue already refreshes a read-only HTML fragment every five seconds, and individual job status is polled. However, `ForwardToManager`, `ForwardAllToManager` and `.js-workflow-form` send a fetch POST and then call `window.location.assign(...)`; tab navigation also reloads the page. This was an intentional way to reconstruct all tables from committed SQL state and avoid stale, conflicting fragments, but it makes the UI feel as though it requires refresh after each action.

**Low-risk next UX iteration:** keep POST endpoints and SQL as the only source of truth; after a successful mutation, fetch fresh read-only `EmployeeEscalations`, `ReviewerQueue`, `AuditTrail` and dashboard fragments (or one versioned summary response), then replace only affected DOM regions. Preserve the selected tab, date filter, focus and scroll; announce updates through an accessible live region. Disable duplicate submissions until the response, show an explicit stale/error state if any fragment fetch fails, and retain a manual full-page refresh as recovery. Never optimistically display an approval before server confirmation. Cover forward/decision/undo, two-tab races, failed fragment fetches and accessibility in browser tests before changing the existing reload behavior. SignalR is unnecessary at this scale; bounded polling is simpler and already partly implemented.

## Multi-instance concurrency

The SQL worker **conditionally claims individual jobs** with a processing lease, so multiple workers can compete without normally claiming the same pending row. This is not sufficient for safe multi-server operation. With the default `Vision__UseSqlDistributedGate=false`, `VisionInvocationCoordinator` uses a per-process `SemaphoreSlim` and cooldown dictionary: with three server processes configured as `MaxConcurrentRequests=1`, up to **three** provider calls can run concurrently. The opt-in SQL gate now serializes processes sharing the same SQL database, but has not been enabled on hosting or chaos/load-tested across physical hosts. `WorkflowOperationGate` is also process-local. Expired job leases can be reclaimed while the former call is still in flight, and a stale worker must not be able to overwrite a newer result. Local receipt files on one host are not automatically available on another.

Correction after inspecting the model and running a two-worker LocalDB race test: the request already has a SQL `rowversion` concurrency token. Worker A was paused inside extraction, its lease was expired and reclaimed by B, and B completed. When A resumed, EF raised `DbUpdateConcurrencyException`; its stale result and audit were not saved. Exactly one audit and B's terminal result remained. This verifies **terminal-write protection for that race**, not provider-capacity control. Since the prior 23/10 measurement, shared cooldown state has been added to SQL; the opt-in SQL gate now couples the shared lock, cooldown and fencing token. Neither is enabled on hosting or proven on real separate hosts. Keep the multi-instance gate closed until shared storage and crash/load tests are done.

The smallest safe scale path is to keep one **logical** provider capacity budget while adding web replicas separately from workers. The SQL global permit, shared cooldown, fencing token and job lease renewal are implemented and covered by local tests. Source-key idempotency is covered for deterministic source intake, not for repeat manual submissions; the next gates are private shared/object storage and staging verification across separate hosts. The permit's fencing token is checked before accepted results are returned, while job rowversion still protects terminal SQL writes. Separate provider concurrency from web request count; initially one worker may still be the right choice under the provider quota. Only then load-test 100 **distinct** and 1,000 distinct labeled scenarios, measure queue wait/processing P50/P95, provider 429, cost, crash/reclaim and lost/duplicate work. Do not simply increase `Vision__MaxConcurrentRequests` or server count.

An opt-in `Vision__UseSqlDistributedGate=true` serializes AI calls from app processes using the **same SQL database**. It requires `Vision__MaxConcurrentRequests=1`, acquires a session-owned SQL application lock, records cooldowns against the SQL UTC clock, and increments a monotonic fencing token on each permit. The extractor validates the owning session and token after inference; a stale owner cannot return accepted facts. LocalDB integration tests cover separate coordinators, cooldown clock skew, session loss, successor fencing and migration application. Debug/Release suites are 285/285; LocalDB worker integration is 15/15. Migrations `20261023181204_AddSharedVisionProviderCooldown` and `20261023182852_AddVisionInvocationFence` have **not** been applied to hosting. The default remains `false`. This is **not approval to scale**: receipt storage still needs a real shared private backend, a provider request already accepted may continue/be billed after SQL session loss, and real-host outage/load evidence is absent.

The worker claims a SQL job **before** entering the AI gate. Its lease defaults to 600 seconds; waiting for a busy global permit is not bounded by the OpenRouter 90-second HTTP timeout. The worker now renews that lease with a separate SQL context every 10–30 seconds (based on configured lease length), conditional on request ID, `PROCESSING` state and attempt number. Claim/reclaim, heartbeat, terminal lease checks and worker audit timestamps use SQL `SYSUTCDATETIME()` rather than each app host's clock. On ownership loss or renewal failure it cancels extraction; before terminal save it stops renewal, checks the attempt still has a live lease and reloads rowversion so its own heartbeat does not invalidate the write. LocalDB tests prove a long blocked extraction renews and saves exactly one terminal audit, and an attempt change cancels a blocked extraction without another audit. The older reclaim test still proves stale terminal writes are rejected. This is local SQL evidence, not clock-skew testing between separate SQL hosts.

The SQL AI permit now monitors `APPLOCK_MODE` over its owning session every five seconds. If the connection or exclusive lock disappears, its cancellation token cancels provider work and surfaces `AI_GATE_LOST`, which keeps the job retryable instead of accepting a result under uncertain global capacity. A LocalDB test kills the owning SQL session and verifies the signal and fail-safe disposal. This narrows the failure window but does not prove an external provider stops immediately, prevent a request already accepted from being billed, or rule out short overlap before detection. Real-host outage/chaos tests remain a gate; do not increase worker count yet.

The storage path accepts an absolute private directory outside `wwwroot`, so a restricted shared file mount could be configured without changing receipt IDs. The worker now checks stored size and SHA-256 against the intake record before calling AI and keeps a read-only handle during extraction; a same-size tamper test stops before the model call. This **does not** provision a shared store, verify cross-host ACLs/SMB semantics, or replace a private object-storage design. Rows predating persisted file metadata may contain the migration defaults (`0`/empty hash) and will now fail safe for manual recovery rather than sending unverifiable bytes.

Drive/source-key intake now stages complete bytes in the same private directory before renaming to the final filename. If the SQL write result is uncertain, it preserves the completed file; a retry reuses it only after size and SHA-256 match, while a partial/conflicting file fails closed without overwrite. Local fault-injection tests cover that retry and reuse of one source key with changed bytes. This reduces a process-crash gap but does **not** make SQL plus filesystem atomic, clean crash-leftover temporary files, validate network-share rename semantics, or settle retention/backup policy. Manual intake without the optional `Idempotency-Key` cannot automatically reconcile an ambiguous commit; the browser now retains a pending per-file key within the same tab/session.

A LocalDB integration test now gives two simulated app hosts different content roots and one absolute private synthetic-file directory. The second host reads and verifies the file from that shared path, then completes the SQL job. This proves the current path/configuration flow can use a common directory; it does **not** test a real network share, independent machines, access-control lists, outages, atomic writes across SMB, or image backup/restore.

An isolated LocalDB drill backs up a database containing one synthetic pending job/audit marker together with its local image file, changes both source state and image bytes after backup, restores into a second temporary database and receipt directory, and verifies pending state, audit marker, file size, and SHA-256. This is a paired local rehearsal, not an atomic cross-system snapshot. It does **not** prove hosting-provider backup availability, GCS/object recovery, restore permissions, independent-host access, RPO/RTO, or a full incident runbook. Those remain pilot gates.

A separate in-process queue monitor logs aggregate pending/processing/failed counts, oldest pending age and SQL-recorded receipt bytes once per minute. It emits transition warnings at 80% of configured outstanding capacity, when pending age reaches `ReceiptProcessing__QueueWarningAgeSeconds` (default 300 seconds), and when the failed total increases after monitor startup. It does not expose these values through public `/healthz` or include receipt identifiers or filenames. LocalDB verifies aggregate counts/age/recorded bytes; unit tests check age and capacity thresholds. Recorded bytes omit orphan/temp objects and do not represent provider-reported storage usage. OpenRouter's optional numeric nonnegative `usage.cost` is logged per response in USD beside token counts; missing or malformed cost is unavailable and provider-supplied strings are never copied into this telemetry field. Provider failures are logged as structured warnings/errors. This is not an alert delivery pipeline, cumulative budget, billing reconciliation, or cross-instance metric; metrics export, log collection/alert rules, actual object storage usage, and multi-instance monitoring remain open operational gates.

## Change gates before any broader pilot

1. Replace the expired provider key in private configuration and smoke one synthetic image. Keep Drive disabled and Verify disabled on public hosting.
2. Lock independent labels and run a diverse accuracy set; separately finish the three-person user study and operational baseline, including negative effects.
3. Decide organizational identity/role ownership, then protect **every** read and mutation endpoint. No bespoke registration is necessary: enterprise SSO plus server-side authorization can be added later, but omitting it means no general real-user pilot now.
4. Add image quarantine/decoder safety, private shared storage access controls, secret rotation, approved retention and restore tests; review provider route and data-processing terms. One-week retention is not yet implemented or approved.
5. Add distributed coordination and observability before multiple instances or unattended high-volume Drive intake. The Drive connector remains Development/loopback synthetic-only and off by default.

The contest-era solution should remain intact as a tagged snapshot; post-contest improvements are additive and evaluated against that baseline. No new login/registration implementation, production deploy, or policy change is implied by this document.
