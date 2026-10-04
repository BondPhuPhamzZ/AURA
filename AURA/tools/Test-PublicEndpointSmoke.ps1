[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^https://')]
    [string]$BaseUrl,

    [Parameter()]
    [ValidateRange(1, 10)]
    [int]$Attempts = 3,

    [Parameter()]
    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http

$normalizedBaseUrl = $BaseUrl.TrimEnd('/')
$client = [System.Net.Http.HttpClient]::new()
$client.Timeout = [TimeSpan]::FromSeconds(30)

try {
    $samples = for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
        $clock = [Diagnostics.Stopwatch]::StartNew()
        $response = $client.GetAsync("$normalizedBaseUrl/healthz").GetAwaiter().GetResult()
        $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $clock.Stop()
        $health = $body | ConvertFrom-Json

        [pscustomobject]@{
            attempt = $attempt
            httpStatus = [int]$response.StatusCode
            elapsedMs = $clock.ElapsedMilliseconds
            healthStatus = $health.status
            databaseAvailable = $health.databaseAvailable
            databaseUpToDate = $health.databaseUpToDate
            pendingMigrationCount = $health.pendingMigrationCount
            storageAvailable = $health.storageAvailable
            provider = $health.provider
            model = $health.model
            fallbackEnabled = $health.fallbackEnabled
            timestampFromServer = $health.timestamp
            observedAtUtc = [DateTime]::UtcNow.ToString('o')
        }
    }

    $homeResponse = $client.GetAsync("$normalizedBaseUrl/").GetAwaiter().GetResult()
    $report = [pscustomobject]@{
        schemaVersion = 1
        baseUrl = $normalizedBaseUrl
        observer = 'Independent HTTP client; no browser session or local application process required.'
        passed = @($samples | Where-Object { $_.httpStatus -ne 200 -or $_.healthStatus -ne 'ok' }).Count -eq 0 -and
            [int]$homeResponse.StatusCode -eq 200
        homeHttpStatus = [int]$homeResponse.StatusCode
        attempts = @($samples)
    }

    $json = $report | ConvertTo-Json -Depth 6
    $json
    if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
        $resolvedOutput = [IO.Path]::GetFullPath($OutputPath)
        $parent = Split-Path -Parent $resolvedOutput
        if (-not [string]::IsNullOrWhiteSpace($parent)) {
            New-Item -ItemType Directory -Force -Path $parent | Out-Null
        }
        [IO.File]::WriteAllText($resolvedOutput, $json, [Text.UTF8Encoding]::new($false))
        Write-Host "Saved: $resolvedOutput"
    }

    if (-not $report.passed) { exit 1 }
}
finally {
    $client.Dispose()
}
