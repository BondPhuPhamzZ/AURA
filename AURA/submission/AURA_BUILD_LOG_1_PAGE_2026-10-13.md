# AURA Build Log Sprint 2

**Track A The Escalation Referee | 13/10/2026 | Pham Gia Phu**

AURA is a supervised reimbursement-review assistant. A vision model extracts structured receipt facts, deterministic C# policy decides the route, and people remain responsible for exceptions. The system covers upload, durable background processing, review, Audit, and Undo.

## How I used AI

Qwen3.8 Flash through OpenRouter and Alibaba, with reasoning disabled, reads one JPG or PNG and returns a strict JSON contract. Codex helped trace code paths, design tests, review refactors, and synchronize documentation. I accepted changes only after build, tests, and evidence review. The model never receives approval authority.

## Where AI helped

- Fast code navigation exposed the difference between an in-memory signal and the durable SQL job queue.
- Generated test cases expanded coverage for field roles, arithmetic, date evidence, fallback, concurrency, Audit, and UI contracts.
- Vision extraction handled varied receipt layouts while policy remained replaceable and testable.

## Where AI cost time or money

Provider names and model slugs changed, and earlier routes produced 404 or intermittent 429 errors. Schema-valid outputs also contained semantic errors such as the wrong money unit or identifier role. A reasoning-enabled request used its completion budget without returning usable JSON. These failures required provider diagnostics, a strict schema, semantic validation, a single repair attempt, cooldown, and explicit claim boundaries. Paid API reruns were avoided once data became known.

## What changed after failure

The first real-receipt holdout exposed a missed escalation even though the response parsed. I replaced “valid JSON is enough” with evidence contract v2: identifiers have distinct roles, dates preserve raw evidence, discounts are explicit, and an amount can auto-approve only when a printed final-total line corroborates it. Unresolved uncertainty becomes FACT review. The policy priority remains `FACT → POLICY → AUTHORITY`.

## Verified evidence

| Gate | Current result |
|---|---:|
| Release build | 0 warnings, 0 errors |
| Offline tests | 163/163 passed |
| Live Verify | 5/5 in 65.004 seconds |
| Prospective mini-holdout | 8/10, 0 missed, 2 over, 0 system/provider/429 errors |
| Sequential known-data repeat | 11/12, 0 missed, 1 over; stability evidence only |

The prospective sample is small and redacted. It supports a supervised demo, not production accuracy. Three real-user sessions remain incomplete.

## Largest feature cut

I cut production evidence localization from the final baseline. Highlighting where a fact appears could improve reviewer confidence, but without word-level OCR, independent annotations, and a false-highlight benchmark it could point to the wrong amount and create false confidence. A synthetic hash-bound SVG prototype remains isolated from extraction and policy. PDF, production RBAC, malware scanning, object storage, and distributed rate limiting also remain roadmap items.

## Next gate

Complete three consented user sessions, preserve verbatim feedback, trace one change to a commit, and report one negative operational impact. Then reduce over-escalation with independent positive and negative fixtures without weakening the missed-escalation guard.
