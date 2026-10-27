# AURA reimbursement review solution proposal

> **27 October local evidence:** A manual synthetic Drive check showed R3-01 AUTO with a duplicate-content marker and later reviewer actions for R3-06/R3-13. Three unapproved real web images took the privacy escalation route before model inference. No end-to-end Drive latency or real-data accuracy was measured. This does not change the historical contest proposal or authorize Drive on SmartASP. [Dated evidence and limits](../docs/USER_DRIVE_PRIVACY_CHECK_2026-10-27.md).

> **Post-contest source update — 26 October 2026.** The submitted 14 October
> proposal below is retained as a historical benchmark. The current local
> synthetic profile accepts web uploads and reads approved images directly from
> a Restricted Drive folder through a read-only service account. Each accepted
> Drive file becomes a durable SQL request; the employee table now shows its
> `PENDING`/`PROCESSING`/final state through visible-tab `fetch` polling without
> a page reload. Unchanged Drive files are skipped by source identity; new copies
> can be flagged as duplicate content and still run because duplicate escalation
> is disabled in the repeatability profile. Current Release tests: 378/378 unit
> suite; see [current status](../docs/CURRENT_PROJECT_STATUS.md) for integration
> evidence. The profile requests Strict ZDR routing and data-collection denial,
> and checks exact approved synthetic image hashes before external inference.
> This is not evidence of contractual zero retention, PII redaction, production
> identity controls, or readiness to enable Drive on SmartASP. The 163-test and
> 5/5 Verify figures below belong to the submitted candidate.

**Track:** OrganizationAI
**Challenge:** Spec A The Escalation Referee
**Team:** The Deciders
**Author:** Pham Gia Phu
**Evidence version:** 14 October 2026

## Executive summary

AURA is a first-line reimbursement review assistant. It accepts a receipt image and the amount claimed by an employee, uses a vision model to extract structured facts, validates those facts, and applies deterministic C# policy rules. Clear routine claims can continue automatically. Claims with insufficient evidence, policy exceptions, authority limits, or infrastructure failures stop for human review with a specific question and an auditable history.

The central design decision separates reading authority from approval authority. Qwen3.8 Flash through OpenRouter and Alibaba extracts facts only. `PolicyDecisionEngine` alone creates `AUTO_APPROVE` or an `ESCALATE_*` status. A submitter confirms the handoff, a manager answers the exception question, the latest manager action can be undone, and the previous events remain visible.

The current candidate is suitable for a supervised demonstration. The Release build is clean, all 163 offline tests pass, and the live Verify Harness completed 5/5 cases in 65.004 seconds. A prospective mini-holdout of ten new real receipts achieved 8/10 exact decisions, zero missed escalations, two fail-safe over-escalations, and no observed system, provider, or HTTP 429 errors. This is a small redacted sample, not a production accuracy claim. Three supervised user sessions remain incomplete.

## 1 Problem and objective

Reimbursement review has two competing risks. An overconfident system may approve an unreadable, unsupported, prohibited, or over-authority claim. A system that sends every claim to a manager does not reduce manual work. AURA focuses on the boundary between these errors. It prioritizes avoiding missed escalations while measuring and disclosing the operational cost of over-escalation.

The Sprint 2 objectives are to:

- execute the workflow from upload through decision and audit;
- distinguish FACT, POLICY, and AUTHORITY uncertainty;
- ask an actionable question when a human decision is required;
- measure missed escalation, over-escalation, field accuracy, latency, and recovery instead of presenting a single favorable output.

## 2 Business scope

AURA accepts one JPG or PNG image up to 5 MB and the reimbursement amount declared by the employee. The current candidate does not support PDF or multipage documents, tax-authority verification, malware scanning, production authentication and RBAC, or managed object storage.

