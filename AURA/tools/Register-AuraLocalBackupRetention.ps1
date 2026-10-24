#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$BackupSetDirectory,
    [Parameter(Mandatory = $true)][string]$DatabaseBackupFile,
    [Parameter(Mandatory = $true)][string]$ReceiptSourceDirectory,
    [Parameter(Mandatory = $true)][string]$ReceiptBackupDirectory,
    [switch]$ConfirmApplicationStopped,
    [string]$ConnectionString
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $ConfirmApplicationStopped) {
    throw 'Stop the AURA app and all database writers, then pass -ConfirmApplicationStopped.'
}

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$setRoot = [IO.Path]::GetFullPath($BackupSetDirectory)
$dbBackup = [IO.Path]::GetFullPath($DatabaseBackupFile)
$receiptSource = [IO.Path]::GetFullPath($ReceiptSourceDirectory)
$receiptBackup = [IO.Path]::GetFullPath($ReceiptBackupDirectory)
$separator = [IO.Path]::DirectorySeparatorChar
$repoPrefix = $repoRoot.TrimEnd('\', '/') + $separator
$setPrefix = $setRoot.TrimEnd('\', '/') + $separator

if ($setRoot.StartsWith($repoPrefix, [StringComparison]::OrdinalIgnoreCase) -or
    $setRoot.Equals($repoRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Backup sets must be outside the Git repository.'
}
if ((Split-Path -Leaf $setRoot) -notmatch '^AURA_BACKUP_[A-Za-z0-9_-]+$') {
    throw 'Backup set folder must use the AURA_BACKUP_<timestamp>_<id> naming pattern.'
}
if (-not (Test-Path -LiteralPath $setRoot -PathType Container) -or
    -not (Test-Path -LiteralPath $dbBackup -PathType Leaf) -or
    -not (Test-Path -LiteralPath $receiptSource -PathType Container) -or
    -not (Test-Path -LiteralPath $receiptBackup -PathType Container)) {
    throw 'A required backup or receipt directory is missing.'
}
if (-not $dbBackup.StartsWith($setPrefix, [StringComparison]::OrdinalIgnoreCase) -or
    -not $receiptBackup.StartsWith($setPrefix, [StringComparison]::OrdinalIgnoreCase) -or
    (Split-Path -Leaf $receiptBackup) -ne 'receipts') {
    throw 'The database backup and receipts folder must be inside the selected backup set.'
}
$manifestPath = Join-Path $setRoot '.aura-retention.json'
if (Test-Path -LiteralPath $manifestPath) {
    throw 'This backup set already has a retention manifest; it will not be overwritten.'
}
$topLevel = @(Get-ChildItem -LiteralPath $setRoot -Force)
if ($topLevel.Count -ne 2 -or
    -not ($topLevel.Name -contains (Split-Path -Leaf $dbBackup)) -or
    -not ($topLevel.Name -contains 'receipts')) {
    throw 'Backup set must contain only the database backup file and the receipts folder before registration.'
}
foreach ($path in @($setRoot, $dbBackup, $receiptSource, $receiptBackup)) {
    $item = Get-Item -LiteralPath $path -Force
    if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw 'Reparse points are not allowed in backup/source roots.'
    }
}
foreach ($directory in @($receiptSource, $receiptBackup)) {
    $children = @(Get-ChildItem -LiteralPath $directory -Force)
    if ($children | Where-Object {
        $_.PSIsContainer -or (($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)
    }) {
        throw 'Receipt storage must be flat and contain only ordinary files.'
    }
}

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    $ConnectionString = $env:AURA_BACKUP_CONNECTION_STRING
}
if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    $projectFile = Join-Path $PSScriptRoot '..\AURA.csproj'
    $secretLines = @(& dotnet user-secrets list --project $projectFile 2>$null)
    $connectionLine = $secretLines | Where-Object { $_ -match '^ConnectionStrings:DefaultConnection\s*=' } | Select-Object -First 1
    if ($connectionLine -match '^[^=]+=\s*(.*)$') { $ConnectionString = $matches[1] }
}
if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    throw 'No database connection string was found. Set AURA_BACKUP_CONNECTION_STRING without printing it.'
}

