[CmdletBinding()]
param(
    [Parameter()]
    [ValidatePattern('^https?://')]
    [string]$BaseUrl = 'http://localhost:5000',

    [Parameter()]
    [ValidateRange(1, 10)]
    [int]$Copies = 5,

    [Parameter()]
    [ValidateRange(1, 3600)]
    [int]$ProcessingTimeoutSeconds = 900,

    [Parameter()]
    [switch]$AllowNonLoopback,

    [Parameter()]
    [string]$ImagePath,

    [Parameter()]
    [ValidateRange(1, 1000000000)]
    [decimal]$ClaimedAmount = 295199,

    [Parameter()]
    [string]$OutputPath
)

$scriptDirectory = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($scriptDirectory)) {
    $scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
}
if ([string]::IsNullOrWhiteSpace($ImagePath)) {
    $ImagePath = Join-Path $scriptDirectory '..\wwwroot\test_data\images\HoaDon1.jpg'
}
if (-not (Test-Path -LiteralPath $ImagePath -PathType Leaf)) {
    throw "Smoke-test image not found at '$ImagePath'. Pass an absolute -ImagePath or run the script from the AURA repository."
}

$resolvedImage = (Resolve-Path -LiteralPath $ImagePath).Path
$imageSha256 = (Get-FileHash -LiteralPath $resolvedImage -Algorithm SHA256).Hash.ToLowerInvariant()
$normalizedBaseUrl = $BaseUrl.TrimEnd('/')
$baseUri = [Uri]$normalizedBaseUrl
if (-not $baseUri.IsLoopback -and -not $AllowNonLoopback) {
    throw 'Refusing to send concurrent uploads to a non-loopback host. Pass -AllowNonLoopback only for an explicitly approved synthetic test target.'
}
$jobs = 1..$Copies | ForEach-Object {
    Start-Job -ArgumentList $_, $normalizedBaseUrl, $resolvedImage, $ClaimedAmount, $ProcessingTimeoutSeconds -ScriptBlock {
        param($Sequence, $RootUrl, $ReceiptPath, $Amount, $ProcessingTimeout)

        Add-Type -AssemblyName System.Net.Http
        $handler = [System.Net.Http.HttpClientHandler]::new()
        $handler.CookieContainer = [System.Net.CookieContainer]::new()
        $client = [System.Net.Http.HttpClient]::new($handler)
        $client.Timeout = [TimeSpan]::FromMinutes(20)
        $clock = [System.Diagnostics.Stopwatch]::StartNew()

        try {
            $html = $client.GetStringAsync("$RootUrl/").GetAwaiter().GetResult()
            $tokenMatch = [regex]::Match($html, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"')
            if (-not $tokenMatch.Success) { throw 'Antiforgery token was not found on the home page.' }

            $multipart = [System.Net.Http.MultipartFormDataContent]::new()
            $bytes = [System.IO.File]::ReadAllBytes($ReceiptPath)
            $fileContent = [System.Net.Http.ByteArrayContent]::new($bytes)
            $fileContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('image/jpeg')
            $multipart.Add($fileContent, 'receiptFile', [System.IO.Path]::GetFileName($ReceiptPath))
            $multipart.Add([System.Net.Http.StringContent]::new([string]$Amount), 'claimedAmount')
            $multipart.Add([System.Net.Http.StringContent]::new("LOAD-$Sequence"), 'submitterCode')
            # Keep persisted runner identities ASCII-only. Windows PowerShell 5.1 can
            # decode UTF-8-without-BOM script literals as ANSI and persist mojibake.
            $multipart.Add([System.Net.Http.StringContent]::new("Load tester $Sequence"), 'submitterDisplayName')
            $multipart.Add([System.Net.Http.StringContent]::new('Concurrent smoke'), 'submitterDepartment')
            $multipart.Add([System.Net.Http.StringContent]::new($tokenMatch.Groups[1].Value), '__RequestVerificationToken')

            $response = $client.PostAsync("$RootUrl/Applicant/UploadReceipt", $multipart).GetAwaiter().GetResult()
            $acceptedMs = $clock.ElapsedMilliseconds
            $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            if (-not $response.IsSuccessStatusCode) { throw "HTTP $([int]$response.StatusCode): $body" }
            $accepted = $body | ConvertFrom-Json

            $processingClock = [System.Diagnostics.Stopwatch]::StartNew()
            do {
                if ($processingClock.Elapsed.TotalSeconds -ge $ProcessingTimeout) {
                    $status = [pscustomobject]@{
                        caseId = $accepted.caseId
                        processingState = 'PROCESSING_TIMEOUT'
                        actual = $null
                        servedProvider = $null
                        fallbackUsed = $false
                        providerErrorCode = 'PROCESSING_TIMEOUT'
                    }
                    break
                }
                Start-Sleep -Milliseconds 500
                $remainingSeconds = $ProcessingTimeout - $processingClock.Elapsed.TotalSeconds
                if ($remainingSeconds -le 0) {
                    $status = [pscustomobject]@{
                        caseId = $accepted.caseId
                        processingState = 'PROCESSING_TIMEOUT'
                        actual = $null
                        servedProvider = $null
                        fallbackUsed = $false
                        providerErrorCode = 'PROCESSING_TIMEOUT'
                    }
                    break
                }
                $pollCancellation = [System.Threading.CancellationTokenSource]::new(
                    [TimeSpan]::FromSeconds($remainingSeconds))
                try {
                    $statusJson = $client.GetStringAsync(
                        "$RootUrl$($accepted.statusUrl)", $pollCancellation.Token).GetAwaiter().GetResult()
                }
                catch [System.Threading.Tasks.TaskCanceledException] {
                    $status = [pscustomobject]@{
                        caseId = $accepted.caseId
                        processingState = 'PROCESSING_TIMEOUT'
                        actual = $null
                        servedProvider = $null
                        fallbackUsed = $false
                        providerErrorCode = 'PROCESSING_TIMEOUT'
                    }
                    break
                }
                finally {
                    $pollCancellation.Dispose()
                }
                $status = $statusJson | ConvertFrom-Json
            } while ($status.processingState -in @('PENDING', 'PROCESSING'))

            $clock.Stop()
            [pscustomobject]@{
                Sequence = $Sequence
                CaseId = $status.caseId
                HttpAcceptedMs = $acceptedMs
                EndToEndMs = $clock.ElapsedMilliseconds
                ProcessingState = $status.processingState
                Actual = $status.actual
                Provider = $status.servedProvider
                FallbackUsed = $status.fallbackUsed
                ErrorCode = $status.providerErrorCode
                ClientError = $null
            }
        }
        catch {
            $message = $_.Exception.Message
            if ($message.Length -gt 500) { $message = $message.Substring(0, 500) }
            [pscustomobject]@{
                Sequence = $Sequence
                CaseId = $null
                HttpAcceptedMs = $null
                EndToEndMs = $clock.ElapsedMilliseconds
                ProcessingState = 'CLIENT_ERROR'
                Actual = $null
                Provider = $null
                FallbackUsed = $false
                ErrorCode = 'CLIENT_ERROR'
                ClientError = $message
            }
        }
        finally {
            $client.Dispose()
            $handler.Dispose()
        }
    }
}

try {
    $results = @($jobs | Wait-Job | Receive-Job | Sort-Object Sequence)
    $results | Format-Table -AutoSize
    $acceptedSamples = @($results | Where-Object { $null -ne $_.HttpAcceptedMs } |
        ForEach-Object { [long]$_.HttpAcceptedMs } | Sort-Object)
    $endToEndSamples = @($results | Where-Object {
            $_.ProcessingState -ne 'CLIENT_ERROR' -and $null -ne $_.EndToEndMs
        } |
        ForEach-Object { [long]$_.EndToEndMs } | Sort-Object)
    $acceptedP95 = if ($acceptedSamples.Count -gt 0) {
        $acceptedSamples[[Math]::Ceiling($acceptedSamples.Count * 0.95) - 1]
    } else { $null }
    $endToEndP95 = if ($endToEndSamples.Count -gt 0) {
        $endToEndSamples[[Math]::Ceiling($endToEndSamples.Count * 0.95) - 1]
    } else { $null }
    $completedCount = @($results | Where-Object ProcessingState -eq 'COMPLETED').Count
    $clientErrorCount = @($results | Where-Object ProcessingState -eq 'CLIENT_ERROR').Count
    $nonCompletedCount = @($results | Where-Object ProcessingState -ne 'COMPLETED').Count
    "Accepted P95: $(if ($null -eq $acceptedP95) { 'n/a' } else { "$acceptedP95 ms" })"
    "End-to-end P95: $(if ($null -eq $endToEndP95) { 'n/a' } else { "$endToEndP95 ms" })"
    "Completed: $completedCount/$($results.Count)"
    "Client errors: $clientErrorCount/$($results.Count)"
    "Non-completed: $nonCompletedCount/$($results.Count)"

    $report = [ordered]@{
        schemaVersion = 1
        generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        baseUrl = $normalizedBaseUrl
        copies = $Copies
        imageSha256 = $imageSha256
        claimedAmount = $ClaimedAmount
        passed = ($nonCompletedCount -eq 0)
        metrics = [ordered]@{
            acceptedP95Ms = $acceptedP95
            endToEndP95Ms = $endToEndP95
            completed = $completedCount
            clientErrors = $clientErrorCount
            nonCompleted = $nonCompletedCount
            total = $results.Count
        }
        results = @($results | Select-Object Sequence, CaseId, HttpAcceptedMs, EndToEndMs,
            ProcessingState, Actual, Provider, FallbackUsed, ErrorCode, ClientError)
    }
    if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
        $resolvedOutputPath = [System.IO.Path]::GetFullPath($OutputPath)
        $outputDirectory = [System.IO.Path]::GetDirectoryName($resolvedOutputPath)
        if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
            [System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
        }
        [System.IO.File]::WriteAllText(
            $resolvedOutputPath,
            ($report | ConvertTo-Json -Depth 8),
            [System.Text.UTF8Encoding]::new($false))
        "Structured report: $resolvedOutputPath"
    }

    if ($nonCompletedCount -gt 0) { exit 1 }
}
finally {
    $jobs | Remove-Job -Force -ErrorAction SilentlyContinue
}
