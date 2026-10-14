# AURA technical documentation

This folder contains only the active, judge-facing technical documentation. Historical progress reports and dated snapshots were removed from the active tree to prevent conflicting instructions; they remain recoverable from Git history and the frozen release tag.

| Document | Purpose |
|---|---|
| `CURRENT_PROJECT_STATUS.md` | Single source of truth for the current verified baseline and open gates |
| `EVALUATION_EVIDENCE.md` | Consolidated map of public results, claim boundaries, and private raw-evidence locations |
| `RUNBOOK.md` | Operating and recovery procedure |
| `JUDGE_LOCAL_SETUP.md` | Local setup for evaluators |
| `DEPLOYMENT.md` | Deployment configuration and safety boundaries |
| `LOCAL_OLLAMA.md` | Optional supervised local contingency |
| `TEST_CASES.md` | Test inventory and expected behavior |
| `DATASET_EVALUATION_GUIDE.md` | Dataset, manifest, and evaluation protocol |
| `MEASUREMENT_PLAN.md` | Metrics and interpretation rules |
| `EVIDENCE_LOCALIZATION_PLAN.md` | Bounding-box overlay feasibility and non-production boundary |
| `USER_STUDY_PROTOCOL.md` | Consent-based three-user study protocol |
| `SUBMISSION_CHECKLIST.md` | Final submission and rehearsal checklist |

Raw receipts, raw provider responses, API dashboards, databases, secrets, and personally identifying material are intentionally excluded from Git. Private evidence packages keep their original timestamps and hashes; they are not renamed or regenerated to appear newer.

Submission artifacts are indexed separately in `../submission/README.md`.
