# Canonical date handling for paper receipts

Updated: 2026-10-01
Scope: the shared `ReceiptExtractionContract`, `ReceiptSemanticValidator` and `PolicyDecisionEngine` used by OpenRouter and Ollama.

## Why the original strict repair was revised

The first hardening checkpoint forced every `VAT_INVOICE`, `RETAIL_RECEIPT` and `RESTAURANT_BILL` date into `invoiceDate`. A paper output with only `transactionDate` triggered one model repair and then `ESCALATE_FACT` if the model kept the same field. That caught a genuine field inconsistency, but live postfix evidence showed the rule itself was too strict: both Highlands and Vinamilk exposed a valid printed purchase/transaction date, and Qwen repeatedly represented it as `transactionDate`.

The fail-safe worked, but it created a false escalation without adding evidence. The current contract therefore distinguishes field normalization from evidence invention.

## Current invariant

1. A paper date explicitly labelled as invoice/receipt issue date belongs in `invoiceDate`.
2. A paper receipt whose only relevant printed date is labelled transaction/payment may return `transactionDate`.
3. On parse, if a paper fact has `invoiceDate=null` and one `transactionDate`, the backend moves that existing value to canonical `invoiceDate` and clears `transactionDate`. No AI retry is needed and no date is invented.
4. If the same date appears in both fields, the duplicate `transactionDate` is removed.
5. If both fields contain distinct valid dates, they may coexist. The paper reimbursement age policy evaluates `invoiceDate`; the supporting transaction date is not itself a blocking contradiction.
6. `completionDate` remains supporting evidence only. It is never promoted into either invoice or transaction date.
7. An unreadable/missing date, invalid `YYYY-MM-DD`, future date, over-90-day date or weekend date still produces `ESCALATE_FACT` under the deterministic policy.

## Flow

```text
schema-valid VLM output
        |
        v
lossless canonicalization
  - paper transactionDate -> invoiceDate when invoiceDate is absent
  - remove exact duplicate paper dates
  - never promote completionDate
        |
        v
semantic validation
        |
        +-- consistent --> deterministic policy
        |
        +-- other semantic conflict --> one image re-read
                                      |
                                      +-- resolved --> policy
                                      +-- unresolved --> ValidationIssues --> ESCALATE_FACT
```

`SemanticRepairApplied=false` is expected when canonicalization alone resolves the paper-date field placement. This is preferable to a paid second model call. `SemanticRepairApplied=true` remains valid for genuine semantic conflicts such as malformed money or identifiers, provided final `ValidationIssues` is empty.

## UI alignment

- Paper evidence shows `Ngày chứng từ / giao dịch` and uses `invoiceDate || transactionDate`.
- Digital evidence shows `Ngày giao dịch / thanh toán` and uses `transactionDate || invoiceDate`.
- `completionDate` is displayed separately as reference evidence.
- The UI explains facts only; policy remains server-side.

## Verification

- The historical Highlands pair demonstrated the field-placement variance.
- The later postfix run proved that forcing repair still false-escalated both Highlands and Vinamilk.
- Regression tests now assert lossless normalization in one provider request, preservation of the existing date value, no completion-date promotion and deterministic policy behavior.
- Current baseline: Release build 0 warnings/0 errors and 102/102 automated tests passed.

The live re-validation gate is in `LIVE_POSTFIX_INCIDENT_2026-10-01.md`. Old pre-change runs remain incident evidence and must not be counted as post-fix accuracy.
