# AURA final submission checklist

**Updated:** 14/10/2026

**Current state:** `CURRENT_PROJECT_STATUS.md`

**Submission manifest:** `../submission/README.md`

## Required deliverables

| Deliverable | Status | Final source |
|---|---|---|
| Public source repository and README | Ready | repository root `README.md` |
| Live application | Technical gate passed; final onsite rehearsal pending | URL in README |
| Five-slide deck | Ready | `../submission/AURA_5_SLIDES_FINAL.pptx` |
| Proposal, Vietnamese and English | Ready | `../submission/AURA_PROPOSAL_VI.*`, `../submission/AURA_PROPOSAL_EN.*` |
| One-page project Build Log | Ready | `../submission/AURA_BUILD_LOG.*` |
| Product demo video, maximum 3 minutes | Ready and uploaded | URL in `../submission/AURA_SUBMISSION_LINKS.*` |
| Verify Harness and evaluator instructions | Ready | `docs/JUDGE_LOCAL_SETUP.md`, `docs/RUNBOOK.md` |
| Three real-user feedback records | Open gate | `USER_STUDY_PROTOCOL.md`, feedback form in `../submission/` |
| Team Drive permissions and final links | Uploaded; incognito permission check pending | `../submission/AURA_SUBMISSION_LINKS.*` |

## Team Drive folder map

| Drive folder | Upload from repository | Required action |
|---|---|---|
| `00_READ_FIRST` | `../submission/AURA_SUBMISSION_LINKS.pdf` and a short folder `README.md` | Keep one current PDF only; verify every URL in an incognito window |
| `01_SLIDES` | `../submission/AURA_5_SLIDES_FINAL.pptx` | Ready |
| `02_PROPOSAL` | `../submission/AURA_PROPOSAL_VI.pdf`, `../submission/AURA_PROPOSAL_EN.pdf` | Ready |
| `03_VIDEO` | Official MP4 linked by `../submission/AURA_SUBMISSION_LINKS.*` | Keep one file clearly marked as the official under-three-minute video |
| `04_BUILD_LOG` | `../submission/AURA_BUILD_LOG.pdf` | Ready; this is the single project-wide one-page Build Log |
| `05_GITHUB_AND_RUNBOOK` | repository root `README.md`, `RUNBOOK.md`, `JUDGE_LOCAL_SETUP.md`, `TEST_CASES.md`, `../BUSINESS_RULES.md`, `../submission/AURA_WORKFLOW_SPEC.md` | Upload the six files; do not copy source code or secrets into Drive |
| `06_USER_EVIDENCE_PENDING` | `USER_STUDY_PROTOCOL.md`, `../submission/AURA_USER_FEEDBACK_FORM.docx`, `../submission/AURA_USER_FEEDBACK_FORM.pdf` | Upload templates now; add only consented, authentic records later |
| `07_EVIDENCE_SUMMARY` | `CURRENT_PROJECT_STATUS.md`, `EVALUATION_EVIDENCE.md` | Upload both current summaries |

## Before recording or presenting

1. Use the frozen Qwen3.8 Flash candidate with reasoning `none` and live fallback off.
2. Check `/healthz`: status ok, database/storage/migrations healthy, provider/model correct.
3. Run the five-case Verify Harness once; it must finish under 90 seconds with 3 AUTO and 2 ESCALATE.
4. Do not run Verify concurrently with an upload benchmark or another Verify batch.
5. Hide browser history, API dashboards, secrets, console output containing keys, and real receipt PII.
6. Rehearse the recovery path: refresh first, inspect status/Audit, then use the prerecorded demo if the upstream provider is unavailable.

## Before uploading to Team Drive

1. Populate every folder in the map above; keep one final copy of each logical deliverable.
2. Keep DOCX/PPTX source files in the team folder only when requested; use PDFs as the default review format.
3. Open every link in an incognito window using an account without owner privileges.
4. Confirm the video duration is at most 180 seconds and plays with audible narration.
5. Confirm the repository contains no real receipts, API keys, user secrets, databases, publish profiles, provider payloads, or billing screenshots.
6. Record the final Git commit and Drive upload time; do not silently replace evidence after the deadline.

## Claim boundaries for judges

- Say `163/163 offline tests`, not `163 receipts`.
- Say `8/10 prospective mini-holdout, 0 missed, 2 fail-safe over`, not production accuracy.
- Say `five concurrent uploads completed`, not a five-user production SLA.
- Say `single-process AI capacity control`; distributed limiting remains roadmap work.
- Say evidence localization is an isolated prototype, not a production capability.
- State plainly that the three-user gate is pending until signed/confirmed evidence exists.

## Files excluded from Git and public Drive folders

API keys, production connection strings, `.env`, user secrets, publish credentials, `.vs/`, `bin/`, `obj/`, `.tmp/`, local databases, raw receipts, unredacted screenshots, raw provider responses, and billing/key dashboards.
