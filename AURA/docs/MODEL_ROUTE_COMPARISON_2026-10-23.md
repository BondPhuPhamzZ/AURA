# Local model-route and decision-gate check — 23 October 2026

This is a regression on previously known synthetic images, not a blind accuracy benchmark or a production approval. All requests used the localhost app, the existing SQL queue, OpenRouter with `data_collection=deny`, one AI permit, and the same schema/policy. The alternate model was selected only through an environment override in the test process; the repository default, user secrets, public host and Drive connector were not changed. Detailed case outputs remain in Git-ignored `test_kit/results/`.

## What the diagnostic established

On the configured `qwen/qwen3.8-flash` route, a sampled failure was HTTP 200 with a two-character non-object completion, `finish_reason=stop`, two completion tokens, zero reasoning tokens, and a configured 4,096-token output cap. Sampled routing metadata identified Alibaba. This is not output-cap exhaustion; increasing the cap is not an evidence-based remedy. OpenRouter's [chat API](https://openrouter.ai/docs/api/api-reference/chat/create-a-chat-completion) documents token usage and opt-in routing metadata. Its [response-healing description](https://openrouter.ai/blog/announcements/response-healing-reduce-json-defects-by-80percent/) says it repairs JSON syntax, not schema adherence. Neither source identifies the exact cause of this response.

The adapter now records only bounded token counts, an allowlisted provider kind, schema reason/field, content length and finish reason. Arbitrary provider error text and invalid request-ID headers no longer enter log or user messages. The parser rejects unapproved model fields, including DTO-only audit/privacy fields, and case-insensitive duplicate keys; unknown field names are not echoed in diagnostics.
Provider HTTP response bodies are capped at 2 MiB both by declared `Content-Length` and by actual bytes read, so an oversized or misleading response fails safely without being fully buffered.

## Same-set synthetic results

| Candidate/run | Decision matches | Completed | System errors | Semantic Repair | End-to-end P50 / P95 |
|---|---:|---:|---:|---:|---:|
| Qwen3.8 Flash, five approval cases | 4/5 | 4/5 | 1 | 0 | 5.170 / 11.249 s |
| Qwen3.6 Flash, same five, run 1 | 5/5 | 5/5 | 0 | 0 | 11.749 / 13.258 s |
| Qwen3.6 Flash, same five, run 2 | 5/5 | 5/5 | 0 | 0 | 11.182 / 18.814 s |
| Qwen3.6 Flash, fifteen cases **before** item-description gate | 14/15 | 15/15 | 0 | 0 | 11.700 / 12.751 s |
| Qwen3.6 Flash, fifteen cases **after** gate | 15/15 | 15/15 | 0 | 1 | 10.709 / 16.765 s |
| Qwen3.8 Flash, same fifteen and gate | 13/15 | 14/15 | 1 | 0 | 5.606 / 10.237 s |

The fifteen-case manifest contains five expected approvals, four fact escalations, three policy escalations and three authority escalations. On the final same-code comparison, Qwen3.8 Flash had one `AI_SCHEMA_MISMATCH` and one over-escalation; Qwen3.6 Flash had no mismatch. The latter's higher latency is descriptive only; neither run establishes a service-level objective or causal model superiority. The final redaction-only change to decision wording was unit-tested after these runs and does not change routing priority.

## Business-rule and security finding

The pre-gate Qwen3.6 Flash run exposed one **missed escalation**: TK-12 was auto-approved although its printed final-total number is blurred. The model supplied a plausible final-total evidence string by reusing the visible line-item amount; Semantic validation cannot verify the underlying pixels from model text alone. The item description is also explicitly non-specific. A deterministic policy gate now sends such generic descriptions to `ESCALATE_FACT`; TK-12 then escalated on the next run. This closes that separate policy loophole, **not** the general blurred-total hallucination problem. All four synthetic FACT cases use the same generic item description, so this manifest has limited diversity for proving that gate.

The policy reason and manager question no longer repeat arbitrary warning text, document/order status, unsupported currency or prohibited line-item descriptions. They retain a static signal/category or count, leaving the underlying receipt for a protected human review. The decision priority remains FACT → PRIVACY → POLICY → AUTHORITY → AUTO.

## Verification and decision

Release and Debug suites: **273/273 passed** each after the response-size guard, worker-race, SQL-gate, stored-file-integrity, synthetic restore and queue-monitor tests (the original comparison gate was 262/262). Isolated opt-in LocalDB worker tests: **8/8**. Release build: **0 warnings/errors**; EF: **no pending model changes**. `NU1900` during `dotnet test` reflects an unreachable NuGet advisory feed and is not a fresh vulnerability audit. `git diff --check` passed. The local test process was stopped after each batch.

**Keep NO-GO for public publish, model switch, multi-worker scale and production claims.** Qwen3.6 Flash is a candidate for the next consented evaluation, not the new default. Next gates: a fresh independently labeled synthetic set that separates blurred-total, generic-item and prohibited-item cases; explicit owner authorization before sending real receipts through an alternate model route; repeatability and critical-field comparison on that set; route-specific privacy/ZDR verification; then the existing storage, distributed coordination, authorization, malware, retention and restore/load gates. The current route still reports `strictZeroDataRetention=false`.
