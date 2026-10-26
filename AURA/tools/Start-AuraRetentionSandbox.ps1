#requires -Version 5.1
[CmdletBinding()]
param(
    [ValidatePattern('^AURA_RetentionSandbox_[0-9]{8}$')]
    [string]$DatabaseName = ('AURA_RetentionSandbox_' + (Get-Date -Format 'yyyyMMdd')),
    [switch]$PrepareOnly,
    [switch]$EnableSyntheticDrive
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$activeProfilePath = Join-Path $projectRoot '.tmp\retention-sandbox\active.json'
if (Test-Path -LiteralPath $activeProfilePath) {
    $existingProfile = Get-Content -LiteralPath $activeProfilePath -Raw | ConvertFrom-Json
    if (-not $PSBoundParameters.ContainsKey('DatabaseName')) {
        $existingConnection = [string]$existingProfile.ConnectionStrings.DefaultConnection
        if ($existingConnection -notmatch '(?:^|;)Database=(AURA_RetentionSandbox_[0-9]{8})(?:;|$)') {
            throw 'Existing local sandbox profile has an invalid database name.'
        }
        $DatabaseName = $Matches[1]
    }
    if (-not $PSBoundParameters.ContainsKey('EnableSyntheticDrive')) {
        $EnableSyntheticDrive = [bool]$existingProfile.DriveSandbox.Enabled
    }
}
$receiptRoot = Join-Path $projectRoot ('.tmp\retention-sandbox\' + $DatabaseName + '\receipts')
$approvalPath = Join-Path (Split-Path -Parent $receiptRoot) 'approved-synthetic-sha256.json'
$url = 'http://127.0.0.1:5000'
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
    OpenRouter__RequireZeroDataRetention = 'true'
    OpenRouter__Model = 'qwen/qwen3.8-27b'
    Vision__Provider = 'OpenRouter'
    ExternalImagePrivacy__Mode = 'ApprovedImagesOnly'
    ExternalImagePrivacy__ApprovedHashesFile = $approvalPath
}
$previous = @{}
$originalLocation = Get-Location
try {
    $previous['AURA_LOCAL_SANDBOX'] = [Environment]::GetEnvironmentVariable('AURA_LOCAL_SANDBOX', 'Process')
    [Environment]::SetEnvironmentVariable('AURA_LOCAL_SANDBOX', $null, 'Process')
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
    Write-Output 'Use synthetic receipts only. This profile pins Qwen3.8-27B with a Strict ZDR routing request; User Secrets supplies the API key.'
    if ($EnableSyntheticDrive) {
        Write-Output 'Read-only Drive is enabled for the configured synthetic test folder; exact-image egress approval remains enforced.'
    }
    & dotnet build AURA.csproj --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Sandbox build failed.' }
    # Always build this exact configuration before using --no-build with EF/run.
    & dotnet ef database update --configuration Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Sandbox database preparation failed.' }
    # Visual Studio F5 and this launcher read the same ignored configuration.
    # The API key, Drive folder ID and JSON credential remain outside this file.
    $profile = [ordered]@{
        AllowedHosts = $overrides.AllowedHosts
        ConnectionStrings = @{ DefaultConnection = $overrides.ConnectionStrings__DefaultConnection }
        Database = @{ ApplyMigrationsOnStartup = $false }
        ReceiptStorage = @{ Provider = 'LocalFileSystem'; Directory = $receiptRoot }
        DataRetention = @{ Enabled = $true; BatchSize = 25; SweepIntervalMinutes = 5 }
        DriveSandbox = @{ Enabled = [bool]$EnableSyntheticDrive;
            RequireMalwareScanner = -not [bool]$EnableSyntheticDrive }
        Verify = @{ Enabled = $false }
        Vision = @{ Provider = 'OpenRouter'; FallbackEnabled = $false;
            MaxConcurrentRequests = 1; UseSqlDistributedGate = $false }
        OpenRouter = @{ Model = 'qwen/qwen3.8-27b'; RequireZeroDataRetention = $true;
            DenyProviderDataCollection = $true }
        ExternalImagePrivacy = @{ Mode = 'ApprovedImagesOnly'; ApprovedHashesFile = $approvalPath }
    }
    $activeDirectory = Split-Path -Parent $activeProfilePath
    New-Item -ItemType Directory -Path $activeDirectory -Force | Out-Null
    $temporaryProfilePath = $activeProfilePath + '.new'
    ConvertTo-Json -InputObject $profile -Depth 6 | Set-Content -LiteralPath $temporaryProfilePath -Encoding UTF8
    Move-Item -LiteralPath $temporaryProfilePath -Destination $activeProfilePath -Force
    Write-Output ('Visual Studio AURA and launcher share ' + $url + ' and database ' + $DatabaseName + '.')
    if ($PrepareOnly) { return }
    [Environment]::SetEnvironmentVariable('AURA_LOCAL_SANDBOX', '1', 'Process')
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
