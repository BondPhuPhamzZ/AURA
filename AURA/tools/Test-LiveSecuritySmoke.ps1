[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^https://')]
    [string]$BaseUrl,

    [Parameter()]
    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http

$normalizedBaseUrl = $BaseUrl.TrimEnd('/')
$handler = [System.Net.Http.HttpClientHandler]::new()
$handler.AllowAutoRedirect = $false
$client = [System.Net.Http.HttpClient]::new($handler)
$client.Timeout = [TimeSpan]::FromSeconds(30)

function Invoke-RequestCheck {
    param(
        [Parameter(Mandatory)] [string]$Name,
        [Parameter(Mandatory)] [System.Net.Http.HttpMethod]$Method,
        [Parameter(Mandatory)] [string]$Path,
        [Parameter(Mandatory)] [int]$ExpectedStatus
    )

    $request = [System.Net.Http.HttpRequestMessage]::new($Method, "$normalizedBaseUrl$Path")
    try {
        if ($Method -eq [System.Net.Http.HttpMethod]::Post) {
            $request.Content = [System.Net.Http.StringContent]::new('')
        }

        $clock = [Diagnostics.Stopwatch]::StartNew()
        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $clock.Stop()

        [pscustomobject]@{
            test = $Name
            passed = [int]$response.StatusCode -eq $ExpectedStatus
            method = $Method.Method
            path = $Path
            httpStatus = [int]$response.StatusCode
            expectedHttpStatus = $ExpectedStatus
            elapsedMs = $clock.ElapsedMilliseconds
            responseContentType = if ($response.Content.Headers.ContentType) {
                $response.Content.Headers.ContentType.ToString()
            } else { $null }
            responseBodyPreview = if ($body.Length -gt 300) { $body.Substring(0, 300) } else { $body }
            testedAtUtc = [DateTime]::UtcNow.ToString('o')
        }
    }
    finally {
        $request.Dispose()
    }
}

try {
    $tests = @(
        Invoke-RequestCheck -Name 'business-rules-not-public' `
            -Method ([System.Net.Http.HttpMethod]::Get) -Path '/BUSINESS_RULES.md' -ExpectedStatus 404
        Invoke-RequestCheck -Name 'appsettings-not-public' `
            -Method ([System.Net.Http.HttpMethod]::Get) -Path '/appsettings.json' -ExpectedStatus 404
        Invoke-RequestCheck -Name 'verify-requires-antiforgery-token' `
            -Method ([System.Net.Http.HttpMethod]::Post) -Path '/Verify/RunHarness' -ExpectedStatus 400
        Invoke-RequestCheck -Name 'upload-requires-antiforgery-token' `
            -Method ([System.Net.Http.HttpMethod]::Post) -Path '/Applicant/UploadReceipt' -ExpectedStatus 400
    )

    $report = [pscustomobject]@{
        schemaVersion = 1
        baseUrl = $normalizedBaseUrl
        note = 'No receipt image, database mutation or AI request is sent by this smoke test.'
        passed = @($tests | Where-Object { -not $_.passed }).Count -eq 0
        tests = $tests
    }

    $json = $report | ConvertTo-Json -Depth 6
    $json

    if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
        $resolvedOutput = [IO.Path]::GetFullPath($OutputPath)
        $parent = Split-Path -Parent $resolvedOutput
        if (-not [string]::IsNullOrWhiteSpace($parent)) {
            New-Item -ItemType Directory -Force -Path $parent | Out-Null
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
