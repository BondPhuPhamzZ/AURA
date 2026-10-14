# AURA consolidated evaluation evidence

**Current evidence cut:** 14/10/2026

**Purpose:** one public index for the measurements used in the proposal, slide deck, Build Log, README, and Demo Day package.

## Current baseline

| Evidence area | Verified result | Claim boundary |
|---|---|---|
| Release build | 0 warnings, 0 errors | Build gate only |
| Offline suite | 163/163 passed | Code tests, not 163 receipts |
| Live Verify Harness | 5/5 expected decisions in 65.004 seconds | Meets the five-case, under-90-second demo gate on the measured run |
| Duplicate Verify clients | 1 HTTP 200 + 4 HTTP 409 | Single-process idempotency; not distributed locking |
| Qwen3.8 Flash candidate | OpenRouter routing to Alibaba; reasoning `none`; fallback off | Frozen supervised demo candidate |
| Prospective mini-holdout | 8/10 exact, 0 missed, 2 over, 0 system/provider/429 errors, 28/30 critical fields | One small redacted run; not production accuracy |
| Regression on 12 original bytes | 10/12 exact, 0 missed, 2 over, 35/36 critical fields | Known-data stability evidence |
| Sequential repeat of same 12 | 11/12 exact, 0 missed, 1 over, P95 32.903 seconds | Not a new holdout |
| Five different uploads | 5/5 terminal; E2E P95 93.684 seconds | Queue-integrity test at AI concurrency 1; not the Verify SLA |
| Human workflow | Synthetic escalation, manager decision, Audit, and Undo passed | Workflow evidence, not real-user validation |
| Evidence localization | Hash-bound synthetic overlay prototype | Not integrated into extraction or policy; no unseen annotation benchmark |

## Historical evidence retained outside the active docs tree

Timestamped raw packages remain under the private evidence workspace and release history. Key historical measurements include:

- locked synthetic judge set: OpenRouter 15/15 decisions and Ollama 14/15;
- Test Kit v3 live: 11/15 decisions, 142/145 fields, 0 system errors;
- Test Kit v3.1: 13/15 exact, 0 missed escalations, 0 system errors;
- one-time locked real holdout: 10/15 raw, with one missed and three over; adjudicated analysis 11/15 after identifying one label error without changing raw evidence;
- Ollama 4B 16K isolated benchmark: 15/15 synthetic decisions but processing P95 213.429 seconds;
- Qwen3.8 Flash post-adjustment comparison: 15/15 on known receipts, followed by the independent 8/10 prospective mini-holdout above.

These measurements are not interchangeable. A later regression must never replace an earlier holdout result.

## Known mismatches

- FAHASA remains a stable fail-safe over-escalation because the validator does not treat `Thanh toán 91.500 đ` as sufficiently certain final-payable evidence.
- GS25_2 showed extraction variance across known-data repeats.
- Three O'Clock demonstrated that privacy blackout plus free-text warning can trigger FACT review even when core fields are correct.

The project intentionally does not weaken the final-total guard immediately before judging because doing so could turn tendered cash, cash received, or change into an approved payable amount.

## Raw-evidence integrity rules

1. Keep original timestamps, SHA-256 manifests, expected labels, and raw outcomes unchanged.
2. Never copy receipts, provider payloads, secrets, databases, or API-key dashboards into the public repository.
3. Correct encoding or parser presentation only from the preserved raw body and record both hashes; do not replay the provider to overwrite an outcome.
4. Apply the under-90-second requirement to the five-case Verify Harness, not the five-user upload concurrency test.
5. Do not claim three-user evidence until all consented records, confirmed quotes, one traceable product change, and one negative effect exist.
6. Use `CURRENT_PROJECT_STATUS.md` as the current state; use Git history and the frozen release tag for dated reports.

## Private evidence map

The curated Demo Day copy is generated under `D:\aura\demo_Day\Evidence`. Source packages remain under `D:\aura\demo_evidence`; real receipt images and raw provider material are excluded from the curated copy. The release tag and Git history retain the removed public snapshots if an audit needs their exact wording.
