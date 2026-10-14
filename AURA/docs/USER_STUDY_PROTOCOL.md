# AURA user study protocol

## Purpose and completion rule

This protocol closes the OrganizationAI Sprint 2 user-evidence gate without fabricating participants or feedback. A completed package must contain three people who directly handle reimbursement submission, accounting review, or manager approval in real work. Each record needs the participant's real name and job title, written consent, verbatim feedback confirmed by that participant, one product change traced to a commit or before and after evidence, and at least one concrete negative effect.

The project owner does not count as all three participants. Synthetic personas, paraphrased quotations presented as verbatim, or retrospective signatures on text the participant did not review are not acceptable.

## Participant criteria

Recruit at least three people across the real workflow where possible:

1. a submitter who prepares reimbursement claims;
2. an accounting or administrative reviewer who checks receipts;
3. a manager or budget owner who decides exceptions.

If two participants share the same role, document why the sample still represents meaningful operational viewpoints. Do not recruit someone whose only exposure is watching the pitch.

## Data and privacy

- Use synthetic receipts and synthetic employee names during the session.
- Do not record API keys, billing dashboards, user secrets, real databases, or private provider logs.
- Ask permission separately for screen/audio recording and for publishing the participant's name, role, and quotation.
- Store signed forms in the private Team Drive. Commit only the blank protocol/template and an anonymized aggregate if consent does not permit publication.
- Stop the session if the participant reveals third-party personal or organizational data.

## Session plan for each participant

Target duration: 20 to 25 minutes.

| Phase | Time | Participant task | Moderator evidence |
|---|---:|---|---|
| Introduction and consent | 3 min | Read purpose, data handling, and voluntary participation statement | Signed consent and recording choice |
| Routine case | 4 min | Upload a clear synthetic receipt, enter the claim, locate the result | Task success, time, hesitation, help needed |
| Exception case | 5 min | Inspect an `ESCALATE_*` result and explain the question in their own words | Comprehension, evidence used, missing context |
| Human workflow | 5 min | Forward the case, answer Yes or No as manager, inspect Audit, then Undo | Outcome correctness and audit comprehension |
| Open feedback | 5 min | Identify the most useful part, the most confusing part, and one negative effect | Participant-written or confirmed verbatim quote |
| Closing | 3 min | Rank change requests and confirm what may be published | Priority and publication consent |

Do not coach through the interface unless the participant is blocked. Record the block before giving help.

## Required measures

For every task, record:

- completion success without help, with help, or not completed;
- start and finish time;
- number of wrong clicks or navigation reversals;
- whether the participant understood who made the decision: model, policy, submitter, or manager;
- whether the escalation question was actionable without reopening the source receipt;
- confidence score from 1 to 5 and the reason for that score.

For the complete session, record one positive observation and one negative impact. Valid negative impacts include AI wait time, review work arriving in bursts, a new confirmation step, automation bias, reduced direct communication, or additional training burden. “No negative impact” does not satisfy the track evidence requirement.

## Verbatim feedback rule

The participant must write the quotation themselves or review the exact sentence and mark it confirmed. Keep Vietnamese wording as given, including informal language. A translated English version may be added beneath it but must be labeled as a translation.

Use this statement before confirmation:

> I confirm that the quotation below reflects my own feedback after using AURA in this session. I understand where it will be shared and may withdraw permission before the submission deadline.

## Turning feedback into one traceable change

After all three sessions:

1. group observations by workflow step;
2. select one change supported by at least one direct observation and safe before freeze;
3. write an acceptance criterion before implementation;
4. preserve before evidence;
5. implement and test without altering ground truth or model-evaluation data;
6. record the commit SHA and after evidence;
7. ask the originating participant to review the change when practical.

Do not claim that the existing auto-scroll or evidence-overlay prototype came from these sessions unless a dated participant record actually supports that causal statement.

## Private evidence package

```text
USER_STUDY_EVIDENCE/
  00_PROTOCOL_AND_CONSENT/
  01_PARTICIPANT_01/
  02_PARTICIPANT_02/
  03_PARTICIPANT_03/
  04_AGGREGATE_METRICS/
  05_FEEDBACK_TO_CHANGE/
    before/
    after/
    commit.txt
  06_NEGATIVE_IMPACT/
  07_INTEGRITY/
    manifest.sha256
```

Each participant folder should contain the signed form, task timing sheet, confirmed quotation, observation notes, and optional consented recording. Keep recordings private unless the participant explicitly permits wider sharing.

## Aggregate report

The public report should state the sample size and roles, not present the sample as statistically representative. Report median task time only if all three sessions measured the same task. Include all failures, assistance, and negative feedback. Distinguish observed behavior from the moderator's interpretation.

## Final completion checklist

- [ ] Three eligible participants with real identities and roles.
- [ ] Written consent and publication scope for each participant.
- [ ] Same core task script and synthetic data for all sessions.
- [ ] Task success, timing, help, comprehension, and confidence recorded.
- [ ] One confirmed verbatim quote from each participant.
- [ ] At least one concrete negative effect documented.
- [ ] One change traced from feedback to acceptance criterion, commit, and before/after evidence.
- [ ] Private evidence manifest generated.
- [ ] Public proposal, Slide 3, Slide 5, Build Log, README, and pitch synchronized only after the evidence exists.
