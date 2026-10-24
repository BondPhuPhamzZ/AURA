# Strict-ZDR Qwen candidate regression — 2026-10-24

## Scope and reproducibility

This report preserves the **pre-fix** result and appends the live rerun after
the narrow R3-04 policy correction. Both are repeated regressions on the locked
**synthetic** Test Kit v3.1, not blind benchmarks or real-world accuracy
estimates. Fifteen cases per run went through manual intake, the SQL-backed
queue, worker, semantic validator, and policy engine. Each run used a fresh
isolated LocalDB and one local app process. Drive and Verify Harness were
disabled. App-generated receipt files and temporary databases were removed
after the runs. Raw results remain in git-ignored local `TestResults/` and
`test_kit/results/` folders and are intentionally not committed.

## Committed privacy-role and policy regression — 24/10

Commit `0568b5a` adds role-specific privacy instructions, a synthetic-only
four-image privacy evaluation kit, per-case privacy-category scoring, and a
Semantic-repair invariant: valid `personal_data:<CATEGORY>` markers from the
first extraction are merged into the repaired DTO so a second model response
cannot silently clear the signal. The test specifically simulates an initial
privacy marker followed by a repair response with no marker.

After the commit, the privacy kit was run three times sequentially on one fresh
isolated local runtime and LocalDB using `qwen/qwen3.8-27b`, LocalFileSystem,
`strictZeroDataRetention=true`, provider data-collection denied, one AI request
at a time, and fallback/Drive/Verify disabled. Each 4-case run matched 4/4
expected decisions and 7/7 expected categories, with 0 false negatives, 0 false
positives, 0 system/provider errors and 0 fallbacks. Across the three runs this
is 12/12 decisions and 21/21 category matches. One request had a 15.975-second
end-to-end latency; this is a small, non-SLO sample. The set contains only four
purpose-built synthetic role cases and is not a general privacy-detector recall
benchmark or proof that a real receipt was redacted before inference.

The final prompt revision was then checked once against the locked v3.1 set on
the same committed source and a fresh LocalDB. It matched 15/15 decisions, 0
missed escalations, 0 over-escalations, 0 system/provider errors/fallbacks, and
143/146 expected facts (97.95%). The three fact differences were only
`invoiceDateEvidence` strings with an added printed label in R3-03, R3-07 and
R3-11. Semantic repair was attempted once for R3-06, remained unresolved because
the printed total is intentionally obscured, and safely stayed `ESCALATE_FACT`.
This one post-commit run is not a three-run repeatability claim; the separate
45/45 repeat table below belongs to the earlier source/prompt checkpoint.

The post-commit `/healthz` reported `status=ok`, local storage, current SQL
schema, Strict-ZDR request and data-collection denial enabled, concurrency 1,
and fallback/Drive/Verify disabled. Full Debug and Release suites passed
333/333 each with LocalDB integration enabled; Release build had 0 warnings and
errors; EF reported no pending model changes. Route settings and health output
prove app configuration only, not a provider contract, legal compliance, or
pre-inference protection. Results live only in Git-ignored `test_kit/results/`.

## Local Ollama route feasibility screen — 24/10

To assess the privacy/locality option without sending a receipt to an external
provider, one synthetic R3-01 image was run through a fresh isolated LocalDB,
private local receipt directory, and `Ollama` at `127.0.0.1:11434` using
`qwen3-vl:4b-instruct`. `/healthz` was `ok`, schema current, LocalFileSystem
available, fallback off, one AI permit, and durable queue enabled. The case
completed in **278,164 ms** as `ESCALATE_FACT`; the locked expected decision is
`AUTO_APPROVE`. Initial inference took 168,338 ms and Semantic flagged
`bookingId:IDENTIFIER_ASSIGNMENT_INVALID`; the single correction attempt took
109,169 ms, after which the workflow safely escalated. There was no provider
error and no external provider was used.

This single-case screen shows that local inference can keep the image on this
machine, but the current 4B route on this host is too slow for interactive
multi-receipt use and did not match this case's expected decision. It is not an
accuracy estimate or a representative latency benchmark. Any local route needs
a labeled multi-case benchmark on target hardware before it can replace the
OpenRouter route; keeping the image at rest locally does not itself prevent
OpenRouter from receiving it when that provider is selected.

## Post-fix live rerun

