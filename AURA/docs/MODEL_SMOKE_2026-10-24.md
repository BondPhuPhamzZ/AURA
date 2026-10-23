# Controlled model smoke test — 2026-10-24

## Scope

One synthetic fixture (`TC-01`, `HoaDon1.jpg`) was sent through the normal local
manual-upload endpoint and durable background worker. The application used a
new, isolated LocalDB database (`AuraSyntheticDiagnostic_20261024`) and listened
only on `127.0.0.1:5187`. Drive sandbox and Verify Harness were disabled. The
temporary database and the uploaded synthetic receipt were deleted after the
test. No real receipt or user database was used.

Fixture SHA-256: `9A5E8199DC16768D6C51E167F27872A2E52312B043637689CE6FE43EADEB838F`.

## Result

| Check | Observed result |
| --- | --- |
| Manual intake | HTTP 202; one case queued |
| Queue/worker | `COMPLETED` |
| Provider | OpenRouter returned HTTP 200; primary and served provider were OpenRouter; no fallback and no provider error |
| Model | `qwen/qwen3.8-flash` |
| Processing latency | 6,659 ms |
| Model usage | 6,063 prompt tokens; 505 completion tokens; 6,568 total; provider-reported cost USD 0.0011468 |
| Expected / actual business status | `AUTO_APPROVE` / `AUTO_APPROVE` |
| Amount check | Claimed amount and printed final total both 295,199 VND; evidence source was `PRINTED_FINAL_TOTAL` |
| Semantic validation | No validation issues; `semanticRepairApplied=false` (no repair was needed) |
| Privacy triage | `privacyRisk=NONE`, no category raised; model output contained an e-commerce shipping tracking identifier |
| Local health | `status=ok`, database available and up to date, zero pending migrations, storage available |

The result confirms one successful inference and one matching policy decision. It
does not establish model accuracy, repeatability, confidence calibration, or
production readiness. The returned confidence was `1`; confidence is model
output, not an independently calibrated probability. The observed tracking
identifier with `privacyRisk=NONE` also demonstrates that the current
post-inference triage is not a complete detector for linkable identifiers. It
does not prove the synthetic identifier identifies a person.

## Privacy and scale boundaries observed

At test time `/healthz` reported:

- `providerDataCollectionDenied=true`
- `strictZeroDataRetention=false`
- `sqlDistributedAiGateEnabled=false`
- `maxConcurrentAiRequests=1`
- `processingMode=durable-background-queue`

The request therefore used the configured data-collection-denial control, but
the application did not request ZDR. A post-inference privacy classifier cannot
prevent the raw image from reaching the external model provider. Do not treat
this smoke test as authorization or evidence for sending user/employee receipts.
The SQL distributed AI gate remains disabled, so the one-permit setting is only
per process; multi-instance use is not established.

## Test-client note

The first scripted upload was rejected before intake because PowerShell's form
helper sent the JPG with a MIME type that did not match `image/jpeg`. Repeating
the same synthetic case with the correct multipart MIME succeeded. This was a
test-client construction error, not a product or provider failure.

The Release test suite passed 300/300 with the SQL LocalDB integration cases
enabled. `dotnet ef migrations has-pending-model-changes --no-build` reported no
model changes. These automated tests are separate from the single live-provider
smoke above.

## Separate Strict-ZDR route smoke

A separate Strict-ZDR smoke was repeated twice with `qwen/qwen3.8-27b` and
`RequireZeroDataRetention=true` while the independent
`DenyProviderDataCollection` setting was deliberately false. The request
completed `AUTO_APPROVE` on the expected amount in both runs, with no provider
error/fallback; `/healthz` reported effective data-collection denial and Strict
ZDR enabled. Latencies were 7,685 ms and 7,631 ms; provider-reported costs were
USD 0.0022541 and USD 0.00118932. The first run's old telemetry reported only
provider kind `Other`; after adding safe endpoint telemetry, the repeated run
identified DekaLLM. This is a Qwen model served by a non-Alibaba inference
provider. Two same-fixture outputs agreeing on the decision do not constitute a
benchmark or production model switch. See
[the live route metadata and ZDR evidence](OPENROUTER_ZDR_ROUTE_CHECK_2026-10-24.md).
