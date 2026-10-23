# OpenRouter Qwen ZDR route check — 2026-10-24

## Method

Queried the public OpenRouter model-endpoint metadata and ZDR endpoint list on
2026-10-24. Compared image-capable Qwen model IDs with their provider endpoints
and with the current ZDR endpoint list. No API key, prompt, or receipt was sent
for this metadata check. OpenRouter documents `provider.zdr=true` as a request
constraint that routes only to ZDR-eligible endpoints, and documents the
endpoint list as the live source of endpoint-level ZDR status:

- [ZDR routing and endpoint policies](https://openrouter.ai/blog/insights/zero-data-retention/)
- [Qwen3.8 Flash endpoint metadata](https://openrouter.ai/api/v1/models/qwen/qwen3.8-flash/endpoints)
- [OpenRouter ZDR endpoint list](https://openrouter.ai/api/v1/endpoints/zdr)
- [OpenRouter model list API and `zdr=true` filter](https://openrouter.ai/docs/api/api-reference/models/get-models)

## Findings at check time

- `qwen/qwen3.8-flash` exposed two Alibaba endpoints; neither appeared in the
  ZDR endpoint list. Therefore the configured Flash model had no listed ZDR
  endpoint at this check time.
- The ZDR-filtered image-capable Qwen model catalog included models such as
  `qwen/qwen3.8-27b`, `qwen/qwen3.6-35b-a3b`, `qwen/qwen3.6-27b`,
  `qwen/qwen3.5-9b`, and `qwen/qwen3-vl-8b-instruct`.
- Cross-checking those Qwen model endpoint lists found Alibaba-hosted endpoints
  for some models, but **zero Alibaba-hosted endpoints in the ZDR list** among
  the image-capable Qwen candidates returned by the catalog query.
- `qwen/qwen3.8-27b` did have ZDR-listed endpoints, including DekaLLM. “Qwen
  model” identifies the model family/weights; it does not mean Alibaba operates
  the inference server.

## Controlled request test

The same synthetic `TC-01` was sent twice through the Release local app using
`qwen/qwen3.8-27b`, `RequireZeroDataRetention=true`, and deliberately
`DenyProviderDataCollection=false`. Both responses were HTTP 200 and AURA
reported the effective request policy as `providerDataCollectionDenied=true`,
`strictZeroDataRetention=true`. Both completed without fallback/provider error
and returned the expected `AUTO_APPROVE`, with printed and claimed totals both
295,199 VND; semantic repair was not needed. Latencies were 7,685 ms and 7,631
ms; provider-reported costs were USD 0.0022541 and USD 0.00118932. The first
run's older telemetry did not capture the upstream name (`Other`); after
provider-name logging was added, the repeat identified `DekaLLM`. The temporary
LocalDB and uploaded synthetic image were deleted after each run.

This verifies that the current AURA request can enforce the ZDR routing flag and
that one eligible Qwen model route worked. It does not establish legal
compliance, make the request local, prove every endpoint/vendor term, or prove
accuracy/repeatability. The serving infrastructure observed was DekaLLM, not
Alibaba. Do not describe this as Alibaba-hosted ZDR. Provider endpoint inventory
is dynamic and must be rechecked before deployment or real-data use.

The source configuration/model was not changed by this experiment. Existing
production/demo settings are not implicitly switched to `qwen/qwen3.8-27b`.
