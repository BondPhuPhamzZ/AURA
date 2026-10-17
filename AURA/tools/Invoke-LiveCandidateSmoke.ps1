[CmdletBinding()]
param(
    [Parameter()]
    [ValidatePattern('^https?://')]
    [string]$BaseUrl = 'http://localhost:5000',

    [Parameter()]
    [ValidateCount(1, 10)]
    [string[]]$ImageNames = @('HoaDon1.jpg', 'HoaDon4.jpg', 'HoaDon5.jpg'),

    [Parameter()]
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http

$scriptDirectory = $PSScriptRoot
$projectRoot = [IO.Path]::GetFullPath((Join-Path $scriptDirectory '..'))
$imagesDirectory = Join-Path $projectRoot 'wwwroot\test_data\images'
$expectedPath = Join-Path $projectRoot 'wwwroot\test_data\expected-results.json'
$expected = @{}
foreach ($item in (Get-Content -LiteralPath $expectedPath -Raw | ConvertFrom-Json)) {
    $expected[[string]$item.imageName] = [string]$item.expectedStatus
}

$normalizedBaseUrl = $BaseUrl.TrimEnd('/')
$handler = [Net.Http.HttpClientHandler]::new()
$handler.CookieContainer = [Net.CookieContainer]::new()
$client = [Net.Http.HttpClient]::new($handler)
$client.Timeout = [TimeSpan]::FromMinutes(20)
$runClock = [Diagnostics.Stopwatch]::StartNew()

try {
    $homeHtml = $client.GetStringAsync("$normalizedBaseUrl/").GetAwaiter().GetResult()
    $tokenMatch = [regex]::Match($homeHtml, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"')
    if (-not $tokenMatch.Success) { throw 'Antiforgery token was not found on the home page.' }
    $antiforgeryToken = $tokenMatch.Groups[1].Value

    $acceptedItems = @()
    foreach ($imageName in $ImageNames) {
        $imagePath = Join-Path $imagesDirectory ([IO.Path]::GetFileName($imageName))
        if (-not (Test-Path -LiteralPath $imagePath -PathType Leaf)) {
            throw "Synthetic image not found: $imagePath"
        }

        $multipart = [Net.Http.MultipartFormDataContent]::new()
        try {
            $bytes = [IO.File]::ReadAllBytes($imagePath)
            $fileContent = [Net.Http.ByteArrayContent]::new($bytes)
            $contentType = if ([IO.Path]::GetExtension($imagePath) -ieq '.png') { 'image/png' } else { 'image/jpeg' }
            $fileContent.Headers.ContentType = [Net.Http.Headers.MediaTypeHeaderValue]::Parse($contentType)
            $multipart.Add($fileContent, 'receiptFile', [IO.Path]::GetFileName($imagePath))
            $multipart.Add([Net.Http.StringContent]::new('LIVE-SYNTHETIC'), 'submitterCode')
            $multipart.Add([Net.Http.StringContent]::new('Candidate smoke'), 'submitterDisplayName')
            $multipart.Add([Net.Http.StringContent]::new('Synthetic QA'), 'submitterDepartment')
            $multipart.Add([Net.Http.StringContent]::new($antiforgeryToken), '__RequestVerificationToken')

            $uploadClock = [Diagnostics.Stopwatch]::StartNew()
            $response = $client.PostAsync("$normalizedBaseUrl/Applicant/UploadReceipt", $multipart).GetAwaiter().GetResult()
            $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            $uploadClock.Stop()
            if ([int]$response.StatusCode -ne 202) {
                throw "Upload $imageName expected HTTP 202 but received $([int]$response.StatusCode): $body"
            }
            $accepted = $body | ConvertFrom-Json
            $acceptedItems += [pscustomobject]@{
                ImageName = [IO.Path]::GetFileName($imagePath)
                ImageSha256 = (Get-FileHash -LiteralPath $imagePath -Algorithm SHA256).Hash.ToLowerInvariant()
                Expected = $expected[[IO.Path]::GetFileName($imagePath)]
                HttpStatus = [int]$response.StatusCode
                AcceptedMs = $uploadClock.ElapsedMilliseconds
                CaseId = [string]$accepted.caseId
                StatusUrl = [string]$accepted.statusUrl
            }
        }
        finally {
            $multipart.Dispose()
        }
    }

    $results = @()
    foreach ($item in $acceptedItems) {
        $caseClock = [Diagnostics.Stopwatch]::StartNew()
        do {
            Start-Sleep -Milliseconds 750
            $statusJson = $client.GetStringAsync("$normalizedBaseUrl$($item.StatusUrl)").GetAwaiter().GetResult()
            $status = $statusJson | ConvertFrom-Json
        } while ($status.processingState -in @('PENDING', 'PROCESSING') -and $caseClock.Elapsed -lt [TimeSpan]::FromMinutes(15))
        $caseClock.Stop()

        $results += [pscustomobject]@{
            imageName = $item.ImageName
            imageSha256 = $item.ImageSha256
            caseId = $item.CaseId
            httpStatus = $item.HttpStatus
            acceptedMs = $item.AcceptedMs
            processingState = [string]$status.processingState
            expected = $item.Expected
            actual = [string]$status.actual
            decisionMatchesExpected = ([string]$status.actual -eq $item.Expected)
            latencyMs = [long]$status.latencyMs
            servedProvider = [string]$status.servedProvider
            fallbackUsed = [bool]$status.fallbackUsed
            providerErrorCode = [string]$status.providerErrorCode
            totalAmount = $status.facts.totalAmount
            amountSource = [string]$status.facts.amountSource
            totalAmountSource = [string]$status.facts.totalAmountSource
            requiresPrivacyReview = [bool]$status.facts.requiresPrivacyReview
        }
    }

    $runClock.Stop()
    $completed = @($results | Where-Object processingState -eq 'COMPLETED').Count
    $decisionMatches = @($results | Where-Object decisionMatchesExpected).Count
    $report = [ordered]@{
        schemaVersion = 1
        generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        baseUrl = $normalizedBaseUrl
        testData = 'Repository synthetic Verify fixtures; no real receipt or personal data.'
        claimedAmountSent = $false
        acceptedBeforePolling = $true
        passed = ($completed -eq $results.Count)
        completed = $completed
        decisionMatchesExpected = $decisionMatches
        total = $results.Count
        wallClockMs = $runClock.ElapsedMilliseconds
        results = $results
    }
    $json = $report | ConvertTo-Json -Depth 8
    $json

    if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
        $resolvedOutput = [IO.Path]::GetFullPath($OutputPath)
        $outputDirectory = [IO.Path]::GetDirectoryName($resolvedOutput)
        if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
            [IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
        }
        [IO.File]::WriteAllText($resolvedOutput, $json, [Text.UTF8Encoding]::new($false))
        Write-Host "Saved: $resolvedOutput"
    }

    if (-not $report.passed) { exit 1 }
}
finally {
    $client.Dispose()
    $handler.Dispose()
}
