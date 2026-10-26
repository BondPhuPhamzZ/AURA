#requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateRange(1024, 65535)][int]$Port = 5098,
    [ValidatePattern('^AURA_RetentionSandbox_[0-9]{8}$')]
    [string]$DatabaseName = ('AURA_RetentionSandbox_' + (Get-Date -Format 'yyyyMMdd')),
    [switch]$PrepareOnly,
    [switch]$EnableSyntheticDrive
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$receiptRoot = Join-Path $projectRoot ('.tmp\retention-sandbox\' + $DatabaseName + '\receipts')
$approvalPath = Join-Path (Split-Path -Parent $receiptRoot) 'approved-synthetic-sha256.json'
$url = 'http://127.0.0.1:' + $Port
$overrides = @{
    ASPNETCORE_ENVIRONMENT = 'Development'
    AllowedHosts = '127.0.0.1;localhost'
    ConnectionStrings__DefaultConnection = ('Server=(localdb)\MSSQLLocalDB;Database=' + $DatabaseName +
        ';Trusted_Connection=True;MultipleActiveResultSets=True;TrustServerCertificate=True;Connect Timeout=5;ConnectRetryCount=0')
    Database__ApplyMigrationsOnStartup = 'false'
    ReceiptStorage__Provider = 'LocalFileSystem'
    ReceiptStorage__Directory = $receiptRoot
    DataRetention__Enabled = 'true'
    DataRetention__BatchSize = '25'
    DataRetention__SweepIntervalMinutes = '5'
    DriveSandbox__Enabled = ([string][bool]$EnableSyntheticDrive).ToLowerInvariant()
    DriveSandbox__RequireMalwareScanner = ([string](-not [bool]$EnableSyntheticDrive)).ToLowerInvariant()
    Verify__Enabled = 'false'
    Vision__FallbackEnabled = 'false'
    Vision__MaxConcurrentRequests = '1'
    Vision__UseSqlDistributedGate = 'false'
    OpenRouter__DenyProviderDataCollection = 'true'
    ExternalImagePrivacy__Mode = 'ApprovedImagesOnly'
    ExternalImagePrivacy__ApprovedHashesFile = $approvalPath
}
$previous = @{}
$originalLocation = Get-Location
try {
    foreach ($name in $overrides.Keys) {
        $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
        [Environment]::SetEnvironmentVariable($name, $overrides[$name], 'Process')
    }
    Set-Location -LiteralPath $projectRoot
    New-Item -ItemType Directory -Path $receiptRoot -Force | Out-Null
    # Approve only the locked synthetic fixtures. Never add uploaded receipt bytes
    # automatically; a changed/unlisted image must be blocked before external HTTP.
    $approved = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($kit in @('v3_1', 'privacy_v1')) {
        $kitRoot = Join-Path $projectRoot ('test_kit\' + $kit)
        $manifest = Get-Content -LiteralPath (Join-Path $kitRoot 'manifest.json') -Raw | ConvertFrom-Json
        foreach ($case in $manifest.cases) {
            $name = if ($case.PSObject.Properties.Name -contains 'file_name') { $case.file_name } else { $case.fileName }
            if ([string]::IsNullOrWhiteSpace($name) -or [IO.Path]::GetFileName($name) -ne $name -or
                $name.IndexOfAny([char[]]@('/', '\', ':')) -ge 0 -or
                [string]$case.sha256 -notmatch '^[0-9A-Fa-f]{64}$') {
                throw 'Synthetic approval manifest has an invalid filename/hash.'
            }
            $image = Join-Path (Join-Path $kitRoot 'images') $name
            if ((Get-FileHash -LiteralPath $image -Algorithm SHA256).Hash -ne [string]$case.sha256) {
                throw 'Synthetic fixture bytes differ from the locked manifest; no images were approved.'
            }
            $null = $approved.Add([string]$case.sha256)
        }
    }
    # The historical 30-case kit predates per-image SHA-256 fields. Pin those
    # bytes to committed Git objects instead of trusting arbitrary files placed
    # in the images folder. The 100-file transfer set is byte-identical copies
    # of these 30 originals and needs no additional blanket approval.
    $legacyRoot = Join-Path $projectRoot 'test_kit'
    $legacy = Get-Content -LiteralPath (Join-Path $legacyRoot 'manifest.json') -Raw | ConvertFrom-Json
    if ($legacy.source -ne 'synthetic-deterministic') { throw 'Historical synthetic manifest provenance is invalid.' }
    $prefix = [string](& git -C $projectRoot rev-parse --show-prefix)
    if ($LASTEXITCODE -ne 0) { throw 'A Git checkout is required to verify historical synthetic fixtures.' }
    foreach ($case in $legacy.cases) {
        $name = [string]$case.file_name
        if ([string]::IsNullOrWhiteSpace($name) -or [IO.Path]::GetFileName($name) -ne $name -or
            $name.IndexOfAny([char[]]@('/', '\', ':')) -ge 0) { throw 'Historical synthetic filename is invalid.' }
        $image = Join-Path (Join-Path $legacyRoot 'images') $name
        $expectedBlob = [string](& git -C $projectRoot rev-parse --verify ('HEAD:' + $prefix + 'test_kit/images/' + $name))
        if ($LASTEXITCODE -ne 0) { throw 'Historical synthetic image is not committed.' }
        $actualBlob = [string](& git -C $projectRoot hash-object -- $image)
        if ($LASTEXITCODE -ne 0 -or $expectedBlob -ne $actualBlob) {
            throw 'Historical synthetic image differs from its committed bytes; no images were approved.'
        }
        $null = $approved.Add((Get-FileHash -LiteralPath $image -Algorithm SHA256).Hash)
    }
    ConvertTo-Json -InputObject @($approved | Sort-Object) | Set-Content -LiteralPath $approvalPath -Encoding UTF8
    Write-Output ('Synthetic retention sandbox: ' + $DatabaseName + '; URL=' + $url)
    Write-Output 'Use synthetic receipts only. Inference uses the provider/model configured in this process and User Secrets.'
    if ($EnableSyntheticDrive) {
        Write-Output 'Read-only Drive is enabled for the configured synthetic test folder; exact-image egress approval remains enforced.'
    }
    & dotnet build AURA.csproj --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Sandbox build failed.' }
    # Always build this exact configuration before using --no-build with EF/run.
    & dotnet ef database update --configuration Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Sandbox database preparation failed.' }
    if ($PrepareOnly) { return }
    & dotnet run --project AURA.csproj --configuration Release --no-build --no-launch-profile --urls $url
    if ($LASTEXITCODE -ne 0) { throw 'Sandbox host stopped with an error.' }
}
finally {
    Set-Location -LiteralPath $originalLocation
    foreach ($name in $previous.Keys) {
        if ($null -eq $previous[$name]) {
            Remove-Item -LiteralPath ('Env:\' + $name) -ErrorAction SilentlyContinue
        }
        else {
            [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process')
        }
    }
}
