# Strict-ZDR Qwen candidate regression — 2026-10-24

## Scope and reproducibility

This is a repeated regression on the locked **synthetic** Test Kit v3.1, not a
blind benchmark or real-world accuracy estimate. Fifteen cases were run three
times (45 requests total) through the normal manual-intake endpoint, SQL-backed
queue, worker, semantic validator and policy engine. Each run used a fresh
isolated LocalDB; the app used one local process on loopback. Drive and Verify
Harness were disabled. All app-generated receipt images and the temporary DB
were deleted after the runs. Detailed raw synthetic results remain in the
git-ignored local `TestResults/Qwen3_8_27B_ZDR_20261024_0[1-3]/` directories;
they are intentionally not committed.

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
three-run model figures above are the **pre-fix** benchmark and were not
rewritten as post-fix model results. Debug and Release suites each passed
302/302 tests with `AURA_RUN_LOCALDB_INTEGRATION=1`, including the opt-in SQL
integration cases. After the later SQL intake-capacity work, the complete suites
were rerun at 305/305 in Debug and 305/305 in Release with LocalDB integration
enabled; the new shared-capacity race case passed as well.

## Decision

`qwen/qwen3.8-27b` is a technically working Strict-ZDR candidate and a possible
supervised-MVP evaluation route. Keep it **out of the default configuration**
for now: the small locked synthetic set showed one variable false escalation,
date-evidence formatting differences, and merchant-name OCR variance. The
false-escalation policy cause has a narrow code fix with regression coverage,
but no full provider benchmark has been rerun after that fix. This set does not
establish generalization, legal compliance, provider contract terms, real-user
accuracy, latency SLOs or multi-host capacity. In particular, the SQL
distributed AI gate was off and no real receipt, bucket, or production host was
used.

Next quality gate: independently label a new synthetic set that stresses
Vietnamese diacritics, near-policy words (such as “Bìa” vs “Bia”), faint totals,
and evidence provenance; require zero missed escalations and explain/resolve
false escalations before considering a model default change. Route/ZDR inventory
must be rechecked immediately before any deployment because provider endpoints
change over time.