After adding the narrowly scoped OCR variant `bia hò sơ` to the stationery
allowlist (while retaining escalation for explicit beer context), the same
locked 15-case manifest was run three more times against the live OpenRouter
route. All three runs used source commit
`2e10e534d082314269341af1114a13b7e19f6922`, the same manifest SHA-256 shown
below, model `qwen/qwen3.8-27b`, Strict ZDR enabled, and provider data
collection denied. The local runtime reported LocalFileSystem, SQL schema
current, one AI request at a time, fallback off, and Drive off.

| Run | Decision matches | Missed escalations | Over-escalations | System errors | Exact expected facts | E2E P50 / P95 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 15/15 | 0 | 0 | 0 | 144/146 (98.63%) | 3.656 / 7.707 s |
| 2 | 15/15 | 0 | 0 | 0 | 144/146 (98.63%) | 3.600 / 7.161 s |
| 3 | 15/15 | 0 | 0 | 0 | 144/146 (98.63%) | 4.098 / 8.178 s |
| Combined | 45/45 (100%) | 0/30 expected escalations missed | 0/15 routine over-escalated | 0/45 | 432/438 (98.63%) | Per-run values shown |

The manifest SHA-256 was
`5781E37F3E6C42EA0C8154B01F83FA2613AD097AC6D1B1851DCFA12663872462` for all
three runs; the source manifest SHA-256 was
`796F1E2E78F830A84E21D010DAD273F94229168D0CFEB32393F96A8EDE0F447A`.
Each run had zero provider errors and zero fallback attempts. Case R3-04
returned its expected `AUTO_APPROVE` in all three runs, so the identified OCR
policy false positive did not recur on this fixture.

The two fact mismatches in every run were only the `invoiceDateEvidence`
string for R3-03 and R3-07: the extracted string included the printed label
`Ngày hóa đơn:` before the correct date. The normalized date remained
`2026-10-05`, and both decisions matched their expected outcomes. This is a
strict evidence-string comparison difference, not a date normalization or
decision mismatch.

The benchmark's R3-06 intentionally hides the printed final total. Semantic
repair was attempted in all three runs but did not resolve
`totalAmount:FINAL_TOTAL_EVIDENCE_INVALID`; all three correctly remained
`ESCALATE_FACT` with no inferred total. This confirms the fail-safe repair
boundary, not successful repair. The 6-case post-run instrumentation check
also observed one repair attempt, zero resolved repairs, one unresolved case,
and zero privacy-review classifications. The privacy count is only what this
small fixture produced; it does not validate privacy-detector coverage.

The repeatability result is a meaningful improvement over the pre-fix R3-04
false escalation, but the sample is still only 15 locked synthetic cases. It
does not establish production accuracy, robust privacy detection, legal
compliance, provider contract terms, latency SLOs, or multi-host capacity.

## Current-source rerun — 24/10, commit `06346de`

After the semantic privacy-marker preservation change, the same locked v3.1
manifest was run once more against source commit `06346dec9cf508d34e69df6c5c7a1866baeb54d7`
on a fresh LocalDB and private local receipt directory. The 15 synthetic images
were sent through manual intake, durable SQL queue, OpenRouter, Semantic and
policy. Before upload, `/healthz` was `ok`, SQL was current, local storage was
available, `qwen/qwen3.8-27b` was selected, provider data collection was denied,
Strict ZDR was requested, concurrency was 1, and fallback/Drive/Verify were off.

| Measure | Result |
| --- | ---: |
| Expected decisions | 15/15 |
| Missed / over-escalations | 0 / 0 |
| Provider/system errors; fallback | 0; 0 |
| Exact expected facts | 143/146 (97.95%) |
| End-to-end P50 / P95 | 3.620 / 7.802 s |
| Semantic repair | 1 attempted; 0 resolved; R3-06 remained `ESCALATE_FACT` |

The three fact differences were only `invoiceDateEvidence` strings: R3-03,
R3-05 and R3-07 included the printed `Ngày hóa đơn:` label before the correct
date. This differs from earlier runs where the affected case IDs varied, so
exact evidence-string extraction is not fully repeatable even though decisions
matched. The final health check remained `ok`; queue monitoring observed zero
pending, processing and failed jobs. The isolated DB and receipt folder are
under Git-ignored `test_kit/results/current-06346de-20261024/`; raw runner output
is under Git-ignored `test_kit/results/20261024-195745/`. These results confirm
the current-source regression only; they do not establish general accuracy,
privacy-detector coverage, contractual retention, production readiness or
multi-host behavior.

