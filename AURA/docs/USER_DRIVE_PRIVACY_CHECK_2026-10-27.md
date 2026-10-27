# User Drive and privacy check — 27 October 2026

This note records the owner's local manual check of the current synthetic profile. The six source files remain in the owner's private evidence folder; screenshots and real-receipt content are not copied to Git. This is observational evidence, not a fresh automated run or a hosting smoke test.

## What the supplied evidence shows

The three JPEGs supplied with the check match the locked `test_kit/v3_1/images` fixture bytes by SHA-256:

| Fixture | Locked expectation | Observed in supplied screenshots | Limit |
| --- | --- | --- | --- |
| R3-01 | `AUTO_APPROVE` | A new Drive copy named `(1)` appears as `AUTO_APPROVE`, marked as duplicate content; UI shows 5,135 ms processing latency. | This is a new Google file ID; the synthetic profile does not escalate duplicates. The screenshot does not measure Drive wait time. |
| R3-06 | `ESCALATE_FACT` | Audit shows contradictory extracted total evidence; the manager subsequently accepted manual review. | The screenshot captures the final human action, not every prior queue/model state. |
| R3-13 | `ESCALATE_AUTHORITY` | Audit shows a 1,050,000 VND item above the 1,000,000 VND auto limit; the manager subsequently returned the case. | The final manager choice is a human decision, not model approval. |

The web screenshot shows three real receipt uploads becoming `ESCALATE_PRIVACY` with the message that their exact image bytes were not approved for external inference (displayed processing times 144–159 ms). `ExternalImagePrivacyGate` checks SHA-256 before remote HTTP. These receipts therefore did not go through the model in this profile; the web may still retain local receipt and SQL/audit records. The screenshot alone is not an outbound traffic trace, a PII detector evaluation or a retention audit.

The owner reported noticeable Drive latency. The default Drive poll is 120 seconds; SQL worker polling and visible employee-table polling are 5 seconds each, and model inference has one AI permit. Provider failure can invoke a 120-second cooldown. The supplied screenshots do not include upload, discovery, queue-start and completion timestamps together, so **end-to-end Drive latency and P50/P95 are not measured here**. Do not compare the 5,135 ms processing cell to upload-to-result time or claim an SLA.

## Readiness decision

This is further positive evidence for a **single-host, supervised, synthetic local MVP**: Drive intake, SQL work, policy routing, human actions and visible results are exercised. It does not establish production readiness, safe real-data egress, public Drive operation or SmartASP behavior. Duplicate-expense prevention is not demonstrated by R3-01 because duplicate escalation is disabled in this regression profile.

The safe next step is a separate **synthetic-only SmartASP staging** rehearsal for the web intake, with a new DB/private receipt directory, controlled migration, backup/restore, private exact-image allowlist, and hosting secrets. Keep `DriveSandbox__Enabled=false` in Production. Test one approved synthetic upload, blocked real/unknown upload, audit/Undo and recycle before considering a public release. The local Restricted Drive connector remains Development/loopback only.

For real receipts, the present hash approval is a manual egress permission boundary, not redaction. Before a scoped pilot, define who may submit/review/see data, minimize or redact sensitive pixels before egress where needed, validate model/provider endpoint terms and ZDR routing in the target account, review consent/retention/backup and exercise security, malware and recovery controls. Do not add real-image hashes to the synthetic allowlist or switch to `LegacyConfigured` to bypass this decision.

## Strict ZDR mechanics

`OpenRouter__RequireZeroDataRetention=true` makes AURA send `provider.zdr=true` in each OpenRouter request; `OpenRouter__DenyProviderDataCollection=true` makes it send `provider.data_collection=deny`. OpenRouter routes a `zdr=true` request only to endpoints listed as ZDR eligible; it does not change AURA's local copies or remove information from an approved image. AURA sends the system prompt and entire approved image as a base64 data URL. `/healthz` reports configured flags, not a provider contract, the actual per-request endpoint, or proof that no operational metadata was retained. See the [OpenRouter route check](OPENROUTER_ZDR_ROUTE_CHECK_2026-10-24.md) and [deployment gate](DEPLOYMENT.md).
