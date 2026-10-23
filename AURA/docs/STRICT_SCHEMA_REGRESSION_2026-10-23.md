# Strict-schema regression gate — 23 October 2026

This is a **known-data local regression**, not a blind benchmark or approval to publish. The owner consented to the fifteen real receipts being sent through the configured OpenRouter route. The images, raw AI output, receipt values, hashes, request IDs, and case-level private results remain outside Git.

## Candidate and tests

- The v2 parser now requires every declared top-level field, each required array, and each line-item key even when `evidenceContractVersion` is explicitly present. A missing protocol version is inferred only for an otherwise complete response. Invalid shape fails closed as `AI_SCHEMA_MISMATCH`; it is not silently deserialized to null/default facts.
- OpenRouter logs a bounded diagnostic code, schema field, response length, and allowlisted finish reason. Ollama logs a bounded diagnostic without carrying a provider-derived parser exception into worker logs. Neither logs raw model JSON.
- Semantic validation still uses the original evidence-bearing issues *internally* for its one repair prompt. Logs, persisted `SemanticRepairIssues`/`ValidationIssues`, and the resulting policy reason now use static field/error codes. The fail-safe escalation path is unchanged.
- Release and Debug default suites: **253/253 passed** each. Two opt-in LocalDB worker-restart/rate-limit tests: **2/2 passed** on isolated temporary databases. Release build: **0 warnings, 0 errors**. EF: **no pending model changes**. The test command reported `NU1900` because the NuGet advisory feed was unreachable; this is not a vulnerability scan.

## Local end-to-end observations

| Previously known set | Completed | Decision matches locked synthetic label | Failure or instability |
|---|---:|---:|---|
| Five synthetic images, run A | 4/5 | 3/5 | TK-06: `AI_SCHEMA_MISMATCH`; TK-10: `ESCALATE_FACT` instead of the expected approval. |
| Same five synthetic images, run B | 5/5 | 4/5 | TK-10 again: `ESCALATE_FACT`; the model omitted the visible merchant name. |
| Fifteen consented, previously used real images, one pass | 13/15 | Not assessed by this runner | Sequences 2 and 3: `AI_SCHEMA_MISMATCH` after bounded retry; zero intake/client errors. |

For the two real-image failures and synthetic TK-06, the provider responded HTTP 200 with a two-character, non-object completion and `finish_reason=stop` on both attempts. The safe diagnostic is `NOT_JSON_OBJECT`. This is **not** a missing required-field diagnostic, and Semantic Repair cannot run before a valid DTO exists. It does not prove the parser change caused the provider behavior. A focused test reproduces the two-attempt fail-safe path with a stub response.

A temporary extra merchant-name recheck was tried on the same five synthetic images: 3/5 decision matches, including the same TK-10 miss and one system error. Because it added a provider call without a demonstrated quality gain, that experiment was **reverted** and is not part of this candidate.

## Decision and next gates

**NO-GO for publishing this candidate, increasing worker count, or claiming production readiness.** The strict shape check closes a demonstrated parser gap and the semantic log change closes an observed data-exposure path, but the configured model route still produces intermittent unusable completions. Keep the contest/live snapshot and Drive sandbox state unchanged.

Next, investigate provider/model output and token accounting using only safe metadata; compare a supported alternative on the same synthetic cases under a fixed budget; then rerun a fresh independently labeled, consented holdout. Track completion, critical-field accuracy, decision agreement, false/missed escalations, Semantic Repair frequency, and latency separately. Shared private storage, distributed AI fencing, restore drill, identity/authorization, malware handling, retention approval, and load testing remain prerequisites to multi-instance or real-user use. `data_collection=deny` does not establish Strict ZDR; the current route reports `strictZeroDataRetention=false`.
