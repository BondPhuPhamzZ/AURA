# Live postfix incident: date, non-impacting discount and durable employee queue

Date: 2026-10-01
Evidence root: `D:\aura\benchmark_2times_img\OpenRouter_test\final_validation_2026-10-01\05_discount_reconciliation_postfix`

## 1. What the evidence proves

The readiness evidence is healthy on commit `29257c3a7cf1ce93c2a38ce987cf9d3f75638038`: the running-app preflight has 0 failures and 0 warnings; `/healthz` reports OpenRouter/Qwen, durable background queue, database up to date, storage available and fallback disabled. `DuplicateDetected=true` is not a decision cause because `DuplicatePolicyEnabled=false`.

The two live receipt decisions exposed two semantic-contract problems:

| Receipt | Case | Latency | Extracted monetary facts | Actual blocking cause |
|---|---|---:|---|---|
| Highlands, claimed 59,000 | `590ba2c5...` | 33,538 ms | `lineItems/subtotal/totalAmount=59000`, `tax=0`, but model proposed `discountAmount=1000` | paper date was placed in `transactionDate`; unrelated `1000` was treated as a discount although it had no distinct before-discount base |
| Vinamilk, claimed 180,286 | `05d27fa9...` | 14,890 ms | `subtotal=183114`, `discountAmount=2828`, `totalAmount=180286` | arithmetic was correct; only the paper date was placed in `transactionDate` |

Therefore the fail-safe worked as designed, but the canonical-date rule was too strict for real paper receipts and the discount prompt lacked negative examples such as loyalty points. This was a false escalation at the semantic-validation layer, not `ESCALATE_POLICY`, duplicate policy, provider outage, database failure or claimed-amount mismatch.

The two saved `statusUrl.txt` files are useful proof of HTTP 202 and durable `PENDING`, but they are not final JSON: both still contain `actual=null`, `processingState=PENDING`, `facts=null`. Final status JSON must be saved again after re-validation.

## 2. Safe remediation

1. For a paper receipt with no `invoiceDate` but one valid `transactionDate`, the backend losslessly promotes that existing value to the canonical paper evidence date and clears the duplicate field. It never promotes `completionDate`.
2. Distinct invoice and transaction dates may coexist. The deterministic paper policy evaluates the invoice date; it no longer escalates merely because both valid date fields exist.
3. Loyalty points, point balances, voucher codes/percentages, cash tendered, change, quantities and terminal/customer identifiers are explicitly excluded from `discountAmount`.
4. A positive discount is deterministically ignored only when three independent monetary views already agree: `sum(lineItems) = subtotal = totalAmount`, tax is zero, and there is no distinct before-discount base. A warning records the removed unsupported value. A real discount such as Vinamilk remains structured and must reconcile `subtotal + tax - discountAmount = totalAmount`.
5. The employee result table now merges the current run with the DB-backed escalation queue and refreshes that queue every five seconds and when the tab becomes visible. Restart-completed escalations no longer depend on `sessionStorage` or a later test run to appear.
6. An ARIA live progress notice now explains queue acceptance, background processing, elapsed wait time, completion and failure. Skeleton rows remain a secondary visual cue.

The remediation does not add a database column or migration. Receipt facts remain in the existing JSON payload.

## 3. Verification performed

- Release build: 0 warnings, 0 errors.
- Automated tests: 102/102 passed.
- JavaScript syntax check on the rendered Razor script surrogate: passed.
- New regressions cover paper transaction-date normalization, the Highlands-style non-impacting `1000`, and preservation of the Vinamilk `2828` discount.
- A full local UI run from the automation environment was blocked by the known per-user LocalDB automatic-instance/registry condition. This is an environment-context limitation; the user's recorded running-app preflight remains the authoritative DB evidence. The user must run the final live re-validation in the normal Windows account.

## 4. Required post-fix live gate

Create a new evidence folder under the same validation root; do not mix these runs with commit `29257c3`:

1. Save `git rev-parse HEAD`, full running-app preflight and `/healthz`.
2. Highlands: run three fresh uploads at 59,000 VND. Save UI, audit and final status JSON. Expected: `AUTO_APPROVE`, canonical evidence date present, `discountAmount=null`, no `ValidationIssues`; a normalization warning may state that an unsupported non-impacting discount value was ignored.
3. Vinamilk: run three fresh uploads at 180,286 VND. Expected: `AUTO_APPROVE`, `subtotal=183114`, `discountAmount=2828`, `totalAmount=180286`, no `ValidationIssues`.
4. Negative Vinamilk: claim 183,114 VND once. Expected: `ESCALATE_FACT` because the claim does not equal the final payable amount.
5. Verify Harness: run three batches. Expected official 5/5 decisions in every batch.
6. Restart queue: upload a receipt that intentionally escalates, wait until status is final but do not forward it, close the browser and stop the app, restart, open the employee tab and wait at most ten seconds. The old row must appear without running another upload or Verify batch.
7. For every asynchronous upload, save both the initial 202/PENDING JSON and a second status request after completion. Only the latter counts as final facts/decision evidence.
