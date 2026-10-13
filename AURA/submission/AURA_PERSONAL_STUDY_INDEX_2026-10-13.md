# AURA personal study index

## Read in this order

1. `00_AURA_PERSONAL_MASTER_INDEX.md` - map of the private defense material.
2. `16_AURA_FINAL_STUDY_GUIDE_2026-10-07.md` - compact end-to-end revision guide.
3. `Project_Folder_Architecture/explain/01_FINAL_REQUEST_FLOW_AND_PROMPT_LAYERS.md` - request flow and prompt boundaries.
4. `Project_Folder_Architecture/explain/02_SOURCE_CODE_AND_FOLDER_REFERENCE.md` - code and folder lookup.
5. `Project_Folder_Architecture/explain/AURA_PROJECT_ARCHITECTURE_GUIDE.md` - architecture and responsibilities.
6. `Project_Folder_Architecture/explain/AURA_TESTING_DATA_AND_IMPLEMENTATION_WORKFLOW_GUIDE.md` - test/evidence workflow.
7. `20_FALLBACK_AND_429_FINAL_STRATEGY_2026-10-12.md` - provider limits, backoff, fallback, and claims.
8. `21_LIVE_CAPACITY_AND_MULTI_JUDGE_DEFENSE_2026-10-12.md` - queue and concurrency defense.
9. `23_OFFICIAL_HOLDOUT10_AND_DEMO_DECISION_2026-10-13.md` - prospective holdout interpretation.
10. `25_REGRESSION12_ORIGINAL_BYTES_AND_CONCURRENCY_2026-10-13.md` - original-byte regression and five-upload evidence.
11. `26_REGRESSION12_SEQUENTIAL_REPEAT_2026-10-13.md` - stability repeat and latency.
12. `28_EVIDENCE_LOCALIZATION_IMPLEMENTATION_DECISION_2026-10-13.md` - why production highlighting was not shipped.
13. `07_ONSITE_FINAL_PLAYBOOK_2026-10-06.md` - on-site operating checklist.

## What to remember

- The model extracts evidence; deterministic C# policy decides the route.
- Policy order is FACT, then POLICY, then AUTHORITY.
- The claimed amount is not sent to the extraction prompt.
- The durable SQL queue survives process restarts; one in-process signal only wakes the worker.
- AI concurrency is one to protect the upstream route. Five uploads can queue safely, but latency increases.
- Live fallback is disabled. Ollama is a supervised local contingency, not the primary Demo Day path.
- The prospective ten-receipt holdout is 8/10 with zero missed and two fail-safe over-escalations.
- The 11/12 result is a repeat on known data and must not be described as a new holdout.
- Evidence localization is an isolated synthetic prototype because false highlights would create false confidence.

## Safe claims

- Release build: 0 warnings, 0 errors.
- Offline test suite: 163/163 passed.
- Live Verify Harness: 5/5 in 65.004 seconds.
- Current candidate: OpenRouter route to Qwen3.8 Flash through Alibaba, reasoning none.
- Human decision, Audit, and Undo are present in the demo workflow.

## No-go claims

- Do not claim production accuracy, causal model superiority, or autonomous approval readiness.
- Do not say 163 tests means 163 receipts.
- Do not claim production evidence highlighting, multi-instance rate limiting, production RBAC, safe PDF, malware scanning, or object storage.
- Do not present incomplete three-user sessions as completed evidence.
