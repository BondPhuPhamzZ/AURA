#requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateRange(1024, 65535)][int]$Port = 5098,
    [ValidatePattern('^AURA_RetentionSandbox_[0-9]{8}$')]
    [string]$DatabaseName = ('AURA_RetentionSandbox_' + (Get-Date -Format 'yyyyMMdd')),
    [switch]$PrepareOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$receiptRoot = Join-Path $projectRoot ('.tmp\retention-sandbox\' + $DatabaseName + '\receipts')
$url = 'http://127.0.0.1:' + $Port
$overrides = @{
    ASPNETCORE_ENVIRONMENT = 'Development'
    ConnectionStrings__DefaultConnection = ('Server=(localdb)\MSSQLLocalDB;Database=' + $DatabaseName +
        ';Trusted_Connection=True;MultipleActiveResultSets=True;TrustServerCertificate=True;Connect Timeout=5;ConnectRetryCount=0')
    Database__ApplyMigrationsOnStartup = 'false'
    ReceiptStorage__Provider = 'LocalFileSystem'
    ReceiptStorage__Directory = $receiptRoot
    DataRetention__Enabled = 'true'
    DataRetention__BatchSize = '25'
    DataRetention__SweepIntervalMinutes = '5'
    DriveSandbox__Enabled = 'false'
    Verify__Enabled = 'false'
    Vision__FallbackEnabled = 'false'
    Vision__MaxConcurrentRequests = '1'
    Vision__UseSqlDistributedGate = 'false'
    OpenRouter__DenyProviderDataCollection = 'true'
}
$previous = @{}
$originalLocation = Get-Location
try {
    foreach ($name in $overrides.Keys) {
        $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
        [Environment]::SetEnvironmentVariable($name, $overrides[$name], 'Process')
    }
    Set-Location -LiteralPath $projectRoot
    New-Item -ItemType Directory -Path $receiptRoot -Force | Out-Null
    Write-Output ('Synthetic retention sandbox: ' + $DatabaseName + '; URL=' + $url)
    Write-Output 'Use synthetic receipts only. Inference uses the provider/model configured in this process and User Secrets.'
    & dotnet build AURA.csproj --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Sandbox build failed.' }
    # Always build this exact configuration before using --no-build with EF/run.
    & dotnet ef database update --configuration Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Sandbox database preparation failed.' }
    if ($PrepareOnly) { return }
    & dotnet run --project AURA.csproj --configuration Release --no-build --no-launch-profile --urls $url
    if ($LASTEXITCODE -ne 0) { throw 'Sandbox host stopped with an error.' }
}
finally {
    Set-Location -LiteralPath $originalLocation
    foreach ($name in $previous.Keys) {
        if ($null -eq $previous[$name]) {
            Remove-Item -LiteralPath ('Env:\' + $name) -ErrorAction SilentlyContinue
        }
        else {
            [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process')
        }
    }
}
