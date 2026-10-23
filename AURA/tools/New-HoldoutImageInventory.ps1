[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ImageDirectory,

    [Parameter(Mandatory)]
    [string]$OutputPath,

    [Parameter()]
    [ValidateRange(0, 10000)]
    [int]$ExpectedCount = 0,

    [Parameter()]
    [switch]$RequireUniqueContent
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $ImageDirectory -PathType Container)) {
    throw "Image directory not found: $ImageDirectory"
}

$resolvedDirectory = (Resolve-Path -LiteralPath $ImageDirectory).Path
$allowedExtensions = @('.jpg', '.jpeg', '.png')
$files = @(Get-ChildItem -LiteralPath $resolvedDirectory -File |
    Where-Object { $allowedExtensions -contains $_.Extension.ToLowerInvariant() } |
    Sort-Object Name)

if ($files.Count -eq 0) {
    throw "No JPG/PNG images found in: $resolvedDirectory"
}
if ($ExpectedCount -gt 0 -and $files.Count -ne $ExpectedCount) {
    throw "Expected $ExpectedCount images; found $($files.Count)."
}

$items = foreach ($file in $files) {
    [pscustomobject]@{
        fileName = $file.Name
        sizeBytes = $file.Length
        sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        lastWriteTimeUtc = $file.LastWriteTimeUtc.ToString('o')
    }
}
$distinctHashCount = @($items | Select-Object -ExpandProperty sha256 -Unique).Count
$duplicateContentCount = $items.Count - $distinctHashCount
if ($RequireUniqueContent -and $duplicateContentCount -gt 0) {
    throw "Unique-content gate failed: $duplicateContentCount byte-identical copies among $($items.Count) images. No inventory was written."
}

$report = [pscustomobject]@{
    schemaVersion = 1
    status = 'PRELIMINARY_NOT_LOCKED'
    warning = 'Hashes only. This is not consent, ground truth, or approval to call an AI provider.'
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    imageDirectory = $resolvedDirectory
    imageCount = $items.Count
    distinctHashCount = $distinctHashCount
    duplicateContentCount = $duplicateContentCount
    uniqueContentGatePassed = ($duplicateContentCount -eq 0)
    images = @($items)
}

$resolvedOutput = [IO.Path]::GetFullPath($OutputPath)
$parent = Split-Path -Parent $resolvedOutput
if (-not [string]::IsNullOrWhiteSpace($parent)) {
    New-Item -ItemType Directory -Force -Path $parent | Out-Null
}

$json = $report | ConvertTo-Json -Depth 6
[IO.File]::WriteAllText($resolvedOutput, $json, [Text.UTF8Encoding]::new($false))
$json
Write-Host "Saved: $resolvedOutput"
