[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$ImagesDirectory,
    [Parameter()] [string]$BaseUrl = 'http://127.0.0.1:5110',
    [Parameter(Mandatory)] [switch]$ConfirmExternalProviderSend,
    [Parameter()] [ValidateRange(1, 30)] [int]$MaxCases = 15,
    [Parameter()] [ValidateRange(1, 30)] [int]$StartSequence = 1,
    [Parameter()] [ValidateRange(1, 30)] [int]$CaseTimeoutMinutes = 10,
    [Parameter()] [string]$OutputDirectory = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# This runner never sends files to a remote AURA host or to Drive. AURA's
# configured vision provider may still receive the images; require opt-in.
$uri = [Uri]$BaseUrl
if ($uri.Scheme -ne 'http' -or $uri.Host -notin @('127.0.0.1', 'localhost', '::1')) {
    throw 'BaseUrl must be an HTTP loopback address.'
}
if (-not $ConfirmExternalProviderSend) { throw 'Explicit provider-send confirmation is required.' }
$source = (Resolve-Path -LiteralPath $ImagesDirectory).Path
$files = @(Get-ChildItem -LiteralPath $source -File |
    Where-Object { $_.Extension -in @('.jpg', '.jpeg', '.png') } |
    Sort-Object Name | Select-Object -Skip ($StartSequence - 1) -First $MaxCases)
if ($files.Count -ne $MaxCases) { throw "Expected $MaxCases supported images; found $($files.Count)." }
$inputHashes = @($files | ForEach-Object { (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash })

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
    $OutputDirectory = Join-Path $projectRoot ('test_kit\results\private-receipts-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
}
$resolvedOutput = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $resolvedOutput | Out-Null

Add-Type -AssemblyName System.Net.Http
$handler = [Net.Http.HttpClientHandler]::new()
$handler.CookieContainer = [Net.CookieContainer]::new()
$client = [Net.Http.HttpClient]::new($handler)
$client.Timeout = [TimeSpan]::FromMinutes($CaseTimeoutMinutes + 1)
$results = [Collections.Generic.List[object]]::new()
$runStart = [DateTimeOffset]::UtcNow

try {
    $root = $BaseUrl.TrimEnd('/')
    $health = ($client.GetStringAsync("$root/healthz").GetAwaiter().GetResult() | ConvertFrom-Json)
    if ($health.status -ne 'ok' -or -not $health.databaseUpToDate -or -not $health.storageAvailable) {
        throw 'Local health gate is not ready.'
    }
    $html = $client.GetStringAsync("$root/").GetAwaiter().GetResult()
    $tokenMatch = [regex]::Match($html, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"')
    if (-not $tokenMatch.Success) { throw 'Antiforgery token was not found.' }
    $token = $tokenMatch.Groups[1].Value

    for ($i = 0; $i -lt $files.Count; $i++) {
        $file = $files[$i]
        $watch = [Diagnostics.Stopwatch]::StartNew()
        $acceptedMs = $null
        $state = 'CLIENT_ERROR'
        $decision = $null
        $providerErrorCode = $null
        $requestId = $null
        $errorType = $null
        try {
            $multipart = [Net.Http.MultipartFormDataContent]::new()
            try {
                $bytes = [IO.File]::ReadAllBytes($file.FullName)
                $content = [Net.Http.ByteArrayContent]::new($bytes)
                $mediaType = $(if ($file.Extension -ieq '.png') { 'image/png' } else { 'image/jpeg' })
                $content.Headers.ContentType = [Net.Http.Headers.MediaTypeHeaderValue]::Parse($mediaType)
                $multipart.Add($content, 'receiptFile', $file.Name)
                $multipart.Add([Net.Http.StringContent]::new($token), '__RequestVerificationToken')
                $response = $client.PostAsync("$root/Applicant/UploadReceipt", $multipart).GetAwaiter().GetResult()
                $acceptedMs = $watch.ElapsedMilliseconds
                if ([int]$response.StatusCode -ne 202) { throw "IntakeHttp$([int]$response.StatusCode)" }
                $accepted = ($response.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json)
            }
            finally { $multipart.Dispose() }

            $deadline = [DateTimeOffset]::UtcNow.AddMinutes($CaseTimeoutMinutes)
            do {
                if ([DateTimeOffset]::UtcNow -ge $deadline) { throw 'CaseTimeout' }
                Start-Sleep -Milliseconds 500
                $status = ($client.GetStringAsync("$root$($accepted.statusUrl)").GetAwaiter().GetResult() | ConvertFrom-Json)
            } while ($status.processingState -in @('PENDING', 'PROCESSING'))
            $state = [string]$status.processingState
            $decision = [string]$status.actual
            $providerErrorCode = [string]$status.providerErrorCode
            $requestId = [string]$status.caseId
        }
        catch { $errorType = $_.Exception.GetType().Name }
        finally { $watch.Stop() }

        $results.Add([pscustomobject]@{
            Sequence = $StartSequence + $i
            InputSha256 = $inputHashes[$i]
            RequestId = $requestId
            ProcessingState = $state
            Decision = $decision
            ProviderErrorCode = $providerErrorCode
            ClientErrorType = $errorType
            AcceptedMs = $acceptedMs
            EndToEndMs = $watch.ElapsedMilliseconds
        })
        # Persist each case so an interrupted run retains auditable progress.
        $results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $resolvedOutput 'results.json') -Encoding UTF8
        Write-Host "Private receipt sequence $($StartSequence + $i): $state"
    }
}
finally {
    $client.Dispose()
    $handler.Dispose()
}

$summary = [pscustomobject]@{
    StartedUtc = $runStart.ToString('o')
    FinishedUtc = [DateTimeOffset]::UtcNow.ToString('o')
    Count = $results.Count
    Completed = @($results | Where-Object ProcessingState -eq 'COMPLETED').Count
    Failed = @($results | Where-Object ProcessingState -eq 'FAILED').Count
    ClientErrors = @($results | Where-Object ProcessingState -eq 'CLIENT_ERROR').Count
    ProviderErrors = @($results | Where-Object { -not [string]::IsNullOrWhiteSpace($_.ProviderErrorCode) }).Count
    AccuracyAssessed = $false
    Note = 'Previously used real images; this runner does not assess locked labels or blind accuracy.'
}
$results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $resolvedOutput 'results.json') -Encoding UTF8
$summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $resolvedOutput 'summary.json') -Encoding UTF8
$summary | ConvertTo-Json -Compress
