[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^https?://')]
    [string]$BaseUrl,

    [Parameter()]
    [ValidateRange(2, 8)]
    [int]$Clients = 5,

    [Parameter(Mandatory)]
    [string]$OutputDirectory,

    [Parameter()]
    [ValidateRange(1, 900)]
    [int]$TimeoutSeconds = 600,

    [Parameter()]
    [ValidateRange(1, 600000)]
    [long]$LatencyTargetMs = 90000
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$normalizedBaseUrl = $BaseUrl.TrimEnd('/')
$resolvedOutput = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($resolvedOutput) | Out-Null

$jobs = 1..$Clients | ForEach-Object {
    Start-Job -ArgumentList $_, $normalizedBaseUrl, $TimeoutSeconds -ScriptBlock {
        param($Sequence, $RootUrl, $RequestTimeoutSeconds)

        Add-Type -AssemblyName System.Net.Http
        $handler = [Net.Http.HttpClientHandler]::new()
        $handler.CookieContainer = [Net.CookieContainer]::new()
        $client = [Net.Http.HttpClient]::new($handler)
        $client.Timeout = [TimeSpan]::FromSeconds($RequestTimeoutSeconds)
        $clock = [Diagnostics.Stopwatch]::StartNew()

        try {
            $homeHtml = $client.GetStringAsync("$RootUrl/").GetAwaiter().GetResult()
            $tokenMatch = [regex]::Match(
                $homeHtml,
                'name="__RequestVerificationToken"[^>]*value="([^"]+)"')
            if (-not $tokenMatch.Success) {
                throw 'Antiforgery token was not found on the home page.'
            }

            $fields = [Collections.Generic.Dictionary[string,string]]::new()
            $fields['__RequestVerificationToken'] = $tokenMatch.Groups[1].Value
            $content = [Net.Http.FormUrlEncodedContent]::new($fields)
            $request = [Net.Http.HttpRequestMessage]::new(
                [Net.Http.HttpMethod]::Post,
                "$RootUrl/Verify/RunHarness")
            $request.Headers.TryAddWithoutValidation('X-Requested-With', 'XMLHttpRequest') | Out-Null
            $request.Content = $content

            try {
                $response = $client.SendAsync($request).GetAwaiter().GetResult()
                $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                $clock.Stop()
                [pscustomobject]@{
                    Sequence = $Sequence
                    HttpStatus = [int]$response.StatusCode
                    ElapsedMs = $clock.ElapsedMilliseconds
                    Body = $body
                    ClientError = $null
                }
            }
            finally {
                $request.Dispose()
                $content.Dispose()
            }
        }
        catch {
            $clock.Stop()
            $message = $_.Exception.Message
            if ($message.Length -gt 1000) { $message = $message.Substring(0, 1000) }
            [pscustomobject]@{
                Sequence = $Sequence
                HttpStatus = $null
                ElapsedMs = $clock.ElapsedMilliseconds
                Body = $null
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
    $startedAtUtc = [DateTimeOffset]::UtcNow
    $results = @($jobs | Wait-Job | Receive-Job | Sort-Object Sequence)
    $completedAtUtc = [DateTimeOffset]::UtcNow

    foreach ($result in $results) {
        if ($null -ne $result.Body) {
            $bodyPath = Join-Path $resolvedOutput ("client-{0:D2}-http-{1}.json" -f
                [int]$result.Sequence, [int]$result.HttpStatus)
            [IO.File]::WriteAllText($bodyPath, [string]$result.Body, [Text.UTF8Encoding]::new($false))
        }
    }

    $successful = @($results | Where-Object HttpStatus -eq 200)
    $conflicts = @($results | Where-Object HttpStatus -eq 409)
    $clientErrors = @($results | Where-Object { $null -ne $_.ClientError })

    $verifyCases = @()
    $verifyPassCount = 0
    $systemErrorCount = 0
    $verifyEndToEndMs = $null
    if ($successful.Count -eq 1) {
        $winner = $successful[0]
        $verifyEndToEndMs = [long]$winner.ElapsedMs
        try {
            $verifyCases = @($winner.Body | ConvertFrom-Json)
            $verifyPassCount = @($verifyCases | Where-Object pass -eq $true).Count
            $systemErrorCount = @($verifyCases | Where-Object actual -eq 'ESCALATE_SYSTEM_ERROR').Count
        }
        catch {
            $clientErrors += [pscustomobject]@{
                Sequence = $winner.Sequence
                HttpStatus = $winner.HttpStatus
                ElapsedMs = $winner.ElapsedMs
                Body = $null
                ClientError = "Successful Verify response was not valid JSON: $($_.Exception.Message)"
            }
        }
    }

    $gatePassed = $successful.Count -eq 1 -and $conflicts.Count -eq ($Clients - 1)
    $verifyPassed = $verifyCases.Count -eq 5 -and $verifyPassCount -eq 5 -and $systemErrorCount -eq 0
    $latencyPassed = $null -ne $verifyEndToEndMs -and $verifyEndToEndMs -le $LatencyTargetMs
    $passed = $gatePassed -and $verifyPassed -and $latencyPassed -and $clientErrors.Count -eq 0

    $report = [ordered]@{
        schemaVersion = 1
        generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        startedAtUtc = $startedAtUtc.ToString('O')
        completedAtUtc = $completedAtUtc.ToString('O')
        baseUrl = $normalizedBaseUrl
        requestedClients = $Clients
        latencyTargetMs = $LatencyTargetMs
        passed = $passed
        gate = [ordered]@{
            passed = $gatePassed
            successfulRuns = $successful.Count
            conflictResponses = $conflicts.Count
            expectedConflictResponses = $Clients - 1
        }
        verify = [ordered]@{
            passed = $verifyPassed
            latencyPassed = $latencyPassed
            endToEndMs = $verifyEndToEndMs
            caseCount = $verifyCases.Count
            passCount = $verifyPassCount
            systemErrors = $systemErrorCount
            cases = @($verifyCases | Select-Object testCaseId, caseId, expected, actual, pass,
                latencyMs, primaryProvider, servedProvider, fallbackUsed, providerErrorCode)
        }
        clients = @($results | Select-Object Sequence, HttpStatus, ElapsedMs, ClientError)
    }

    $reportPath = Join-Path $resolvedOutput 'concurrent-verify-summary.json'
    [IO.File]::WriteAllText(
        $reportPath,
        ($report | ConvertTo-Json -Depth 12),
        [Text.UTF8Encoding]::new($false))

    $results | Select-Object Sequence, HttpStatus, ElapsedMs, ClientError | Format-Table -AutoSize
    "Verify: $verifyPassCount/5 pass; system errors: $systemErrorCount; end-to-end: $verifyEndToEndMs ms"
    "Gate: $($successful.Count) HTTP 200; $($conflicts.Count) HTTP 409; expected conflicts: $($Clients - 1)"
    "Latency target: <= $LatencyTargetMs ms; passed: $latencyPassed"
    "Structured report: $reportPath"

    if (-not $passed) { exit 1 }
}
finally {
    $jobs | Remove-Job -Force -ErrorAction SilentlyContinue
}
