[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ResultsPath,

    [Parameter(Mandatory)]
    [string]$ManifestPath,

    [Parameter(Mandatory)]
    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Read-Utf8Json {
    param([Parameter(Mandatory)] [string]$Path)
    $resolved = (Resolve-Path -LiteralPath $Path).Path
    $utf8Strict = [Text.UTF8Encoding]::new($false, $true)
    return [IO.File]::ReadAllText($resolved, $utf8Strict) | ConvertFrom-Json
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

$resolvedResults = (Resolve-Path -LiteralPath $ResultsPath).Path
$resolvedManifest = (Resolve-Path -LiteralPath $ManifestPath).Path
$results = Read-Utf8Json $resolvedResults
$manifest = Read-Utf8Json $resolvedManifest
$manifestCases = @($manifest.cases)
$manifestById = @{}
foreach ($testCase in $manifestCases) {
    $manifestById[[string]$testCase.id] = $testCase
}

$caseReports = [Collections.Generic.List[object]]::new()
$correctTotal = 0
$fieldTotal = 0

foreach ($result in $results) {
    $caseId = [string]$result.DatasetCaseId
    if (-not $manifestById.ContainsKey($caseId)) {
        throw "Result case '$caseId' is missing from the manifest."
    }

    $testCase = $manifestById[$caseId]
    $expectedFacts = $testCase.expectedFacts
    if ($null -eq $expectedFacts) { $expectedFacts = $testCase.expected_facts }
    $actualFacts = if ([string]::IsNullOrWhiteSpace([string]$result.ActualFactsJson)) {
        $null
    } else {
        [string]$result.ActualFactsJson | ConvertFrom-Json
    }

    $mismatches = [Collections.Generic.List[object]]::new()
    $caseCorrect = 0
    $caseTotal = 0
    foreach ($expectedProperty in $expectedFacts.PSObject.Properties) {
        $caseTotal++
        $expectedValue = Convert-ComparableValue $expectedProperty.Value
        $actualProperty = if ($null -eq $actualFacts) { $null } else {
            $actualFacts.PSObject.Properties[$expectedProperty.Name]
        }
        $actualValue = if ($null -eq $actualProperty) { $null } else {
            Convert-ComparableValue $actualProperty.Value
        }

        if ($expectedValue -ceq $actualValue) {
            $caseCorrect++
        } else {
            $mismatches.Add([pscustomobject]@{
                Field = $expectedProperty.Name
                Expected = $expectedValue
                Actual = $actualValue
            })
        }
    }

    $correctTotal += $caseCorrect
    $fieldTotal += $caseTotal
    $caseReports.Add([pscustomobject]@{
        DatasetCaseId = $caseId
        FieldCorrect = $caseCorrect
        FieldTotal = $caseTotal
        Mismatches = @($mismatches)
    })
}

$report = [pscustomobject]@{
    SchemaVersion = 1
    Purpose = 'Offline UTF-8-safe field re-evaluation; no HTTP or AI request is performed.'
    MeasuredAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    ResultsPath = $resolvedResults
    ResultsSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $resolvedResults).Hash
    ManifestPath = $resolvedManifest
    ManifestSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $resolvedManifest).Hash
    TotalCases = $caseReports.Count
    FieldCorrect = $correctTotal
    FieldTotal = $fieldTotal
    FieldExactMatch = if ($fieldTotal -gt 0) { [Math]::Round($correctTotal / $fieldTotal, 4) } else { $null }
    Cases = @($caseReports)
}

$resolvedOutput = [IO.Path]::GetFullPath($OutputPath)
$parent = Split-Path -Parent $resolvedOutput
if (-not [string]::IsNullOrWhiteSpace($parent)) {
    New-Item -ItemType Directory -Force -Path $parent | Out-Null
}
$json = $report | ConvertTo-Json -Depth 10
[IO.File]::WriteAllText($resolvedOutput, $json, [Text.UTF8Encoding]::new($false))
$report | Select-Object TotalCases, FieldCorrect, FieldTotal, FieldExactMatch | Format-List
"Saved: $resolvedOutput"