| Status | Main condition | Next accountable role |
|---|---|---|
| `AUTO_APPROVE` | Evidence is sufficient, amount matches, no prohibited item, and authority limit is respected | The demo workflow continues automatically |
| `ESCALATE_FACT` | Evidence is missing or contradictory, date rules fail, or a critical fact is unreliable | Human reviewer checks the original receipt |
| `ESCALATE_POLICY` | A prohibited category or policy exception is present | Manager decides the exception |
| `ESCALATE_AUTHORITY` | Facts are valid but total exceeds VND 1,000,000 | Higher-authority approver |
| `ESCALATE_SYSTEM_ERROR` | Provider, contract, or infrastructure cannot produce a trustworthy result | Operator handles the incident; no business decision is fabricated |

Policy applies the priority `FACT → POLICY → AUTHORITY`. FACT comes first because AURA must not make an automated decision on unreliable evidence. The claimed amount is excluded from the extraction prompt to avoid anchoring; the backend compares it with the extracted printed total afterward.

## 3 Workflow and human authority

```text
Image + claimed amount
  -> validate file and persist a PENDING request
  -> return HTTP 202
  -> SQL-backed worker claims the job with a lease
  -> Qwen extracts structured facts and evidence
  -> semantic validation and at most one repair
  -> deterministic C# policy decision
  -> AUTO_APPROVE or ESCALATE_*
  -> employee confirms the handoff
  -> manager answers Yes or No
  -> Audit and Undo
```

Humans make decisions at two explicit points. The employee chooses whether to forward an exception; AURA does not silently hand it off. The manager answers a concrete question with a meaningful Yes or No outcome. Undo appends a new event rather than deleting the earlier decision.

## 4 Architecture and resilience

| Layer | Implemented component | Boundary |
|---|---|---|
| User interface | ASP.NET Core Razor, asynchronous upload, status polling, reviewer and audit views | No production identity or RBAC |
| Intake | Extension, MIME, magic-byte and size checks; GUID filename and SHA-256 | No antivirus or CDR |
| Durable processing | SQL is the source of truth; lease and reclaim; in-memory channel only wakes the worker | Single-process demo, no distributed queue |
| Vision extraction | OpenRouter/Qwen3.8 Flash routed to Alibaba, reasoning disabled | Depends on Internet access and upstream capacity |
| Validation | JSON Schema, identifier, money, date, and evidence guards; one repair | Never reconstructs a final payable amount from torn subtotal evidence |
| Decision | Deterministic C# policy | The model cannot approve a claim |
| Human workflow | Forward, manager Yes or No, Undo, consolidated timeline | Application-level audit, not compliance-grade WORM storage |

AI concurrency remains one to avoid an upstream burst. Verify has an exclusive operation gate, so four concurrent duplicate clients receive HTTP 409 while one batch runs. Distinct uploads still receive HTTP 202 and wait in SQL. Current coordination is process-local; a multi-instance deployment requires a distributed limiter, cooldown, and circuit state.

## 5 Evaluation method

AURA keeps evidence types separate:

| Evaluation | What it demonstrates | What it does not demonstrate |
|---|---|---|
| 163 offline tests | Policy, contract, queue, fallback, audit, and UI contract correctness | Accuracy on 163 receipts |
| Five-case Verify | Fast Spec A gate with three AUTO and two ESCALATE cases | Production accuracy |
| Prospective ten-receipt holdout | First-run generalization on a small locked sample | Large-scale operational performance |
| Twelve-receipt known-data regression | Stability, extraction variance, and queue behavior | A new holdout or causal A/B result |
| User study | Usability, review time, workload, and negative effects | Model accuracy |

Ground truth, expected status, and SHA-256 are locked before the first request. Raw evidence is never rewritten after output review. Every later run on known data is labelled regression or stability evidence.

## 6 Verified results

| Gate | Result | Safe interpretation |
|---|---:|---|
| Release build | 0 warnings, 0 errors | Current build gate |
| Automated tests | 163/163 | Offline code tests with no paid AI call |
| Live Verify | 5/5 in 65.004 seconds | Meets the five-case sub-90-second gate |
| Prospective mini-holdout | 8/10, zero missed, two over, no system/provider/429 errors | One official run with no result shopping |
| Prospective critical fields | 28/30 | Date, canonical identifier, and final total only |
| Twelve-source-byte regression | 10/12, zero missed, two over, 35/36 fields | Known data, not a new holdout |
| Sequential repeat | 11/12, zero missed, one over, P95 32.903 seconds | Stability evidence |
| Five concurrent uploads | 5/5 completed, E2E P95 93.684 seconds | Queue integrity, not the Verify latency gate |

