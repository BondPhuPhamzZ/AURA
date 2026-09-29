[CmdletBinding()]
param(
    [string]$BaseUrl = "http://localhost:5000",
    [string]$ExpectedDatabase = "AuraDb",
    [switch]$StartLocalDb,
    [switch]$SkipHttp
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $projectRoot 'AURA.csproj'
$failures = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()

function Write-Check {
    param(
        [ValidateSet('PASS', 'WARN', 'FAIL')]
        [string]$State,
        [string]$Message
    )

    $color = switch ($State) {
        'PASS' { 'Green' }
        'WARN' { 'Yellow' }
        default { 'Red' }
    }
    Write-Host "[$State] $Message" -ForegroundColor $color
}

function Add-Failure([string]$Message) {
    $failures.Add($Message)
    Write-Check FAIL $Message
}

function Add-Warning([string]$Message) {
    $warnings.Add($Message)
    Write-Check WARN $Message
}

Write-Host 'AURA Demo Readiness Check' -ForegroundColor Cyan
Write-Host "Project: $projectRoot"
Write-Host "Expected database: $ExpectedDatabase"
Write-Host ''

if (-not (Test-Path -LiteralPath $projectPath)) {
    Add-Failure "Project not found at $projectPath."
    exit 1
}

try {
    $dotnetVersion = (& dotnet --version).Trim()
    Write-Check PASS ".NET SDK is available: $dotnetVersion."
}
catch {
    Add-Failure "dotnet CLI is unavailable: $($_.Exception.Message)"
}

$connectionString = $null
try {
    $secretOutput = & dotnet user-secrets list --project $projectPath 2>$null
    $connectionLine = $secretOutput | Where-Object {
        $_ -match '^ConnectionStrings:DefaultConnection\s*='
    } | Select-Object -First 1
    if ($connectionLine) {
        $connectionString = ($connectionLine -split '=', 2)[1].Trim()
    }
}
catch {
    Add-Warning "Cannot read user-secrets; falling back to appsettings.json for the database name."
}

if ([string]::IsNullOrWhiteSpace($connectionString)) {
    try {
        $settings = Get-Content -LiteralPath (Join-Path $projectRoot 'appsettings.json') -Raw |
            ConvertFrom-Json
        $connectionString = [string]$settings.ConnectionStrings.DefaultConnection
    }
    catch {
        Add-Failure "Cannot read the connection string from appsettings.json."
    }
}

$databaseName = $null
if (-not [string]::IsNullOrWhiteSpace($connectionString) -and
    $connectionString -match '(?i)(?:^|;)\s*(?:Database|Initial Catalog)\s*=\s*([^;]+)') {
    $databaseName = $Matches[1].Trim()
}

if ([string]::IsNullOrWhiteSpace($databaseName)) {
    Add-Failure 'Cannot determine the database name from the connection string.'
}
elseif ($databaseName -ne $ExpectedDatabase) {
    Add-Failure "The app targets '$databaseName', not the expected demo database '$ExpectedDatabase'."
}
else {
    Write-Check PASS "Connection string targets '$databaseName' (credentials are not printed)."
}

$usesLocalDb = -not [string]::IsNullOrWhiteSpace($connectionString) -and
    $connectionString -match '(?i)\(localdb\)\\([^;]+)'
if ($usesLocalDb) {
    $localDbInstance = $Matches[1]
    if (-not (Get-Command sqllocaldb -ErrorAction SilentlyContinue)) {
        Add-Failure 'The connection string uses LocalDB, but sqllocaldb.exe is unavailable.'
    }
    else {
        if ($StartLocalDb) {
            & sqllocaldb start $localDbInstance | Out-Null
        }

        $instanceOutput = & sqllocaldb info $localDbInstance 2>&1
        if ($LASTEXITCODE -ne 0) {
            Add-Failure "Cannot access LocalDB '$localDbInstance': $($instanceOutput -join ' ')"
        }
        elseif (($instanceOutput -join "`n") -notmatch '(?im)^State:\s+Running\s*$') {
            Add-Failure "LocalDB '$localDbInstance' is not running. Run again with -StartLocalDb before AURA."
        }
        else {
            Write-Check PASS "LocalDB '$localDbInstance' is running."
        }
    }
}
else {
    Write-Check PASS 'The connection string does not use LocalDB; LocalDB check skipped.'
}

$policyPath = Join-Path $projectRoot 'BUSINESS_RULES.md'
if (Test-Path -LiteralPath $policyPath) {
    Write-Check PASS 'BUSINESS_RULES.md is present.'
}
else {
    Add-Failure 'BUSINESS_RULES.md is missing; the Vision provider has no runtime policy.'
}

$storagePath = Join-Path $projectRoot 'App_Data\receipts'
try {
    if (-not (Test-Path -LiteralPath $storagePath)) {
        New-Item -ItemType Directory -Path $storagePath -Force | Out-Null
    }
    $probePath = Join-Path $storagePath '.readiness-probe.tmp'
    [System.IO.File]::WriteAllText($probePath, 'ready')
    Remove-Item -LiteralPath $probePath -Force
    Write-Check PASS 'Receipt storage exists and is writable.'
}
catch {
    Add-Failure "Receipt storage is not writable: $($_.Exception.Message)"
}

if (-not $SkipHttp) {
    try {
        $health = Invoke-RestMethod -Uri "$($BaseUrl.TrimEnd('/'))/healthz" -TimeoutSec 15
        if ($health.status -ne 'ok') {
            Add-Failure "Health endpoint returned '$($health.status)'."
        }
        else {
            Write-Check PASS 'Health endpoint returned status=ok.'
        }

        if ($health.databaseAvailable -ne $true) {
            Add-Failure 'Health endpoint reports databaseAvailable=false.'
        }
        elseif ($health.databaseUpToDate -ne $true -or [int]$health.pendingMigrationCount -ne 0) {
            Add-Failure "Database has pending migrations: $($health.pendingMigrationCount)."
        }
        else {
            Write-Check PASS 'Database is reachable and has no pending migration.'
        }

        if ($health.storageAvailable -ne $true) {
            Add-Failure 'Health endpoint reports storageAvailable=false.'
        }
        if ($health.aiConfigured -ne $true -or $health.policyAvailable -ne $true) {
            Add-Failure 'AI credentials or runtime policy are not ready.'
        }
        else {
            Write-Check PASS "Provider '$($health.provider)' / model '$($health.model)' is configured."
        }

        if ($health.fallbackEnabled -eq $true) {
            Add-Warning "Fallback to '$($health.fallbackProvider)' is enabled. Disable it for provider benchmarks."
        }
        else {
            Write-Check PASS 'Fallback is disabled, matching the current benchmark and demo baseline.'
        }
    }
    catch {
        Add-Failure "Cannot read $BaseUrl/healthz. Start AURA and retry. $($_.Exception.Message)"
    }
}
else {
    Add-Warning 'HTTP health check skipped because -SkipHttp was supplied.'
}

Write-Host ''
if ($failures.Count -gt 0) {
    Write-Host "NOT READY: $($failures.Count) failure(s), $($warnings.Count) warning(s)." -ForegroundColor Red
    exit 1
}

Write-Host "READY: 0 failures, $($warnings.Count) warning(s)." -ForegroundColor Green
exit 0
