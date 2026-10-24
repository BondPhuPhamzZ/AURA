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
