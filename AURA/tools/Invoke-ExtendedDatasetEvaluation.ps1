[CmdletBinding()]
param(
    [Parameter()]
    [ValidatePattern('^https?://')]
    [string]$BaseUrl = 'http://localhost:5000',

    [Parameter()]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string]$ManifestPath = (Join-Path $PSScriptRoot '..\test_kit\judge-manifest.json'),

    [Parameter()]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })]
    [string]$ImagesDirectory = (Join-Path $PSScriptRoot '..\test_kit\images'),

    [Parameter()]
    [ValidateRange(1, 30)]
    [int]$MaxCases = 15,

    [Parameter()]
    [ValidateRange(0, 30)]
    [int]$InterCaseDelaySeconds = 4,

    [Parameter()]
    [ValidateRange(1, 30)]
    [int]$CaseTimeoutMinutes = 10,

    [Parameter()]
    [string]$OutputDirectory = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-CaseValue {
    param(
        [Parameter(Mandatory)] [object]$Case,
        [Parameter(Mandatory)] [string[]]$Names,
        [Parameter()] $Default = $null
    )

    foreach ($name in $Names) {
        $property = $Case.PSObject.Properties[$name]
        if ($null -ne $property) { return $property.Value }
    }
    return $Default
}

function Get-Percentile {
    param(
        [Parameter()] [object[]]$Values,
        [Parameter(Mandatory)] [double]$Percentile
    )

    $numbers = @($Values | Where-Object { $null -ne $_ } | ForEach-Object { [double]$_ } | Sort-Object)
    if ($numbers.Count -eq 0) { return $null }
    $index = [Math]::Max(0, [Math]::Ceiling($numbers.Count * $Percentile) - 1)
    return [long][Math]::Round($numbers[$index])
}

function Convert-ComparableValue {
    param([Parameter()] $Value)
    if ($null -eq $Value) { return $null }
    if ($Value -is [string]) {
        $normalized = $Value.Trim()
        return $(if ($normalized.Length -eq 0) { $null } else { $normalized })
    }
    if ($Value -is [decimal] -or $Value -is [double] -or $Value -is [float] -or
        $Value -is [int] -or $Value -is [long]) {
        return [Convert]::ToDecimal($Value, [Globalization.CultureInfo]::InvariantCulture).ToString(
            [Globalization.CultureInfo]::InvariantCulture)
    }
    if ($Value -is [bool]) { return $Value.ToString().ToLowerInvariant() }
    return ([string]$Value).Trim()
}

$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$resolvedManifest = (Resolve-Path -LiteralPath $ManifestPath).Path
$resolvedImages = (Resolve-Path -LiteralPath $ImagesDirectory).Path
$normalizedBaseUrl = $BaseUrl.TrimEnd('/')

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $runStamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $OutputDirectory = Join-Path $projectRoot "test_kit\results\$runStamp"
}
$resolvedOutput = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $resolvedOutput | Out-Null

$manifest = Get-Content -Raw -LiteralPath $resolvedManifest | ConvertFrom-Json
$manifestCases = @($(if ($null -ne $manifest.PSObject.Properties['cases']) { $manifest.cases } else { $manifest }))
if ($manifestCases.Count -eq 0) { throw 'Manifest không có test case.' }

$sourceManifestPath = $null
$sourceManifestSha256 = $null
$sourceCasesById = @{}
$sourceManifestProperty = $manifest.PSObject.Properties['sourceManifest']
if ($null -ne $sourceManifestProperty -and -not [string]::IsNullOrWhiteSpace([string]$sourceManifestProperty.Value)) {
    $sourceManifestCandidate = Join-Path (Split-Path -Parent $resolvedManifest) ([string]$sourceManifestProperty.Value)
    if (Test-Path -LiteralPath $sourceManifestCandidate -PathType Leaf) {
        $sourceManifestPath = (Resolve-Path -LiteralPath $sourceManifestCandidate).Path
        $sourceManifestSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $sourceManifestPath).Hash
        $sourceManifest = Get-Content -Raw -LiteralPath $sourceManifestPath | ConvertFrom-Json
        $sourceCases = @($(if ($null -ne $sourceManifest.PSObject.Properties['cases']) {
            $sourceManifest.cases
        } else {
            $sourceManifest
        }))
        foreach ($sourceCase in $sourceCases) {
            $sourceCaseId = [string](Get-CaseValue $sourceCase @('id'))
            if (-not [string]::IsNullOrWhiteSpace($sourceCaseId)) {
                $sourceCasesById[$sourceCaseId] = $sourceCase
            }
        }
    }
}

