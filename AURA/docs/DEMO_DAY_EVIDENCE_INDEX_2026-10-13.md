# AURA Demo Day evidence index

## Purpose

This index is the curated map for `D:\aura\demo_Day\Evidence`. It keeps judge-ready summaries and integrity records together without copying real receipt images, raw personal responses, API keys, billing screenshots, databases, or user secrets.

## Current baseline

| Evidence area | Result | Public report |
|---|---|---|
| Build and test | Release build clean, 163/163 offline tests | `docs/CURRENT_PROJECT_STATUS_2026-10-13.md` |
| Final local gate | Release build 0 warnings/errors; 163/163 tests pass on 14/10 | `docs/FINAL_LOCAL_GATE_2026-10-14.md` |
| Final live health recheck | HTTP 200; deployed candidate still OpenRouter/Qwen3.8 Flash, reasoning none, fallback off | `docs/FINAL_LIVE_HEALTH_RECHECK_2026-10-14.md` |
| Live health and Verify | Health ok, Verify 5/5 in 65.004 seconds, duplicate clients 1×200 + 4×409 | `docs/QWEN38_FLASH_LIVE_VERIFY_2026-10-12.md` |
| Model candidate | Qwen3.8 Flash, Alibaba route, reasoning none | `docs/QWEN38_FLASH_BENCHMARK_2026-10-12.md` |
| Prospective mini-holdout | 8/10, 0 missed, 2 over, 0 system/provider/429, 28/30 critical fields | `docs/OFFICIAL_HOLDOUT10_2026-10-13.md` |
| Original-byte regression | 10/12, 0 missed, 2 over, 35/36 fields; 5 uploads completed | `docs/REGRESSION12_ORIGINAL_BYTES_RESULT_2026-10-13.md` |
| Sequential repeat | 11/12, 0 missed, 1 over, P95 32.903 seconds | `docs/REGRESSION12_SEQUENTIAL_REPEAT_RESULT_2026-10-13.md` |
| Human workflow and mobile | Synthetic manager flow, Audit, Undo, responsive and phone gates | `docs/LIVE_POST_HOLDOUT_VALIDATION_2026-10-06.md` |
| Evidence localization | Synthetic prototype only; no production integration | `docs/EVIDENCE_LOCALIZATION_PLAN_2026-10-13.md` |

## Curated folder structure

```text
Evidence/
  00_INDEX_AND_SCOPE/
  01_RELEASE_GATES/
  02_LIVE_HEALTH_AND_VERIFY/
  03_MODEL_AND_PROVIDER/
  04_PROSPECTIVE_HOLDOUT/
  05_REGRESSION_AND_CONCURRENCY/
  06_HUMAN_WORKFLOW_AND_MOBILE/
  07_EVIDENCE_LOCALIZATION/
  08_USER_STUDY_PENDING/
  09_SUBMISSION_INTEGRITY/
```

## Files selected from private raw evidence

Only summaries, CSV metrics, health snapshots, and evidence-hash manifests should be copied:

- live Qwen3.8 health, corrected concurrent Verify summary, and evidence SHA-256;
- Qwen3.8 comparison summary, candidate summary, and five-upload report;
- holdout consent/scope, locked manifest, privacy review, official summary, confusion matrix, critical-field comparison, latency table, mismatch register, and integrity notes;
- 12-source-byte regression manifest, result summary/report, result CSV, health snapshots, and package hashes;
- sequential-repeat manifest, result summary/report, result CSV, health snapshots, and package hashes;
- synthetic workflow/mobile and concurrency summaries;
- Ollama go/no-go and measured summary for fallback context.

Raw `receipt.jpg`, provider-ready images, raw acceptance/status bodies, request logs, app console logs, API dashboards, and any secret are excluded from the Demo Day copy. Their original private folders remain the source of truth when a judge requests a controlled audit.

## Claim rules

1. Never replace the 8/10 prospective result with a later 10/12 or 11/12 regression result.
2. Never describe 163 code tests as receipt accuracy.
3. Apply the 90-second requirement to the five-case Verify Harness, not to the five-user upload queue test.
4. State that FAHASA is a stable over-escalation and GS25_2 showed extraction variance.
5. Do not claim three-user evidence until all three consented records and a traceable product change exist.
6. Do not present the evidence-overlay prototype as production explainability.

## Integrity procedure

After copying the curated package, generate `SHA256SUMS.txt` over all files and store a directory inventory with size and last-write time. If a source summary is corrected only for encoding, keep the raw and corrected hashes together and label the correction. Do not regenerate provider results.
