# Storage and dataset gates — 23 October 2026

This follow-up hardens the **existing local MVP** without enabling Drive on a public host, deleting receipts, or changing the database model. It is not an authentication, malware-scanning, retention, or production approval.

## Changes

- The configured receipt directory is resolved consistently at startup, intake, worker read, and image retrieval. A path equal to or nested under ASP.NET `wwwroot` now fails at startup; stored filenames containing path separators or `:` are rejected. This prevents an accidental configuration that directly exposes receipt bytes through static files. It does **not** secure anonymous application endpoints or prove filesystem ACL/symlink safety.
- The main dashboard response now uses `Cache-Control: no-store`, like the sensitive status, image, report, and queue endpoints.
- `New-HoldoutImageInventory.ps1` has optional count and SHA-256 uniqueness gates. These detect byte-identical copies **before** a paid inference run. Unique bytes do not prove independent business scenarios, consent, labels, or decision accuracy.

## Verification

| Check | Observed result |
|---|---|
| Debug and Release tests | **231/231 pass in each configuration**, 0 failed, 0 skipped; includes 11 storage-path tests |
| Release build | 0 warnings, 0 errors |
| EF model | No pending model changes |
| Unsafe storage smoke | `ReceiptStorage__Directory=wwwroot/receipts` rejected at startup before SQL/AI |
| Safe local smoke | New isolated `AuraStorageBoundarySmoke_20261023` LocalDB migrated; `/healthz` `ok`, DB up to date, storage available, Drive disabled, AI permit 1; dashboard HTTP 200 and `no-store` |
| 100-file dataset preflight | **FAIL as designed:** 100 files, 70 byte-identical copies; no unique-content inventory written |
| Consented 15-file preflight | 15 files, 15 distinct SHA-256 values; inventory stays under Git-ignored private results; **no accuracy claim** |
| Dependency advisory query | NuGet reported no vulnerable packages for the app or test project from the configured sources at check time |

The no-restore test command still printed `NU1900` from its existing assets because it could not fetch the vulnerability feed in that invocation; the separate NuGet advisory queries completed successfully. The first safe-start attempt under a restricted sandbox could not create a LocalDB instance; the same isolated smoke succeeded with normal local execution. The app was stopped afterward. The new test DB and private test directory were **not** deleted. No receipt was uploaded and no AI provider call was made in this follow-up.

## Remaining gates and next decision

1. Supply or generate a **new** set with 100 distinct receipt contents and scenario labels locked before inference; distinguish synthetic variety from an independent real-world holdout. Do not recycle the 70 copies as accuracy cases.
2. Choose an enterprise identity provider and owner/role model before implementing access to images, status, management decisions and CSV. The current anonymous demo endpoints remain unsafe for general real-user access.
3. Agree on a retention and restore policy for image, SQL facts, audit and backup copies before enabling automatic deletion. The earlier one-week idea is not yet an approved deletion rule; the Drive connector remains read-only.
4. Add malware scanning/quarantine, filesystem/object-store access controls, monitored recovery, and route-specific provider privacy review before any public Drive or production pilot. `data_collection=deny` does not imply Strict ZDR.

**Decision:** GO for further controlled local synthetic testing. NO-GO for public real-user intake or production. No master merge or hosting publish is part of this evidence.
