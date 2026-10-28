# SmartASP live validation — 28 October 2026

**Scope:** public AURA site at `https://bondphupham-001-site1.ltempurl.com/`, single-host configuration, existing SmartASP SQL database, approved synthetic images only. This is a supervised demo check, not a production-readiness or real-data privacy certification.

## Database and backup evidence

- The owner supplied `D:\aura\DB_Backup_28-10\db_acec88_aura_10_27_2026_22.bak` (9,097,216 bytes; SHA-256 `1C4D3E721292A2C26B11E8321706C91084F8CD1568FD66FE54233A14A24E54D9`) and `query-results-1.csv` (six pre-upgrade rows ending at `20260928095906_AddDurableReceiptProcessing`). Both files remain outside Git.
- After the one-time startup migration, the owner reported `/healthz` at 05:49 UTC with `databaseUpToDate=true` and `pendingMigrationCount=0`. After they set the startup-migration flag back to `false`, an independent request at 05:55 UTC again returned HTTP 200, `status=ok`, and zero pending migrations. The endpoint does not expose the flag itself.
- The source contains nine EF migrations. Zero pending migrations supports that the deployed assembly and SQL schema agree; a direct nine-row post-upgrade query and a restore rehearsal were **not** supplied. File existence and a SHA-256 hash do not prove restore viability.

## Live synthetic smoke

| Observation | Result |
| --- | --- |
| Initial R3-01 upload before the owner changed the hosting key | HTTP 202 and a durable case, then `FAILED / ESCALATE_SYSTEM_ERROR / AI_AUTH_ERROR` at 05:57 UTC. In this code, `AI_AUTH_ERROR` maps OpenRouter HTTP 401 or 403; the response does not distinguish them. No automatic approval was recorded. |
| Owner action | Replaced `OpenRouter__ApiKey` in the SmartASP pool and recycled it. No key value was shared or recorded. |
| Health after recycle | At 06:05 UTC, HTTP 200, `status=ok`, `databaseUpToDate=true`, zero pending migrations, `storageAvailable=true`, OpenRouter model `qwen/qwen3.8-27b`, `strictZeroDataRetention=true`, `providerDataCollectionDenied=true`, 49 approved image hashes. |
| R3-01 retry as a new synthetic case | HTTP 202, initially `PENDING`, then `COMPLETED / AUTO_APPROVE` at 06:05:56 UTC; OpenRouter served the result, no provider error, reported processing latency 4,307 ms. |
| Extracted facts | VAT invoice; merchant `CÔNG TY TNHH VĂN PHÒNG NAM VIỆT`; invoice number `000142`; date `2026-10-05`; printed final total **241,920 VND**; two line items. These match the locked R3-01 fixture for the checked fields. `semanticRepairApplied=false` for this case. |
| Persisted UI and image | Employee, reviewer, audit and October dashboard fragments each returned HTTP 200 without `data-fragment-error=true`. The audit row showed `AI_PROCESSED_AUTO_APPROVE` and two timeline events. The synthetic image was readable with HTTP 200, `image/jpeg`, 103,509 bytes. The case remained `COMPLETED / AUTO_APPROVE` on a later status read. |
| Private-path probe | Requests to `/App_Data/approved-synthetic-sha256.json` and `/App_Data/receipts/` returned HTTP 404. This checks only these paths, not every file-access route. |

The successful case establishes a working **live synthetic path** from web upload through SQL queue, image gate, OpenRouter extraction, policy decision, audit and stored image. It does not establish statistical accuracy, throughput, a complete manager/Undo workflow on this deployment, backup recovery, or safe real-receipt processing. The earlier failed case remains in SQL as failure evidence; no records were deleted.

## Public-demo decision and open gates

The live URL may be listed as a **time-limited supervised synthetic demo**, with the SmartASP trial expiration of **20 November 2026** and local setup instructions. Do not describe it as production ready or invite unrestricted real-receipt uploads. `ApprovedImagesOnly` allows only exact approved image hashes to reach external inference, but an unapproved upload is still accepted and stored before that gate. The application has no user authentication or role-based access control.

`strictZeroDataRetention=true` and `providerDataCollectionDenied=true` report request-routing configuration; they are not independent proof of an upstream retention guarantee. `driveSandboxConfigured=false` means the public site does **not** ingest the Restricted Drive folder. Retention cleanup is off, storage is local to the host, and the SQL distributed AI gate is off; this profile is intended for one process and has no multi-instance evidence. Before real-data pilot or multi-host scaling, verify consent/access controls, pre-inference handling, provider terms, restore, retention, shared storage and distributed admission separately.