## Historical pre-fix run

| Run | Decision matches | Missed escalations | Over-escalations | System errors | Exact expected facts | E2E P50 / P95 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 15/15 | 0 | 0 | 0 | 136/146 (93.15%) | 7.224 / 16.951 s |
| 2 | 14/15 | 0 | 1 | 0 | 135/146 (92.47%) | 5.177 / 31.465 s |
| 3 | 15/15 | 0 | 0 | 0 | 134/146 (91.78%) | 5.670 / 14.396 s |
| Combined | 44/45 (97.78%) | 0/30 escalations missed | 1/15 routine cases over-escalated | 0/45 | 405/438 (92.47%) | Per-run values shown |

All 45 jobs completed without provider errors or fallback. `/healthz` in each
run reported `strictZeroDataRetention=true`,
`providerDataCollectionDenied=true`, model `qwen/qwen3.8-27b`, database up to
date, and `sqlDistributedAiGateEnabled=false`. The request selected ZDR-eligible
OpenRouter endpoints; response metadata during the runs showed multiple
upstream hosts, including DekaLLM, Reka, Near AI and Phala. This is a Qwen model
family served by non-Alibaba infrastructure, not Alibaba-hosted inference.

The locked judge-manifest SHA-256 for all runs was
`5781E37F3E6C42EA0C8154B01F83FA2613AD097AC6D1B1851DCFA12663872462`;
each run records source commit `0038acd7163d268d76c0696926b25b4d5d46d5ca`.

## What varied and what Semantic did

The only decision that varied was `R3-04` (expected `AUTO_APPROVE`): runs 1 and
3 read the item as “Bìa hồ sơ” and approved; run 2 read “Bia hò sơ” and returned
`ESCALATE_POLICY` because the policy treats beer as prohibited alcohol. This is
a conservative false escalation, not a missed escalation. It is concrete
evidence that diacritic/OCR variance can change policy outcomes.

Across the 438 expected fact comparisons, all normalized fields matched except
`invoiceDateEvidence` (24 mismatches: the model included a printed label such as
“Ngày hóa đơn:” in addition to the date) and `merchantName` (9 diacritic
mismatches). No normalized amount or document-number mismatch was observed in
this set.

Semantic repair was attempted for `R3-06` in all three runs. Its final-total
evidence remained invalid after repair, so all three results stayed
`ESCALATE_FACT`; no unsafe approval occurred. This demonstrates the repair
attempt plus fail-safe escalation path, **not** successful repair of that
contradiction. The fixture intentionally obscures the printed final total;
reconstructing a number from line-item arithmetic would not satisfy the
required printed-final-total evidence contract.

## Root-cause fix after the recorded runs

The R3-04 false escalation was traced to a narrow OCR variant: `Bìa hồ sơ`
became `Bia hò sơ`. The policy already exempted common stationery readings such
as `Bia hồ sơ` and `Bia ho so`, but did not include the erroneous grave accent
on `hò`. The stationery phrase allowlist now includes exactly `bia hò sơ`;
regression tests verify that this OCR reading no longer triggers an alcohol
policy escalation while explicit beer context (`bia chai`) still does. The
three-run figures in the historical section below are the **pre-fix**
benchmark. The new post-fix runs above measure the corrected source. The full
Debug and Release suites passed 332/332 each with
`AURA_RUN_LOCALDB_INTEGRATION=1`, including the opt-in SQL integration cases.

## Decision

`qwen/qwen3.8-27b` is a technically working Strict-ZDR candidate and a possible
supervised-MVP evaluation route. Keep it **out of the default configuration**
for now: the post-fix repeat is still only 15 synthetic cases, the date evidence
string has two stable formatting mismatches, and the Semantic repair correctly
leaves an obscured total unresolved. The 45/45 repeated decision matches show
this locked fixture is stable after the narrow policy fix, not broad accuracy.
This set does not establish generalization, legal compliance, provider contract
terms, real-user accuracy, latency SLOs or multi-host capacity. In particular, the SQL
distributed AI gate was off and no real receipt, bucket, or production host was
used.

Next quality gate: independently label a new synthetic set that stresses
Vietnamese diacritics, near-policy words (such as “Bìa” vs “Bia”), faint totals,
and evidence provenance; require zero missed escalations and explain/resolve
false escalations before considering a model default change. Route/ZDR inventory
must be rechecked immediately before any deployment because provider endpoints
change over time.
