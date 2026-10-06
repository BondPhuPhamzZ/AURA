[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^https://')]
    [string]$BaseUrl,

    [Parameter(Mandatory)]
    [string]$OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http

function Write-Utf8Json {
    param([Parameter(Mandatory)] [string]$Path, [Parameter(Mandatory)] $Value)
    [IO.File]::WriteAllText($Path, ($Value | ConvertTo-Json -Depth 30), [Text.UTF8Encoding]::new($false))
}

function Post-Form {
    param(
        [Parameter(Mandatory)] [Net.Http.HttpClient]$Client,
        [Parameter(Mandatory)] [string]$Url,
        [Parameter(Mandatory)] [Collections.Generic.Dictionary[string,string]]$Fields
    )
    $content = [Net.Http.FormUrlEncodedContent]::new($Fields)
    $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Post, $Url)
    $request.Headers.TryAddWithoutValidation('X-Requested-With', 'XMLHttpRequest') | Out-Null
    $request.Content = $content
    try {
        $response = $Client.SendAsync($request).GetAwaiter().GetResult()
        $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        if (-not $response.IsSuccessStatusCode) {
            throw "POST $Url returned HTTP $([int]$response.StatusCode): $body"
        }
        return $body
    }
    finally {
        $request.Dispose()
        $content.Dispose()
    }
}

$normalizedBaseUrl = $BaseUrl.TrimEnd('/')
$resolvedOutput = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $resolvedOutput | Out-Null

$handler = [Net.Http.HttpClientHandler]::new()
$handler.CookieContainer = [Net.CookieContainer]::new()
$client = [Net.Http.HttpClient]::new($handler)
$client.Timeout = [TimeSpan]::FromMinutes(12)

try {
    $homeHtml = $client.GetStringAsync("$normalizedBaseUrl/").GetAwaiter().GetResult()
    $tokenMatch = [regex]::Match($homeHtml, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"')
    if (-not $tokenMatch.Success) { throw 'Antiforgery token was not found.' }
    $token = $tokenMatch.Groups[1].Value

    $verifyResultsPath = Join-Path $resolvedOutput 'verify-results.json'
    $verifyWasResumed = Test-Path -LiteralPath $verifyResultsPath -PathType Leaf
    $verifyClock = [Diagnostics.Stopwatch]::StartNew()
    if ($verifyWasResumed) {
        $verifyRaw = [IO.File]::ReadAllText($verifyResultsPath, [Text.UTF8Encoding]::new($false, $true))
    } else {
        $verifyFields = [Collections.Generic.Dictionary[string,string]]::new()
        $verifyFields['__RequestVerificationToken'] = $token
        $verifyRaw = Post-Form -Client $client -Url "$normalizedBaseUrl/Verify/RunHarness" -Fields $verifyFields
        [IO.File]::WriteAllText($verifyResultsPath, $verifyRaw, [Text.UTF8Encoding]::new($false))
    }
    $verifyClock.Stop()
    $verifyResults = ConvertFrom-Json -InputObject $verifyRaw
    if ($verifyResults.Count -ne 5) { throw "Verify returned $($verifyResults.Count) cases instead of 5." }

    $verifyPassCount = @($verifyResults | Where-Object { $_.pass }).Count
    $systemErrors = @($verifyResults | Where-Object { $_.actual -eq 'ESCALATE_SYSTEM_ERROR' }).Count
    if ($verifyPassCount -ne 5 -or $systemErrors -ne 0) {
        throw "Verify gate failed: pass=$verifyPassCount/5, systemErrors=$systemErrors."
    }

    $target = $verifyResults | Where-Object { $_.canForward -and $_.actual -like 'ESCALATE_*' } |
        Select-Object -First 1
    if ($null -eq $target) { throw 'Verify produced no escalation for workflow smoke.' }

    $forwardFields = [Collections.Generic.Dictionary[string,string]]::new()
    $forwardFields['__RequestVerificationToken'] = $token
    $forwardFields['id'] = [string]$target.caseId
    $forwardRaw = Post-Form -Client $client `
        -Url "$normalizedBaseUrl/Applicant/ForwardToManager" -Fields $forwardFields

    $reviewerAfterForward = $client.GetStringAsync("$normalizedBaseUrl/Home/ReviewerQueue").GetAwaiter().GetResult()
    $shortId = ([string]$target.caseId).Substring(0, 8)
    if ($reviewerAfterForward.IndexOf($shortId, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
        throw "Forwarded request $shortId was not visible in ReviewerQueue."
    }

    $decisionFields = [Collections.Generic.Dictionary[string,string]]::new()
    $decisionFields['__RequestVerificationToken'] = $token
    $decisionFields['id'] = [string]$target.caseId
    $decisionFields['decision'] = 'YES'
    $decisionRaw = Post-Form -Client $client `
        -Url "$normalizedBaseUrl/Reviewer/EscalateAction" -Fields $decisionFields

    $undoFields = [Collections.Generic.Dictionary[string,string]]::new()
    $undoFields['__RequestVerificationToken'] = $token
    $undoFields['id'] = [string]$target.caseId
    $undoFields['decision'] = 'UNDO'
    $undoRaw = Post-Form -Client $client `
        -Url "$normalizedBaseUrl/Reviewer/EscalateAction" -Fields $undoFields

    $auditHtml = $client.GetStringAsync("$normalizedBaseUrl/Home/AuditTrail").GetAwaiter().GetResult()
    [IO.File]::WriteAllText((Join-Path $resolvedOutput 'audit-after-undo.html'), $auditHtml,
        [Text.UTF8Encoding]::new($false))
    if ($auditHtml.IndexOf($shortId, [StringComparison]::OrdinalIgnoreCase) -lt 0 -or
        $auditHtml.IndexOf('MANAGER_UNDO', [StringComparison]::Ordinal) -lt 0) {
        throw "Audit did not contain request $shortId and MANAGER_UNDO after workflow smoke."
    }

    $report = [pscustomobject]@{
        SchemaVersion = 1
        BaseUrl = $normalizedBaseUrl
        TestedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        Verify = [pscustomobject]@{
            PassCount = $verifyPassCount
            Total = $verifyResults.Count
            SystemErrors = $systemErrors
            EndToEndMs = if ($verifyWasResumed) { $null } else { $verifyClock.ElapsedMilliseconds }
            ResumedFromExistingRawResults = $verifyWasResumed
            Cases = $verifyResults | Select-Object testCaseId, caseId, expected, actual, pass, latencyMs
        }
        Workflow = [pscustomobject]@{
            RequestId = $target.caseId
            OriginalStatus = $target.actual
            ForwardResponse = $forwardRaw | ConvertFrom-Json
            DecisionResponse = $decisionRaw | ConvertFrom-Json
            UndoResponse = $undoRaw | ConvertFrom-Json
            ReviewerQueueContainedRequest = $true
            AuditContainedRequestAndUndo = $true
            FinalStatusAfterUndo = ($undoRaw | ConvertFrom-Json).status
        }
        Notes = @(
            'Verify sends five real OpenRouter image requests and persists results.',
            'When an existing raw result is resumed, aggregate wall time is intentionally null; per-case latency remains authoritative.',
            'Workflow uses one Verify escalation, approves it, then restores the original escalation with UNDO.',
            'Audit events remain immutable evidence after undo.'
        )
    }
    Write-Utf8Json -Path (Join-Path $resolvedOutput 'verify-workflow-summary.json') -Value $report
    $report | ConvertTo-Json -Depth 15
}
finally {
    $client.Dispose()
    $handler.Dispose()
}
