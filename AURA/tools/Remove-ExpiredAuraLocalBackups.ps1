#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$BackupRoot,
    [switch]$Apply,
    [DateTimeOffset]$NowUtc = [DateTimeOffset]::UtcNow
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($BackupRoot).TrimEnd('\', '/')
$separator = [IO.Path]::DirectorySeparatorChar
$rootPrefix = $root + $separator
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$repoPrefix = $repoRoot.TrimEnd('\', '/') + $separator
if (-not (Test-Path -LiteralPath $root -PathType Container) -or
    $root.StartsWith($repoPrefix, [StringComparison]::OrdinalIgnoreCase) -or
    $root.Equals($repoRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'BackupRoot must be an existing directory outside the Git repository.'
}
$rootItem = Get-Item -LiteralPath $root -Force
if (($rootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
    throw 'BackupRoot cannot be a reparse point.'
}

$expiredCount = 0
$skippedCount = 0
foreach ($candidate in Get-ChildItem -LiteralPath $root -Directory -Force) {
    if ($candidate.Name -notmatch '^AURA_BACKUP_[A-Za-z0-9_-]+$') { continue }
    $target = [IO.Path]::GetFullPath($candidate.FullName)
    if (-not $target.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetDirectoryName($target).TrimEnd('\', '/') -ne $root -or
        ($candidate.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        $skippedCount++
        continue
    }

    $manifestPath = Join-Path $target '.aura-retention.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { $skippedCount++; continue }
    try {
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        if ($manifest.format -ne 'AURA_LOCAL_BACKUP_RETENTION_V1' -or
            $manifest.backupSetName -ne $candidate.Name -or
            $manifest.retentionDays -ne 7 -or
            [int]$manifest.receiptCount -ne @($manifest.receipts).Count -or
            [string]::IsNullOrWhiteSpace($manifest.purgeAtUtc)) {
            $skippedCount++
            continue
        }
        $dbRelative = [string]$manifest.databaseBackup.path
        if ([IO.Path]::GetFileName($dbRelative) -ne $dbRelative -or
            [string]::IsNullOrWhiteSpace($dbRelative)) { $skippedCount++; continue }
        $unsafeReceiptPath = $false
        foreach ($receipt in @($manifest.receipts)) {
            $relative = [string]$receipt.path
            if (-not $relative.StartsWith('receipts/', [StringComparison]::Ordinal) -or
                [IO.Path]::GetFileName($relative) -ne $relative.Substring('receipts/'.Length) -or
                $relative.Substring('receipts/'.Length).IndexOfAny([char[]]@('/', '\', ':')) -ge 0 -or
                [string]::IsNullOrWhiteSpace($relative.Substring('receipts/'.Length)) -or
                [long]$receipt.sizeBytes -lt 1 -or [string]$receipt.sha256 -notmatch '^[0-9A-Fa-f]{64}$') {
                $unsafeReceiptPath = $true
                break
            }
        }
        if ($unsafeReceiptPath) { $skippedCount++; continue }
        $receiptPathKeys = @($manifest.receipts | ForEach-Object { [string]$_.path } | Sort-Object -Unique)
        if ($receiptPathKeys.Count -ne @($manifest.receipts).Count) { $skippedCount++; continue }
        $createdAt = [DateTimeOffset]::Parse($manifest.createdAtUtc).ToUniversalTime()
        $expectedPurgeAt = $createdAt.AddDays(7)
        $earliestExpiry = $null
        foreach ($receipt in @($manifest.receipts)) {
            if ($receipt.retentionExpiresAtUtc) {
                $deadline = [DateTimeOffset]::Parse($receipt.retentionExpiresAtUtc).ToUniversalTime()
                if ($null -eq $earliestExpiry -or $deadline -lt $earliestExpiry) { $earliestExpiry = $deadline }
            }
        }
        if ($manifest.earliestIncludedDataExpiryUtc) {
            $recordedEarliest = [DateTimeOffset]::Parse($manifest.earliestIncludedDataExpiryUtc).ToUniversalTime()
            if ($null -eq $earliestExpiry -or $recordedEarliest -ne $earliestExpiry) { $skippedCount++; continue }
            if ($recordedEarliest -lt $expectedPurgeAt) { $expectedPurgeAt = $recordedEarliest }
        }
        elseif ($null -ne $earliestExpiry) { $skippedCount++; continue }
        if ([DateTimeOffset]::Parse($manifest.purgeAtUtc).ToUniversalTime() -ne $expectedPurgeAt) {
            $skippedCount++
            continue
        }
        $purgeAt = [DateTimeOffset]::Parse($manifest.purgeAtUtc).ToUniversalTime()
        if ($purgeAt -gt $NowUtc.ToUniversalTime()) { continue }

        $expected = @($manifest.databaseBackup.path)
        foreach ($receipt in @($manifest.receipts)) { $expected += [string]$receipt.path }
        $expected += '.aura-retention.json'
        $expected = @($expected | Sort-Object -Unique)
        $actual = @(Get-ChildItem -LiteralPath $target -File -Recurse -Force | ForEach-Object {
            $file = Get-Item -LiteralPath $_.FullName -Force
            if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Reparse file.' }
            $_.FullName.Substring($target.Length + 1).Replace('\', '/')
        } | Sort-Object -Unique)
        $actualDirectories = @(Get-ChildItem -LiteralPath $target -Directory -Recurse -Force | ForEach-Object {
            $relative = $_.FullName.Substring($target.Length + 1).Replace('\', '/')
            if (($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Reparse directory.' }
            $relative
        } | Sort-Object -Unique)
        if ((Compare-Object $expected $actual) -or
            (Compare-Object @('receipts') $actualDirectories)) { $skippedCount++; continue }

        $dbPath = Join-Path $target ([string]$manifest.databaseBackup.path.Replace('/', '\'))
        $dbItem = Get-Item -LiteralPath $dbPath -Force
        if (($dbItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
            $dbItem.Length -ne [long]$manifest.databaseBackup.sizeBytes -or
            (Get-FileHash -LiteralPath $dbPath -Algorithm SHA256).Hash -ne [string]$manifest.databaseBackup.sha256) {
            $skippedCount++
            continue
        }
        $allHashesMatch = $true
        foreach ($receipt in @($manifest.receipts)) {
            $filePath = Join-Path $target ([string]$receipt.path.Replace('/', '\'))
            $item = Get-Item -LiteralPath $filePath -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
                $item.Length -ne [long]$receipt.sizeBytes -or
                (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash -ne [string]$receipt.sha256) {
                $allHashesMatch = $false
                break
            }
        }
        if (-not $allHashesMatch) { $skippedCount++; continue }

        if ($Apply) {
            Remove-Item -LiteralPath $target -Recurse -Force
            Write-Output ("PURGED {0}; expiredAtUtc={1}." -f $candidate.Name, $purgeAt.ToString('o'))
        }
        else {
            Write-Output ("EXPIRED (report only) {0}; expiredAtUtc={1}." -f $candidate.Name, $purgeAt.ToString('o'))
        }
        $expiredCount++
    }
    catch {
        $skippedCount++
        Write-Warning ("Skipped backup set {0}: manifest, path or hash validation failed." -f $candidate.Name)
    }
}

Write-Output ("Retention backup scan complete; expired={0}; skippedOrUnmanaged={1}; apply={2}." -f
    $expiredCount, $skippedCount, [bool]$Apply)
