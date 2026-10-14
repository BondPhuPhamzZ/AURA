# AURA final submission links

## Project

- Project: AURA Automated Underwriting and Reimbursement AI
- Team: The Deciders
- Track: OrganizationAI
- Challenge: Spec A The Escalation Referee
- Author: Pham Gia Phu

## Access

- Live application: https://bondphupham-001-site1.ltempurl.com/
- Health endpoint: https://bondphupham-001-site1.ltempurl.com/healthz
- Public repository: https://github.com/BondPhuPhamzZ/AURA
- Frozen release ref: `aura-demo-day-2026-10-14` (tag is created from the final synchronized commit and pushed with the repository)
- Official video: add the Team Drive link after recording and upload

## First action for judges

Open the live application, select **Chạy Verify Harness**, and wait for the table to complete three routine approvals and two escalations. Then open one escalated request, select **Chuyển tiếp**, answer it in **Quản lý**, and inspect **Lịch sử hành vi** and **Undo**.

## Current evidence boundary

- 163/163 refers to offline automated tests, not 163 receipts.
- Live Verify completed 5/5 in 65.004 seconds.
- The prospective ten-receipt mini-holdout completed 8/10 with zero missed escalations, two over-escalations, and no observed system, provider, or HTTP 429 errors.
- The 11/12 sequential result is a stability repeat on known data, not a new holdout.
- The candidate is a supervised MVP and does not support production accuracy or unattended rollout claims.

## Data and secrets

Do not place API keys, user secrets, raw personal receipts, provider billing screenshots, private logs, or production connection strings in this Drive package. The evaluator key, if required, must be shared separately and revoked after evaluation.
