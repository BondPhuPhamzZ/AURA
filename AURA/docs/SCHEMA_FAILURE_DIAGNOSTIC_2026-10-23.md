# Schema-failure diagnostic gate — 23 October 2026

This is a local, known-data regression check, not a new blind accuracy result or a production-readiness claim. The fifteen real receipts were previously used and the owner explicitly authorized sending them to the configured OpenRouter route. Raw images, model responses, receipt values, request IDs, and private result files are not included in this document or Git.

## What was changed

- `AI_SCHEMA_MISMATCH` now logs a bounded reason code and a schema-field identifier, when known, instead of attaching the JSON parser exception or logging an arbitrary JSON path. It does not log the model response or receipt values. The user-facing error and fail-safe queue behavior are unchanged.
- The private localhost runner accepts `-StartSequence` so one known case can be retried without retransmitting the other fourteen. It continues to require explicit provider-send confirmation and writes checkpoints only under Git-ignored `test_kit/results/`.
- Four diagnostic unit cases cover missing required evidence with absent protocol version, invalid JSON syntax, unsupported version, and an invalid field type. These tests verify that a sample private value is absent from the diagnostic tuple.

## Observations and limits

| Check | Result |
|---|---|
| Local `/healthz` before targeted run | `status=ok`; AI configured; DB available/up to date; storage available; OpenRouter `qwen/qwen3.8-flash`; configured AI concurrency 1; `strictZeroDataRetention=false` |
| Prior whole known-data regression on this branch | 14/15 completed; sequence 13 failed `AI_SCHEMA_MISMATCH` after bounded retry; 10/15 matched locked labels and 11/15 repeated the earlier decision. See [evaluation evidence](EVALUATION_EVIDENCE.md). |
| Targeted rerun of the *same* sequence-13 input | Three separate calls, 3/3 `COMPLETED`, zero provider errors; private checkpoint folders `private-receipts-20261023-221643`, `-221707`, and `-221722` are Git-ignored. Accuracy was **not** reassessed. |
| Build and automated tests after diagnostic change | Release build: 0 warnings/errors. Debug and Release default suites: 248/248 pass each. Two LocalDB integration cases remain opt-in; these runs do not claim they executed. NuGet advisory lookup emitted `NU1900` during test because the feed was unreachable, so this is not a fresh vulnerability audit. |

**Conclusion:** the old schema failure did not reproduce in three targeted attempts. Its precise malformed response cannot be reconstructed: the previous run did not retain raw model output or a safe reason code, by design. A stochastic model response is plausible but unproven. The new reason code will classify any future occurrence without storing sensitive content. Do not infer a 100% success rate or an accuracy improvement from these three calls. No new publish or multi-worker scale approval follows from this result.

Semantic Repair runs only after a response has become a valid DTO. The failed sequence-13 attempt in the earlier batch therefore could not reach Semantic Repair. In that earlier fifteen-case audit, four cases recorded `SemanticRepairApplied=true`, but this does not describe the failed case. A parser/schema repair retry and a semantic repair are different stages.

## Exact next gates

1. **P0 — complete schema validation:** parser currently demands every required field only when it infers a missing `evidenceContractVersion`; an explicit v2 response can omit another required property and deserialize to a default/null value. Add complete required-field and line-item shape validation for *both* version paths, update provider-stub fixtures, and prove fail-closed behavior before measuring model impact. This is a confirmed code-level gap, separate from the un-reproduced sequence-13 failure.
2. **P0 — controlled regression:** after the parser change, run offline tests, two sequential five-case synthetic runs, then the same fifteen known real images once with locked labels and a per-case diagnostic-code count. Preserve the old raw result. If a schema failure recurs, inspect only the safe code/field and provider finish reason/token usage; do not log raw JSON.
3. **P1 — quality evidence:** use a new consented, independently labeled holdout for accuracy; report critical-field agreement, false/missed escalations, semantic repair frequency, and run-to-run instability separately from completion rate.
4. **P1 — operations and scale:** keep one worker and private localhost test boundaries until shared private storage, distributed AI coordination/fencing, restore drill, malware handling, identity/authorization, retention approval, and load evidence are in place. `data_collection=deny` is configured but Strict ZDR remains unverified.

This gate does not alter the contest snapshot, business policy, decision thresholds, model choice, or public hosting.
