# Shared private receipt storage

## Status

AURA now has a storage boundary for local files and an opt-in Google Cloud Storage (GCS) adapter.
The default remains `LocalFileSystem`; no cloud bucket, credentials, hosting settings, or receipt
data are changed by this code. The GCS adapter is not production-validated until a private bucket
is provisioned and an authenticated integration test is run in the target environment.

Images are stored with generated request IDs and extensions, not user-supplied object names. The
GCS adapter uses create-only generation preconditions; an existing key is reused only when its
size and SHA-256 match. On reads, AURA checks object size, downloads a pinned generation, then
checks the stored size and SHA-256 against SQL before sending bytes to the existing extraction,
semantic validation, privacy triage, and deterministic policy pipeline. The application serves
image bytes through its controller; it does not expose bucket URLs or generate signed URLs.
Both LocalFileSystem and GCS adapters also verify that newly supplied bytes match the caller's
declared SHA-256 before writing; malformed or mismatched hashes are rejected before storage I/O.

For the local filesystem provider, a background sweeper removes only AURA-generated staging
`.tmp` files older than the configured `ReceiptStorage__StaleTempFileAgeHours` (default 24 hours),
and only when it can open them exclusively. It does not delete completed receipt files. This cleanup
is disabled for GCS and is unrelated to receipt retention; no receipt-object lifecycle deletion is
implemented by the application.

## GCS configuration (opt-in only)

Create a dedicated private bucket, enable [uniform bucket-level access](https://cloud.google.com/storage/docs/uniform-bucket-level-access) and [public access prevention](https://cloud.google.com/storage/docs/public-access-prevention),
and give a dedicated runtime service account only the bucket-scoped `Storage Object User`
([`roles/storage.objectUser`](https://cloud.google.com/storage/docs/access-control/iam-roles)) role. Do not reuse the Drive read-only service account. Keep its JSON
credential outside the repository and deployment package if a key is necessary. The adapter now
supports both explicit JSON credentials and Application Default Credentials (ADC). On Google Cloud
compute, prefer ADC with an attached workload service account so no private key is distributed. The
.NET Storage client supports ADC through its normal credential discovery flow; see [Google's ADC
overview](https://cloud.google.com/docs/authentication/application-default-credentials).

For local testing, prefer service-account impersonation rather than a downloaded key or a broad
personal user credential. Google documents this C#-supported setup as
`gcloud auth application-default login --impersonate-service-account=<service-account-email>`; it
requires `roles/iam.serviceAccountTokenCreator` on the target service account. See [local ADC setup](https://cloud.google.com/docs/authentication/set-up-adc-local-dev-environment).

Configure the following app settings only after the bucket and IAM policy are ready:

```text
ReceiptStorage__Provider=GoogleCloudStorage
ReceiptStorage__GoogleCloudStorage__Bucket=<private bucket name>
ReceiptStorage__GoogleCloudStorage__Prefix=aura/receipts
ReceiptStorage__GoogleCloudStorage__CredentialMode=ApplicationDefault
ReceiptStorage__MaxFileSizeMb=5
```

For a **synthetic, one-case local smoke** after starting AURA with the GCS settings,
use the existing loopback-only runner and require both GCS and Strict ZDR:

```powershell
.\tools\Invoke-PrivateReceiptSmoke.ps1 `
  -ImagesDirectory .\test_kit\v3_1\images `
  -BaseUrl http://127.0.0.1:5110 `
  -MaxCases 1 `
  -ConfirmExternalProviderSend `
  -RequireGoogleCloudStorage `
  -RequireStrictZeroDataRetention
```

The script checks health, SQL/schema readiness, storage provider, effective
provider data-collection denial, fallback disabled, and one AI permit before the
first upload. Results are local-only under the git-ignored `test_kit/results/`;
the runner does not assess accuracy or prove multi-host access. The required
confirmation is still necessary because the configured vision provider can
receive the synthetic image.

When probing a staging instance through a local loopback port-forward and validating a deployment
intended to run more than one worker, add
`-RequireSqlDistributedAiGate`. This makes the smoke stop before upload unless `/healthz`
confirms `sqlDistributedAiGateEnabled=true`; it does not prove two hosts contend for the same
permit, so a separate multi-host race/lost-session test is still required.

For a legacy/external key instead, set
`ReceiptStorage__GoogleCloudStorage__CredentialMode=ServiceAccountJson` and
`ReceiptStorage__GoogleCloudStorage__ServiceAccountJsonPath=<absolute path outside the app/repository>`.
The two modes are mutually exclusive at the AURA setting level; ADC mode rejects an explicit JSON
key path. ADC itself follows Google's standard discovery order, which can include
`GOOGLE_APPLICATION_CREDENTIALS` or a local user ADC file. If the environment variable is set, AURA
requires its credential file to exist outside the app content root. On a server, explicitly attach
the intended runtime service account and verify its identity/permissions; for local testing, prefer
the impersonation command above. Never point the JSON mode at the Drive connector's read-only key.

If hosting remains on SmartASP or another non-Google host, do not assume a Google workload identity
is attached. Use ADC only if the host can securely provide a supported federated/impersonated ADC
identity; otherwise use a separate GCS-only service-account key outside the deployed site and set
`CredentialMode=ServiceAccountJson`. If the host cannot keep that key outside the site root with
restricted access, leave GCS disabled and run the worker on infrastructure with workload identity.

Do not add the JSON contents, private key, bucket URLs, or local credential path to source control.
The currently configured Drive folder is a separate source; Drive originals are not deleted by this
storage adapter.

## Safe rollout and recovery gates

1. Keep `LocalFileSystem` until a test bucket is private and restricted to the application identity.
2. In an isolated Development/staging DB, verify health check, upload, readback, SHA-256, worker
   extraction, and a repeated source scan. Confirm a missing object fails closed and is not approved.
3. Verify object deletion is not enabled by a lifecycle rule before pending SQL jobs have completed.
   No application retention cleanup is implemented here. In particular, do not set a one-day bucket
   lifecycle rule: pending/retry jobs must keep their source bytes until terminal processing and any
   approved retention period.
4. Test two app instances against the same private bucket and shared SQL, including object readback
   from the second instance, a lost SQL lease, provider throttling, and retry after restart.
5. Back up and restore the SQL database and object data together; record RPO/RTO and demonstrate that
   restored rows still resolve to the correct object generation and hash.
6. Only after those checks may the storage provider be enabled in a hosting environment. Keep the
   SQL distributed AI gate disabled until multi-host chaos/load tests and backup/restore gates pass.

This adapter does not by itself make the system production-ready: business identity/authorization,
retention/legal approval, malware scanning, monitoring/alerting, provider privacy assurances, and
multi-host operational evidence remain separate release gates.