FAHASA remains a stable over-escalation because the validator does not yet accept `Thanh toán 91.500 đ` as final-payable evidence. GS25_2 varies at the merchant-name region near the privacy blackout. The team did not hard-code a merchant or relax the guard before the final demo. A safe fix requires positive final-payable fixtures and negative tender, cash-given, and change fixtures.

## 7 Product development story

AURA's turning point was not a favorable regression score. The first real-receipt holdout exposed a missed escalation: schema-valid JSON could still contain semantically incorrect facts. The project then moved from “the model can read a receipt” to “every critical fact needs an explicit role and provenance.” Evidence contract v2 separated identifiers, raw dates, printed final totals, and discounts. Semantic validation now blocks contradictions, and unresolved uncertainty stops for a human.

The project therefore does not use one attractive number as its conclusion. Each result is labelled as blind holdout, post-holdout regression, stability repeat, or user study. This is the development story for business judges: AURA matured by discovering its limits, preserving the evidence, and turning each failure into a testable guard.

## 8 User research

The track requires at least three real people who directly handle the workflow, with real identity and role, verbatim feedback, one product change derived from feedback, and one concrete negative effect. This gate is not complete at this evidence version. AURA has prepared a protocol and consent form; this proposal does not invent participants or quotations.

Each planned 20-to-25-minute session uses synthetic data and covers a routine upload, an escalation and handoff, and a manager decision with Audit and Undo. The moderator records task success, review time, assistance, a participant-written or participant-confirmed quotation, confusing steps, and negative effects such as AI wait time, review batching, or automation bias.

## 9 Privacy and responsible use

Real receipts are used only with the owner's permission and after redaction. Raw receipts, raw provider responses, databases, operational logs, and secrets stay outside the public repository. The proposal, slides, and video use synthetic material or aggregated measurements without personal data.

Evidence localization is currently an isolated synthetic prototype. Source image bytes remain unchanged, region JSON is bound to the image SHA-256, and the browser draws the overlay. The feature has no unseen annotation benchmark and does not influence policy, so it must not be presented as a production capability.

## 10 Limitations and roadmap

The candidate is a supervised MVP, not a production reimbursement system. The next priorities are:

1. complete three consented user sessions and trace one resulting change to a commit;
2. reduce over-escalation after the submission freeze using independent positive and negative fixtures without increasing missed escalation;
3. add authentication and RBAC, private object storage, retention, monitoring, a distributed provider limiter, and a safe PDF pipeline before a real pilot.

Evidence overlay should proceed only after word-level OCR boxes, independent annotation, and region-recall and false-highlight benchmarks are available. Ollama 4B remains a supervised local contingency; live fallback stays disabled because of latency and hosting constraints.

## 11 Reproducibility

- Live application: https://bondphupham-001-site1.ltempurl.com/
- Health endpoint: https://bondphupham-001-site1.ltempurl.com/healthz
- Public repository: https://github.com/BondPhuPhamzZ/AURA
- Runbook: `AURA/docs/RUNBOOK.md`
- Business rules: `AURA/BUSINESS_RULES.md`
- Workflow: `AURA/submission/AURA_WORKFLOW_SPEC.md`
- Current status: `AURA/docs/CURRENT_PROJECT_STATUS.md`
- User-study protocol: `AURA/docs/USER_STUDY_PROTOCOL.md`

## Conclusion

AURA demonstrates a responsible approach to reimbursement automation: a model handles variable visual input, deterministic code keeps approval authority, humans decide exceptions, and evidence remains reproducible. The current evidence supports a supervised demonstration and a small controlled pilot. It does not support production accuracy or unattended deployment claims.