$connection = $null
try {
    Add-Type -AssemblyName System.Data
    $connection = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
    $connection.Open()
    $requestCommand = $connection.CreateCommand()
    $requestCommand.CommandText = @'
SELECT [StoredFileName], [FileSizeBytes], [FileSha256], [RetentionExpiresAt]
FROM [dbo].[ReimbursementRequests];
'@
    $reader = $requestCommand.ExecuteReader()
    $requestFiles = @()
    while ($reader.Read()) {
        $requestFiles += [pscustomobject]@{
            Name = [string]$reader['StoredFileName']
            Size = [long]$reader['FileSizeBytes']
            Sha256 = ([string]$reader['FileSha256']).ToUpperInvariant()
            RetentionExpiresAtUtc = if ($reader['RetentionExpiresAt'] -is [DBNull]) { $null } else {
                [DateTime]::SpecifyKind([DateTime]$reader['RetentionExpiresAt'], [DateTimeKind]::Utc).ToString('o')
            }
        }
    }
    $reader.Close()
    $connection.Close()
}
catch {
    if ($connection) { $connection.Dispose() }
    throw 'Could not read the retention metadata from SQL. Confirm the retention migration is applied; connection details are suppressed.'
}

$createdUtc = (Get-Item -LiteralPath $dbBackup).LastWriteTimeUtc
$minimumDeadline = $null
foreach ($entry in $requestFiles) {
    if ([string]::IsNullOrWhiteSpace($entry.Name) -or
        $entry.Name.IndexOfAny([char[]]@('/', '\', ':')) -ge 0 -or
        [IO.Path]::GetFileName($entry.Name) -ne $entry.Name) {
        throw 'The database contains an unsafe stored filename; backup registration stopped.'
    }
    if ($entry.Size -lt 1 -or $entry.Sha256 -notmatch '^[0-9A-F]{64}$') {
        throw 'The database contains invalid receipt integrity metadata; backup registration stopped.'
    }
    $sourcePath = Join-Path $receiptSource $entry.Name
    $copyPath = Join-Path $receiptBackup $entry.Name
    foreach ($path in @($sourcePath, $copyPath)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw 'A receipt referenced by SQL is missing from the source or backup copy.'
        }
        $file = Get-Item -LiteralPath $path -Force
        if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
            $file.Length -ne $entry.Size -or
            (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.Sha256) {
            throw 'A receipt source/copy failed size or SHA-256 verification.'
        }
    }
    if ($entry.RetentionExpiresAtUtc) {
        $deadline = [DateTimeOffset]::Parse($entry.RetentionExpiresAtUtc).UtcDateTime
        if ($deadline -le $createdUtc) {
            throw 'The live database still contains data past its retention deadline; purge overdue requests before creating a backup.'
        }
        if ($null -eq $minimumDeadline -or $deadline -lt $minimumDeadline) {
            $minimumDeadline = $deadline
        }
    }
}

$sourceNames = @(Get-ChildItem -LiteralPath $receiptSource -File | ForEach-Object Name | Sort-Object)
$backupNames = @(Get-ChildItem -LiteralPath $receiptBackup -File | ForEach-Object Name | Sort-Object)
$expectedNames = @($requestFiles | ForEach-Object Name | Sort-Object)
if ((Compare-Object $sourceNames $expectedNames) -or (Compare-Object $backupNames $expectedNames)) {
    throw 'Receipt directories contain missing or unreferenced files; backup registration stopped.'
}

$backupFileItem = Get-Item -LiteralPath $dbBackup
if ($backupFileItem.Length -lt 1) { throw 'Database backup file is empty.' }
$purgeAt = $createdUtc.AddDays(7)
if ($minimumDeadline -and $minimumDeadline -lt $purgeAt) { $purgeAt = $minimumDeadline }
$databaseRelative = $dbBackup.Substring($setPrefix.Length).Replace('\', '/')
$receiptEntries = @($requestFiles | ForEach-Object {
    [pscustomobject]@{
        path = 'receipts/' + $_.Name
        sizeBytes = $_.Size
        sha256 = $_.Sha256
        retentionExpiresAtUtc = $_.RetentionExpiresAtUtc
    }
})
$manifest = [ordered]@{
    format = 'AURA_LOCAL_BACKUP_RETENTION_V1'
    backupSetName = (Split-Path -Leaf $setRoot)
    databaseBackup = [ordered]@{
        path = $databaseRelative
        sizeBytes = $backupFileItem.Length
        sha256 = (Get-FileHash -LiteralPath $dbBackup -Algorithm SHA256).Hash
    }
    createdAtUtc = $createdUtc.ToString('o')
    earliestIncludedDataExpiryUtc = if ($minimumDeadline) { $minimumDeadline.ToString('o') } else { $null }
    purgeAtUtc = $purgeAt.ToString('o')
    retentionDays = 7
    receiptCount = $receiptEntries.Count
    receipts = $receiptEntries
}

$manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
Write-Output ("Registered backup set {0}; receipts={1}; purgeAtUtc={2}." -f
    $manifest.backupSetName, $manifest.receiptCount, $manifest.purgeAtUtc)
