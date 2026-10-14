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
| Product demo video, maximum 3 minutes | Must record/upload final cut | script: `../submission/AURA_OFFICIAL_VIDEO_SCRIPT_3_MIN_VI.md` |
| Verify Harness and evaluator instructions | Ready | `docs/JUDGE_LOCAL_SETUP.md`, `docs/RUNBOOK.md` |
| Three real-user feedback records | Open gate | `USER_STUDY_PROTOCOL.md`, feedback form in `../submission/` |
| Team Drive permissions and final links | Manual gate | `../submission/AURA_SUBMISSION_LINKS.*` |

## Before recording or presenting

1. Use the frozen Qwen3.8 Flash candidate with reasoning `none` and live fallback off.
2. Check `/healthz`: status ok, database/storage/migrations healthy, provider/model correct.
3. Run the five-case Verify Harness once; it must finish under 90 seconds with 3 AUTO and 2 ESCALATE.
4. Do not run Verify concurrently with an upload benchmark or another Verify batch.
5. Hide browser history, API dashboards, secrets, console output containing keys, and real receipt PII.
6. Rehearse the recovery path: refresh first, inspect status/Audit, then use the prerecorded demo if the upstream provider is unavailable.

## Before uploading to Team Drive

1. Upload one final copy of the slide deck, both proposal PDFs, Build Log PDF, video, GitHub URL, live URL, and any required track form.
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
