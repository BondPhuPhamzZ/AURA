# One local synthetic profile — 26 October 2026

**Drive progress update:** The visible employee tab now polls its SQL-backed
table every five seconds. Recent unforwarded Drive requests show PENDING,
PROCESSING and the final decision without F5; Audit remains the permanent
record. It adds at most 20 recent Drive requests from the last 24 hours,
while all unforwarded escalations remain visible regardless of age.
An unchanged Drive file ID/checksum is silently skipped on later scans. A
new copy can create a new request and carry a duplicate-content marker;
duplicate escalation remains off in this profile. Refreshing the browser does
not trigger a scan. The connector polls every 120 seconds by default, then
the single AI permit may add queue time. No SmartASP Drive enablement follows
from this local UX change.

The `AURA` Visual Studio launch profile and `tools/Start-AuraRetentionSandbox.ps1` now use one prepared profile at `127.0.0.1:5000`. Preparation writes `.tmp/retention-sandbox/active.json` and a verified 49-hash image approval list; both are Git/publish ignored. The launcher reuses the database and Drive setting in that active profile on subsequent runs. This avoids accidentally changing to a new dated database on the next day.

## Prepare once

1. Keep the separate Google Drive test folder **Restricted** and shared as **Viewer** only to the service account. Put only the locked synthetic JPGs directly in the configured folder. No public folder link is needed. The folder ID and absolute service-account JSON path remain in .NET User Secrets; the JSON key stays outside the repository. The OpenRouter API key also remains in User Secrets.
2. Close other AURA local processes, especially the former `5098` host. Start LocalDB with the same Windows user that owns it.
3. In `D:\aura\AURA\AURA`, run:

```powershell
.\tools\Start-AuraRetentionSandbox.ps1 -PrepareOnly -EnableSyntheticDrive
```

The script verifies committed fixture bytes, builds Release, migrates **only** `AURA_RetentionSandbox_yyyyMMdd`, then writes the active profile. It does not edit User Secrets or migrate the contest `AuraDb`. To turn Drive off on a later preparation, pass `-EnableSyntheticDrive:$false` explicitly.

## Run one host

Choose either Visual Studio **AURA** → **Run/F5**, or the PowerShell command below. Do not run both at once; the second cannot bind port 5000. Both use the same sandbox SQL database, private receipt directory, approval hashes, model, retention policy and Drive option:

```powershell
.\tools\Start-AuraRetentionSandbox.ps1
Invoke-RestMethod http://127.0.0.1:5000/healthz
```

The launcher rebuilds and checks migrations before starting; F5 uses the prepared profile and does not apply migrations. If the profile is missing, F5 stops at startup instead of falling back to `AuraDb`. Check `status=ok`, `pendingMigrationCount=0`, `driveSandboxConfigured=true`, `externalImagePrivacyMode=ApprovedImagesOnly`, `externallyApprovedImageCount=49`, `strictZeroDataRetention=true`, `providerDataCollectionDenied=true`, `maxConcurrentAiRequests=1`. Health proves configuration and local dependencies; it does not verify credit, model quality or provider terms.

## Two manual synthetic cases

The earlier `R3-01` and `R3-12` files already produced records in this database. Uploading identical Drive files again is idempotent and will not create new actions. For a fresh pair, upload `test_kit/v3_1/images/R3-02-restaurant-ptt.jpg` and `test_kit/v3_1/images/R3-13-authority-vat.jpg` directly into the same Restricted folder. Wait for the Drive scan (default every 120 seconds), then for the one-at-a-time AI queue. Keep the employee tab visible; its result table updates automatically, then inspect Audit and compare against the locked `test_kit/v3_1/manifest.json`. Record actual outcomes; do not change labels to match the model. Drive reads files only and does not delete originals.

## Privacy boundary and publication gate

`OpenRouter:RequireZeroDataRetention=true` sends `provider.zdr=true`, while `DenyProviderDataCollection=true` sends `provider.data_collection=deny`. This is a routing request, not a certificate or proof that the provider never retains all operational data. The Qwen3.8-27B model can be served by a provider other than Alibaba; the observed synthetic route was Modal. `ApprovedImagesOnly` checks the SHA-256 of exact synthetic bytes **before** external HTTP and rejects any other image; it does not detect or redact personal information in an approved image. Privacy triage runs after inference. Real receipts therefore remain blocked in this profile. Do not add their hashes or enable `LegacyConfigured` to bypass the gate. A separate consented pilot needs route/terms review, minimization and access control before external inference.

The Drive worker is intentionally Development plus loopback only and has no malware scanner or identity boundary for public users. Do not enable it on SmartASP or publish this generated profile, service-account key, real images or private evidence. The proposal/slides/build log contain dated current-source notes above their historical contest evidence; neither constitutes public Drive readiness.

## Verification actually performed

On 26 October, both entry points separately served `127.0.0.1:5000` using the same isolated database. Each returned `/healthz=ok` with the profile values above; four read-only UI fragments and the dashboard returned HTTP 200. The Drive worker scanned 8 folder entries without new jobs. Read-only SQL aggregates stayed at **5 requests, 10 audit rows, 0 pending/processing**, confirming no duplicate processing of the two prior files on restart. Release build had 0 warnings/errors; .NET tests passed **377/377**; frontend refresh tests passed **8/8** by direct Node execution. No new model inference or real receipt upload was performed for this profile-unification check. The initial restricted-tool LocalDB migration attempt failed because that environment could not open the user-owned instance; the same prepare command succeeded in the user's normal local context with no migrations pending.
