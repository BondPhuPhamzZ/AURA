[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ImageDirectory,

    [Parameter(Mandatory)]
    [string]$OutputPath
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

$items = foreach ($file in $files) {
    [pscustomobject]@{
        fileName = $file.Name
        sizeBytes = $file.Length
        sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        lastWriteTimeUtc = $file.LastWriteTimeUtc.ToString('o')
    }
}

$report = [pscustomobject]@{
    schemaVersion = 1
    status = 'PRELIMINARY_NOT_LOCKED'
    warning = 'Hashes only. This is not consent, ground truth, or approval to call an AI provider.'
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    imageDirectory = $resolvedDirectory
    imageCount = $items.Count
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