$selectedCases = @($manifestCases | Select-Object -First $MaxCases)
if ($selectedCases.Count -lt $MaxCases) {
    Write-Warning "Manifest chỉ có $($selectedCases.Count) ca; MaxCases=$MaxCases."
}

Add-Type -AssemblyName System.Net.Http
$handler = [Net.Http.HttpClientHandler]::new()
$handler.CookieContainer = [Net.CookieContainer]::new()
$client = [Net.Http.HttpClient]::new($handler)
$client.Timeout = [TimeSpan]::FromMinutes($CaseTimeoutMinutes + 1)

$runStartedAt = [DateTimeOffset]::UtcNow
$results = [Collections.Generic.List[object]]::new()

try {
    $homeHtml = $client.GetStringAsync("$normalizedBaseUrl/").GetAwaiter().GetResult()
    $tokenMatch = [regex]::Match($homeHtml, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"')
    if (-not $tokenMatch.Success) { throw 'Không tìm thấy antiforgery token trên trang chủ.' }
    $antiforgeryToken = $tokenMatch.Groups[1].Value

    $healthJson = $client.GetStringAsync("$normalizedBaseUrl/healthz").GetAwaiter().GetResult()
    $health = $healthJson | ConvertFrom-Json

    for ($index = 0; $index -lt $selectedCases.Count; $index++) {
        $testCase = $selectedCases[$index]
        $caseId = [string](Get-CaseValue $testCase @('id'))
        $fileName = [string](Get-CaseValue $testCase @('fileName', 'file_name'))
        $expectedStatus = [string](Get-CaseValue $testCase @('expectedStatus', 'expected_status'))
        $claimedAmount = [decimal](Get-CaseValue $testCase @('claimedAmount', 'claimed_amount'))
        $expectedFacts = Get-CaseValue $testCase @('expectedFacts', 'expected_facts')
        if ($null -eq $expectedFacts -and $sourceCasesById.ContainsKey($caseId)) {
            $expectedFacts = Get-CaseValue $sourceCasesById[$caseId] @('expectedFacts', 'expected_facts')
        }
        $imagePath = [IO.Path]::GetFullPath((Join-Path $resolvedImages ([IO.Path]::GetFileName($fileName))))

        if (-not $imagePath.StartsWith($resolvedImages, [StringComparison]::OrdinalIgnoreCase) -or
            -not (Test-Path -LiteralPath $imagePath -PathType Leaf)) {
            throw "Không tìm thấy ảnh hợp lệ cho $caseId`: $fileName"
        }

        if ($index -gt 0 -and $InterCaseDelaySeconds -gt 0) {
            Start-Sleep -Seconds $InterCaseDelaySeconds
        }

        $clock = [Diagnostics.Stopwatch]::StartNew()
        $acceptedMs = $null
        $status = $null
        $clientError = $null

        try {
            $multipart = [Net.Http.MultipartFormDataContent]::new()
            try {
                $bytes = [IO.File]::ReadAllBytes($imagePath)
                $fileContent = [Net.Http.ByteArrayContent]::new($bytes)
                $mediaType = $(if ([IO.Path]::GetExtension($imagePath) -ieq '.png') { 'image/png' } else { 'image/jpeg' })
                $fileContent.Headers.ContentType = [Net.Http.Headers.MediaTypeHeaderValue]::Parse($mediaType)
                $multipart.Add($fileContent, 'receiptFile', [IO.Path]::GetFileName($imagePath))
                $multipart.Add([Net.Http.StringContent]::new($claimedAmount.ToString([Globalization.CultureInfo]::InvariantCulture)), 'claimedAmount')
                $multipart.Add([Net.Http.StringContent]::new("EVAL-$caseId"), 'submitterCode')
                $multipart.Add([Net.Http.StringContent]::new('Extended Dataset Evaluation'), 'submitterDisplayName')
                $multipart.Add([Net.Http.StringContent]::new('Quality Assurance'), 'submitterDepartment')
                $multipart.Add([Net.Http.StringContent]::new($antiforgeryToken), '__RequestVerificationToken')

                $response = $client.PostAsync("$normalizedBaseUrl/Applicant/UploadReceipt", $multipart).GetAwaiter().GetResult()
                $acceptedMs = $clock.ElapsedMilliseconds
                $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                if ([int]$response.StatusCode -ne 202) {
                    throw "Expected HTTP 202, nhận HTTP $([int]$response.StatusCode): $body"
                }
                $accepted = $body | ConvertFrom-Json
            }
            finally {
                $multipart.Dispose()
            }

            $deadline = [DateTimeOffset]::UtcNow.AddMinutes($CaseTimeoutMinutes)
            do {
                if ([DateTimeOffset]::UtcNow -ge $deadline) {
                    throw "Quá thời gian chờ $CaseTimeoutMinutes phút."
                }
                Start-Sleep -Milliseconds 500
                $statusJson = $client.GetStringAsync("$normalizedBaseUrl$($accepted.statusUrl)").GetAwaiter().GetResult()
                $status = $statusJson | ConvertFrom-Json
            } while ($status.processingState -in @('PENDING', 'PROCESSING'))
        }
        catch {
            $clientError = $_.Exception.Message
        }
        finally {
            $clock.Stop()
        }

        $actualStatus = $(if ($null -ne $status) { [string]$status.actual } else { 'CLIENT_ERROR' })
        $decisionCorrect = $actualStatus -eq $expectedStatus
        $fieldCorrect = 0
        $fieldTotal = 0

        if ($null -ne $expectedFacts -and $null -ne $status -and $null -ne $status.facts) {
            foreach ($expectedProperty in $expectedFacts.PSObject.Properties) {
                $fieldTotal++
                $actualProperty = $status.facts.PSObject.Properties[$expectedProperty.Name]
                $actualValue = $(if ($null -ne $actualProperty) { $actualProperty.Value } else { $null })
                if ((Convert-ComparableValue $expectedProperty.Value) -eq (Convert-ComparableValue $actualValue)) {
                    $fieldCorrect++
                }
            }
        }

        $results.Add([pscustomobject]@{
            Sequence = $index + 1
            DatasetCaseId = $caseId
            RequestId = $(if ($null -ne $status) { $status.caseId } else { $null })
            FileName = $fileName
            Expected = $expectedStatus
            Actual = $actualStatus
            Pass = $decisionCorrect
            ProcessingState = $(if ($null -ne $status) { $status.processingState } else { 'CLIENT_ERROR' })
            HttpAcceptedMs = $acceptedMs
            EndToEndMs = $clock.ElapsedMilliseconds
            ProcessingLatencyMs = $(if ($null -ne $status) { $status.latencyMs } else { $null })
            PrimaryProvider = $(if ($null -ne $status) { $status.primaryProvider } else { $null })
            ServedProvider = $(if ($null -ne $status) { $status.servedProvider } else { $null })
            FallbackUsed = $(if ($null -ne $status) { $status.fallbackUsed } else { $false })
            ProviderErrorCode = $(if ($null -ne $status) { $status.providerErrorCode } else { $null })
            FieldCorrect = $fieldCorrect
            FieldTotal = $fieldTotal
            Reason = $(if ($null -ne $status) { $status.reason } else { $clientError })
            ExpectedFactsJson = $(if ($null -ne $expectedFacts) { $expectedFacts | ConvertTo-Json -Compress -Depth 10 } else { $null })
            ActualFactsJson = $(if ($null -ne $status -and $null -ne $status.facts) { $status.facts | ConvertTo-Json -Compress -Depth 10 } else { $null })
        })
    }

    $runCompletedAt = [DateTimeOffset]::UtcNow
    $decisionPassCount = @($results | Where-Object Pass).Count
    $expectedEscalations = @($results | Where-Object { $_.Expected -like 'ESCALATE_*' })
    $expectedRoutine = @($results | Where-Object Expected -eq 'AUTO_APPROVE')
    $missedEscalations = @($expectedEscalations | Where-Object Actual -eq 'AUTO_APPROVE').Count
    $overEscalations = @($expectedRoutine | Where-Object { $_.Actual -like 'ESCALATE_*' -and $_.Actual -ne 'ESCALATE_SYSTEM_ERROR' }).Count
    $systemErrors = @($results | Where-Object Actual -eq 'ESCALATE_SYSTEM_ERROR').Count
    $fallbackAttempts = @($results | Where-Object FallbackUsed).Count
    $fallbackSuccesses = @($results | Where-Object { $_.FallbackUsed -and $_.ProcessingState -eq 'COMPLETED' }).Count
    $fieldCorrectTotal = ($results | Measure-Object -Property FieldCorrect -Sum).Sum
    $fieldCountTotal = ($results | Measure-Object -Property FieldTotal -Sum).Sum

    $summary = [pscustomobject]@{
        TotalCases = $results.Count
        DecisionPassCount = $decisionPassCount
        DecisionAccuracy = $(if ($results.Count -gt 0) { [Math]::Round($decisionPassCount / $results.Count, 4) } else { $null })
        MissedEscalations = $missedEscalations
        MissedEscalationRate = $(if ($expectedEscalations.Count -gt 0) { [Math]::Round($missedEscalations / $expectedEscalations.Count, 4) } else { $null })
        OverEscalations = $overEscalations
        OverEscalationRate = $(if ($expectedRoutine.Count -gt 0) { [Math]::Round($overEscalations / $expectedRoutine.Count, 4) } else { $null })
        SystemErrors = $systemErrors
        SystemErrorRate = $(if ($results.Count -gt 0) { [Math]::Round($systemErrors / $results.Count, 4) } else { $null })
        FieldCorrect = $fieldCorrectTotal
        FieldTotal = $fieldCountTotal
        FieldExactMatch = $(if ($fieldCountTotal -gt 0) { [Math]::Round($fieldCorrectTotal / $fieldCountTotal, 4) } else { $null })
        AcceptedP50Ms = Get-Percentile @($results.HttpAcceptedMs) 0.50
        AcceptedP95Ms = Get-Percentile @($results.HttpAcceptedMs) 0.95
        EndToEndP50Ms = Get-Percentile @($results.EndToEndMs) 0.50
        EndToEndP95Ms = Get-Percentile @($results.EndToEndMs) 0.95
        FallbackAttempts = $fallbackAttempts
        FallbackSuccesses = $fallbackSuccesses
    }

    $commit = $null
    try { $commit = (git -C $projectRoot rev-parse HEAD 2>$null).Trim() } catch { }
    $metadata = [pscustomobject]@{
        RunStartedAtUtc = $runStartedAt.ToString('O')
        RunCompletedAtUtc = $runCompletedAt.ToString('O')
        Commit = $commit
        BaseUrl = $normalizedBaseUrl
        ManifestPath = $resolvedManifest
        ManifestSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $resolvedManifest).Hash
        SourceManifestPath = $sourceManifestPath
        SourceManifestSha256 = $sourceManifestSha256
        ImagesDirectory = $resolvedImages
        RequestedMaxCases = $MaxCases
        InterCaseDelaySeconds = $InterCaseDelaySeconds
        Health = $health
    }

    $results | Export-Csv -LiteralPath (Join-Path $resolvedOutput 'results.csv') -NoTypeInformation -Encoding utf8
    $results | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $resolvedOutput 'results.json') -Encoding utf8
    $summary | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $resolvedOutput 'summary.json') -Encoding utf8
    $metadata | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $resolvedOutput 'metadata.json') -Encoding utf8

    $results | Select-Object DatasetCaseId, Expected, Actual, Pass, EndToEndMs, ServedProvider, FallbackUsed, ProviderErrorCode |
        Format-Table -AutoSize
    $summary | Format-List
    "Evidence directory: $resolvedOutput"
}
finally {
    $client.Dispose()
    $handler.Dispose()
}
