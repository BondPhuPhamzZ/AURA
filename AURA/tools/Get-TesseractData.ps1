#requires -Version 5.1
<#
.SYNOPSIS
    Downloads the pinned Tesseract language data used by the optional local evidence localizer.

.DESCRIPTION
    Fetches vie/eng "tessdata_fast" models (Apache-2.0) from the official tesseract-ocr GitHub
    repository at a fixed commit and refuses any file whose SHA-256 differs from the pinned value.
    The default destination is the Git-ignored .tmp\tessdata folder of this project.
#>
[CmdletBinding()]
param(
    [string]$Destination
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
# Windows PowerShell 5.1 does not expose $PSScriptRoot to parameter defaults.
if ([string]::IsNullOrWhiteSpace($Destination)) {
    $Destination = Join-Path ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))) '.tmp\tessdata'
}
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$commit = '87416418657359cb625c412a48b6e1d6d41c29bd'
$files = [ordered]@{
    'vie.traineddata' = '79DF64CAF7BCFB2A27DF5042ECB6121E196EADA34DA774956995747636D5BFA1'
    'eng.traineddata' = '7D4322BD2A7749724879683FC3912CB542F19906C83BCC1A52132556427170B2'
}

New-Item -ItemType Directory -Path $Destination -Force | Out-Null
foreach ($name in $files.Keys) {
    $target = Join-Path $Destination $name
    if ((Test-Path -LiteralPath $target) -and
        (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -eq $files[$name]) {
        Write-Output "$name already present and verified."
        continue
    }
    $temporary = $target + '.download'
    try {
        Invoke-WebRequest -UseBasicParsing -Uri "https://github.com/tesseract-ocr/tessdata_fast/raw/$commit/$name" `
            -OutFile $temporary
        $hash = (Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash
        if ($hash -ne $files[$name]) { throw "$name SHA-256 mismatch; the file was not installed." }
        Move-Item -LiteralPath $temporary -Destination $target -Force
        Write-Output "$name downloaded and verified."
    }
    finally {
        if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
    }
}
Write-Output ('Tessdata ready: ' + (Resolve-Path -LiteralPath $Destination))
